// Wallet Proxy Edge Function — routes authenticated requests to upstream
// Gemini APIs using operator master keys. Single model for both STT (audio)
// and LLM (text) processing. Deepgram handler kept for rollback.
//
// Security controls:
// 1. JWT crypto-validation via supabase.auth.getUser()
// 2. Service freeze check (config.service_frozen)
// 3. Per-user rate limiting (60 req/min via check_rate_limit RPC)
// 4. Pre-check: reject if balance < $0.01
// 5. Request body size limits (10MB audio, 100KB text)
// 6. Forward to upstream with operator master key
// 7. Calculate exact cost from upstream response metadata
// 8. Atomic deduction via deduct_wallet_balance RPC (FOR UPDATE lock)
// 9. Return X-Wallet-Cost / X-Wallet-Balance headers
// 10. Audit logging to proxy_audit_log table

import { createClient } from "jsr:@supabase/supabase-js@2";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, content-type",
  "Access-Control-Expose-Headers": "X-Wallet-Cost, X-Wallet-Balance, X-Timing-Jwt, X-Timing-Checks, X-Timing-Parse, X-Timing-Upstream, X-Timing-Deduct, X-Timing-Total, X-Timing-Base64, X-Timing-Fetch, X-Timing-HasPrompt",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

// Single model for all wallet operations (STT audio + LLM text)
const WALLET_MODEL = "gemini-2.5-flash";

// Gemini pricing: $0.15/1M input tokens, $0.60/1M output tokens (2.5 Flash)
const GEMINI_INPUT_COST_PER_TOKEN = 0.00000015; // $0.15 / 1M
const GEMINI_OUTPUT_COST_PER_TOKEN = 0.00000060; // $0.60 / 1M
// Deepgram pricing: ~$0.0077/min for Nova-3 (kept for rollback)
const DEEPGRAM_COST_PER_MINUTE = 0.0077;

const MAX_TEXT_BODY_BYTES = 100 * 1024; // 100KB
const MAX_AUDIO_BODY_BYTES = 10 * 1024 * 1024; // 10MB
const MAX_SINGLE_REQUEST_COST_MICRO = 500000; // $0.50 cap

Deno.serve(async (req: Request) => {
  const t0 = performance.now();

  // CORS preflight
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }

  if (req.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  // ── 1. JWT Validation ───────────────────────────────────────────────
  const authHeader = req.headers.get("authorization");
  if (!authHeader?.startsWith("Bearer ")) {
    return jsonResponse({ error: "Missing authorization" }, 401);
  }
  const token = authHeader.slice(7);

  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

  // Auth client uses the user's JWT to validate identity
  const authClient = createClient(supabaseUrl, supabaseServiceKey, {
    global: { headers: { Authorization: `Bearer ${token}` } },
    auth: { persistSession: false },
  });

  const { data: { user }, error: authError } = await authClient.auth.getUser(token);
  if (authError || !user) {
    return jsonResponse({ error: "Invalid or expired token" }, 401);
  }
  const t1 = performance.now();

  const userId = user.id;

  // Service client for DB operations (uses service_role key)
  const db = createClient(supabaseUrl, supabaseServiceKey, {
    auth: { persistSession: false },
  });

  // ── 2. Service Freeze + Pipeline Mode Check ─────────────────────────
  const { data: configRows } = await db
    .from("config")
    .select("key, value")
    .in("key", ["service_frozen", "wallet_pipeline_mode"]);

  const configMap = new Map(
    (configRows ?? []).map((r: { key: string; value: string }) => [
      r.key,
      r.value,
    ]),
  );

  if (configMap.get("service_frozen") === "true") {
    return jsonResponse({ error: "Service temporarily unavailable" }, 503);
  }

  // Pipeline mode determines batch routing when killswitch is active
  const pipelineMode = configMap.get("wallet_pipeline_mode") ?? "streaming";

  // ── 3. Rate Limiting ────────────────────────────────────────────────
  const { data: rateResult } = await db.rpc("check_rate_limit", {
    p_user_id: userId,
    p_max_requests: 60,
  });

  if (rateResult && !rateResult.allowed) {
    return jsonResponse({ error: "Rate limit exceeded" }, 429);
  }

  const t2 = performance.now();

  // ── 4. Parse Request Body ───────────────────────────────────────────
  const contentType = req.headers.get("content-type") ?? "";
  let service: string;
  let requestBody: Record<string, unknown> | null = null;
  let audioBlob: Uint8Array | null = null;
  let language = "en";
  let audioSystemPrompt: string | null = null;
  let audioMode: string | null = null;

  if (contentType.includes("multipart/form-data")) {
    // STT request (audio upload) — may include systemPrompt for combined mode
    const formData = await req.formData();
    service = (formData.get("service") as string) ?? "gemini-audio";
    language = (formData.get("language") as string) ?? "en";
    audioSystemPrompt = (formData.get("systemPrompt") as string) ?? null;
    audioMode = (formData.get("mode") as string) ?? null;
    const audioFile = formData.get("audio") as File | null;

    if (!audioFile) {
      return jsonResponse({ error: "Missing audio file" }, 400);
    }

    audioBlob = new Uint8Array(await audioFile.arrayBuffer());
    if (audioBlob.length > MAX_AUDIO_BODY_BYTES) {
      return jsonResponse({ error: "Audio file too large (max 10MB)" }, 413);
    }
  } else {
    // LLM request (JSON)
    const rawBody = await req.text();
    if (rawBody.length > MAX_TEXT_BODY_BYTES) {
      return jsonResponse({ error: "Request body too large (max 100KB)" }, 413);
    }

    try {
      requestBody = JSON.parse(rawBody);
    } catch {
      return jsonResponse({ error: "Invalid JSON" }, 400);
    }

    service = (requestBody?.service as string) ?? "gemini";
  }

  const t3 = performance.now();

  // ── 5. Balance Pre-check ────────────────────────────────────────────
  const { data: balanceRow } = await db
    .from("wallet_ledger")
    .select("balance_after_micro")
    .eq("user_id", userId)
    .order("created_at", { ascending: false })
    .limit(1)
    .single();

  const currentBalance = balanceRow?.balance_after_micro ?? 0;
  if (currentBalance < 10000) {
    // < $0.01
    return jsonResponse(
      { error: "Insufficient wallet balance" },
      402,
      { "X-Wallet-Balance": String(currentBalance) },
    );
  }

  // ── 6-7. Forward to Upstream ────────────────────────────────────────
  // Killswitch routing: when batch_fast, force audio requests through Deepgram
  if (audioBlob && pipelineMode === "batch_fast" && service === "gemini-audio") {
    service = "deepgram";
  }

  let upstreamResponse: Response;
  let costMicro: number;
  let inputTokens: number | null = null;
  let outputTokens: number | null = null;
  let audioDurationMs: number | null = null;
  let resultPayload: Record<string, unknown>;
  let audioTimingBase64 = 0;
  let audioTimingFetch = 0;
  let audioHasPrompt = false;

  try {
    if (service === "gemini-audio") {
      const result = await handleGeminiAudio(
        audioBlob!,
        language,
        audioSystemPrompt,
        audioMode,
      );
      upstreamResponse = result.response;
      costMicro = result.costMicro;
      inputTokens = result.inputTokens;
      outputTokens = result.outputTokens;
      resultPayload = result.payload;
      audioTimingBase64 = result.timingBase64Ms;
      audioTimingFetch = result.timingFetchMs;
      audioHasPrompt = result.hasPrompt;
    } else if (service === "deepgram") {
      // Kept for rollback — switch C# service field to "deepgram" to revert
      const result = await handleDeepgram(audioBlob!, language);
      upstreamResponse = result.response;
      costMicro = result.costMicro;
      audioDurationMs = result.audioDurationMs;
      resultPayload = result.payload;
    } else {
      const result = await handleGemini(requestBody!);
      upstreamResponse = result.response;
      costMicro = result.costMicro;
      inputTokens = result.inputTokens;
      outputTokens = result.outputTokens;
      resultPayload = result.payload;
    }
  } catch (err) {
    console.error("Upstream error:", err);
    // Log failed attempt
    await db.from("proxy_audit_log").insert({
      user_id: userId,
      service,
      cost_micro: 0,
      status_code: 502,
    });
    return jsonResponse({ error: "Upstream service error" }, 502);
  }

  const t4 = performance.now();

  if (!upstreamResponse.ok) {
    const statusCode = upstreamResponse.status;
    await db.from("proxy_audit_log").insert({
      user_id: userId,
      service,
      cost_micro: 0,
      status_code: statusCode,
    });
    return jsonResponse(
      { error: `Upstream returned ${statusCode}` },
      statusCode >= 500 ? 502 : statusCode,
    );
  }

  // ── 8. Cap cost and atomic deduction ────────────────────────────────
  costMicro = Math.min(costMicro, MAX_SINGLE_REQUEST_COST_MICRO);
  // Minimum 1 microdollar (avoid zero-cost entries)
  costMicro = Math.max(costMicro, 1);

  const { data: deductResult } = await db.rpc("deduct_wallet_balance", {
    p_user_id: userId,
    p_amount_micro: costMicro,
    p_type: "USAGE",
    p_metadata: { service, mode: audioMode ?? requestBody?.mode ?? "stt" },
  });

  const newBalance = deductResult?.balance_micro ?? (currentBalance - costMicro);

  // ── 9. Audit Log ───────────────────────────────────────────────────
  await db.from("proxy_audit_log").insert({
    user_id: userId,
    service,
    cost_micro: costMicro,
    input_tokens: inputTokens,
    output_tokens: outputTokens,
    audio_duration_ms: audioDurationMs,
    status_code: 200,
  });

  // ── 10. Return response with wallet headers + timing ─────────────────
  const t5 = performance.now();

  return jsonResponse(resultPayload, 200, {
    "X-Wallet-Cost": String(costMicro),
    "X-Wallet-Balance": String(newBalance),
    "X-Timing-Jwt": String(Math.round(t1 - t0)),
    "X-Timing-Checks": String(Math.round(t2 - t1)),
    "X-Timing-Parse": String(Math.round(t3 - t2)),
    "X-Timing-Upstream": String(Math.round(t4 - t3)),
    "X-Timing-Deduct": String(Math.round(t5 - t4)),
    "X-Timing-Total": String(Math.round(t5 - t0)),
    "X-Timing-Base64": String(audioTimingBase64),
    "X-Timing-Fetch": String(audioTimingFetch),
    "X-Timing-HasPrompt": String(audioHasPrompt),
  });
});

// ── Gemini Handler ──────────────────────────────────────────────────────

interface GeminiResult {
  response: Response;
  costMicro: number;
  inputTokens: number | null;
  outputTokens: number | null;
  payload: Record<string, unknown>;
}

async function handleGemini(
  body: Record<string, unknown>,
): Promise<GeminiResult> {
  const apiKey = Deno.env.get("GEMINI_API_KEY")!;
  const text = (body.text as string) ?? "";
  const systemPrompt = (body.systemPrompt as string) ?? "";
  const mode = (body.mode as string) ?? "dictate";

  // Build Gemini request
  const geminiBody = {
    system_instruction: { parts: [{ text: systemPrompt }] },
    contents: [{ parts: [{ text }] }],
    generationConfig: {
      temperature: mode === "dictate" ? 0.3 : 0.7,
      maxOutputTokens: 8192,
    },
  };

  const url =
    `https://generativelanguage.googleapis.com/v1beta/models/${WALLET_MODEL}:generateContent?key=${apiKey}`;

  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(geminiBody),
  });

  if (!response.ok) {
    return {
      response,
      costMicro: 0,
      inputTokens: null,
      outputTokens: null,
      payload: {},
    };
  }

  const data = await response.json();
  const resultText =
    data?.candidates?.[0]?.content?.parts?.[0]?.text ?? "";
  const usage = data?.usageMetadata;
  const inTokens = usage?.promptTokenCount ?? 0;
  const outTokens = usage?.candidatesTokenCount ?? 0;

  // Calculate cost in microdollars
  const costDollars =
    inTokens * GEMINI_INPUT_COST_PER_TOKEN +
    outTokens * GEMINI_OUTPUT_COST_PER_TOKEN;
  const costMicro = Math.round(costDollars * 1_000_000);

  return {
    response,
    costMicro,
    inputTokens: inTokens,
    outputTokens: outTokens,
    payload: {
      text: resultText,
      inputTokens: inTokens,
      outputTokens: outTokens,
    },
  };
}

// ── Gemini Audio Handler (STT, or combined STT+LLM) ────────────────────

interface GeminiAudioResult {
  response: Response;
  costMicro: number;
  inputTokens: number | null;
  outputTokens: number | null;
  payload: Record<string, unknown>;
  timingBase64Ms: number;
  timingFetchMs: number;
  hasPrompt: boolean;
}

async function handleGeminiAudio(
  audio: Uint8Array,
  language: string,
  systemPrompt: string | null,
  mode: string | null,
): Promise<GeminiAudioResult> {
  const apiKey = Deno.env.get("GEMINI_API_KEY")!;
  const ta = performance.now();

  // Base64-encode the audio for Gemini's inline_data format
  let audioBase64 = "";
  const chunkSize = 32768;
  for (let i = 0; i < audio.length; i += chunkSize) {
    audioBase64 += String.fromCharCode(
      ...audio.subarray(i, Math.min(i + chunkSize, audio.length)),
    );
  }
  audioBase64 = btoa(audioBase64);
  const tb = performance.now();

  // For wallet dictation, use a short unified prompt for all calls.
  // This replaces both the 325-char C# system prompt and the transcription-only instruction.
  const langName = language === "auto" ? "the detected language"
    : language === "es" ? "Spanish"
    : "English";
  const walletPrompt = systemPrompt && systemPrompt.trim() !== "" && mode === "ask"
    ? systemPrompt
    : `Transcribe in ${langName}, remove fillers, add punctuation.`;

  // Build content parts — audio is always included
  // deno-lint-ignore no-explicit-any
  const parts: any[] = [];

  parts.push({
    inlineData: { mimeType: "audio/wav", data: audioBase64 },
  });

  // deno-lint-ignore no-explicit-any
  const body: any = {
    system_instruction: { parts: [{ text: walletPrompt }] },
    contents: [{ parts }],
    generationConfig: {
      temperature: 0.1,
      maxOutputTokens: 8192,
    },
  };

  const url =
    `https://generativelanguage.googleapis.com/v1beta/models/${WALLET_MODEL}:generateContent?key=${apiKey}`;

  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const tc = performance.now();

  if (!response.ok) {
    return {
      response,
      costMicro: 0,
      inputTokens: null,
      outputTokens: null,
      payload: {},
      timingBase64Ms: Math.round(tb - ta),
      timingFetchMs: Math.round(tc - tb),
      hasPrompt: !!systemPrompt,
    };
  }

  const data = await response.json();
  const resultText =
    data?.candidates?.[0]?.content?.parts?.[0]?.text ?? "";
  const usage = data?.usageMetadata;
  const inTokens = usage?.promptTokenCount ?? 0;
  const outTokens = usage?.candidatesTokenCount ?? 0;

  // Calculate cost in microdollars (audio tokens included in promptTokenCount)
  const costDollars =
    inTokens * GEMINI_INPUT_COST_PER_TOKEN +
    outTokens * GEMINI_OUTPUT_COST_PER_TOKEN;
  const costMicro = Math.round(costDollars * 1_000_000);

  return {
    response,
    costMicro,
    inputTokens: inTokens,
    outputTokens: outTokens,
    payload: {
      text: resultText,
      inputTokens: inTokens,
      outputTokens: outTokens,
    },
    timingBase64Ms: Math.round(tb - ta),
    timingFetchMs: Math.round(tc - tb),
    hasPrompt: !!systemPrompt,
  };
}

// ── Deepgram Handler (kept for rollback) ────────────────────────────────

interface DeepgramResult {
  response: Response;
  costMicro: number;
  audioDurationMs: number | null;
  payload: Record<string, unknown>;
}

async function handleDeepgram(
  audio: Uint8Array,
  language: string,
): Promise<DeepgramResult> {
  const apiKey = Deno.env.get("DEEPGRAM_API_KEY")!;

  const url = new URL("https://api.deepgram.com/v1/listen");
  url.searchParams.set("model", "nova-3");
  url.searchParams.set("language", language);
  url.searchParams.set("punctuate", "true");
  url.searchParams.set("smart_format", "true");

  const response = await fetch(url.toString(), {
    method: "POST",
    headers: {
      Authorization: `Token ${apiKey}`,
      "Content-Type": "audio/wav",
    },
    body: audio,
  });

  if (!response.ok) {
    return { response, costMicro: 0, audioDurationMs: null, payload: {} };
  }

  const data = await response.json();
  const transcript =
    data?.results?.channels?.[0]?.alternatives?.[0]?.transcript ?? "";
  const durationSeconds = data?.metadata?.duration ?? 0;
  const durationMs = Math.round(durationSeconds * 1000);

  // Calculate cost: $0.0077/min
  const durationMinutes = durationSeconds / 60;
  const costDollars = durationMinutes * DEEPGRAM_COST_PER_MINUTE;
  const costMicro = Math.round(costDollars * 1_000_000);

  return {
    response,
    costMicro,
    audioDurationMs: durationMs,
    payload: {
      transcript,
      duration_ms: durationMs,
      confidence:
        data?.results?.channels?.[0]?.alternatives?.[0]?.confidence ?? 0,
    },
  };
}

// ── Helpers ─────────────────────────────────────────────────────────────

function jsonResponse(
  body: Record<string, unknown>,
  status: number,
  extraHeaders: Record<string, string> = {},
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...CORS_HEADERS,
      ...extraHeaders,
    },
  });
}
