// wallet-stream — Gemini Live API WebSocket Proxy
//
// Bridges C# client ↔ Gemini Live API (BidiGenerateContent).
//
// Protocol (C# → Edge Function):
//   1. Connect via WebSocket with ?jwt=<token>&lang=<lang>
//   2. Send JSON config: { language, mode, systemPrompt? }
//   3. Send binary frames: raw 16kHz mono 16-bit PCM audio chunks
//   4. Send JSON: { type: "end_audio" } when recording stops
//
// Protocol (Edge Function → C#):
//   {"type":"ready"}                             — Gemini session established
//   {"type":"transcript","text":"..."}           — partial transcript
//   {"type":"final","text":"...","cost":N,"balance":N} — session complete
//   {"type":"error","message":"...","code":N}    — error
//   {"type":"fallback","reason":"..."}           — killswitch, use batch pipeline
//
// Gemini Live API wire format (proven via probe scripts 2025-04-06):
//   Setup:  AUDIO modality + speechConfig + inputAudioTranscription at root level
//   Audio:  { realtimeInput: { audio: { mimeType: "audio/pcm;rate=16000", data: <base64> } } }
//   End:    { realtimeInput: { audioStreamEnd: true } }
//   Msgs arrive as Blob: must decode via arrayBuffer() before JSON.parse()
//   Transcript: serverContent.inputTranscription.text

import { createClient } from "jsr:@supabase/supabase-js@2";

const GEMINI_LIVE_WS_URL =
  "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";

const LIVE_MODEL = "gemini-3.1-flash-live-preview";

// Live API pricing (audio input/output tokens)
const COST_PER_INPUT_TOKEN = 0.000001;  // $1.00/1M tokens
const COST_PER_OUTPUT_TOKEN = 0.000002; // $2.00/1M tokens

// ── Main handler ─────────────────────────────────────────────────────────────

Deno.serve(async (req: Request) => {
  const upgrade = req.headers.get("upgrade") ?? "";
  if (upgrade.toLowerCase() !== "websocket") {
    return new Response(
      JSON.stringify({ error: "WebSocket upgrade required" }),
      { status: 426, headers: { "Content-Type": "application/json" } },
    );
  }

  const url = new URL(req.url);
  const token = url.searchParams.get("jwt");
  const lang = url.searchParams.get("lang") ?? "en";

  if (!token) {
    return new Response(
      JSON.stringify({ error: "Missing jwt parameter" }),
      { status: 401, headers: { "Content-Type": "application/json" } },
    );
  }

  const { socket: clientWs, response } = Deno.upgradeWebSocket(req);

  handleSession(clientWs, token, lang).catch((err) => {
    console.error("[wallet-stream] Unhandled session error:", err);
    try {
      if (clientWs.readyState === WebSocket.OPEN) {
        clientWs.send(JSON.stringify({ type: "error", message: String(err), code: 500 }));
        clientWs.close(1011, "Internal error");
      }
    } catch { /* ignore */ }
  });

  return response;
});

// ── Session handler ───────────────────────────────────────────────────────────

async function handleSession(clientWs: WebSocket, token: string, lang: string) {
  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
  const geminiKey = Deno.env.get("GEMINI_API_KEY")!;

  const authClient = createClient(supabaseUrl, serviceKey, {
    global: { headers: { Authorization: `Bearer ${token}` } },
    auth: { persistSession: false },
  });
  const db = createClient(supabaseUrl, serviceKey, { auth: { persistSession: false } });

  // ── Auth ─────────────────────────────────────────────────────────────────
  const { data: { user }, error: authErr } = await authClient.auth.getUser(token);
  if (authErr || !user) {
    send(clientWs, { type: "error", message: "Unauthorized", code: 401 });
    clientWs.close(1008, "Unauthorized");
    return;
  }
  const userId = user.id;

  // ── Guards: frozen / killswitch / rate limit / balance ───────────────────
  const [{ data: freezeRow }, { data: modeRow }, { data: rateResult }, { data: balRow }] =
    await Promise.all([
      db.from("config").select("value").eq("key", "service_frozen").single(),
      db.from("config").select("value").eq("key", "wallet_pipeline_mode").single(),
      db.rpc("check_rate_limit", { p_user_id: userId, p_max_requests: 60 }),
      db.from("wallet_ledger").select("balance_after_micro").eq("user_id", userId)
        .order("created_at", { ascending: false }).limit(1).single(),
    ]);

  if (freezeRow?.value === "true") {
    send(clientWs, { type: "error", message: "Service unavailable", code: 503 });
    clientWs.close(1008, "Frozen"); return;
  }
  if ((modeRow?.value ?? "streaming") !== "streaming") {
    send(clientWs, { type: "fallback", reason: `pipeline_mode=${modeRow?.value}` });
    clientWs.close(1000, "Killswitch"); return;
  }
  if (rateResult && !rateResult.allowed) {
    send(clientWs, { type: "error", message: "Rate limit exceeded", code: 429 });
    clientWs.close(1008, "Rate limited"); return;
  }
  const currentBalance = balRow?.balance_after_micro ?? 0;
  if (currentBalance < 10000) {
    send(clientWs, { type: "error", message: "Insufficient wallet balance", code: 402 });
    clientWs.close(1008, "Insufficient balance"); return;
  }

  // ── Session state ─────────────────────────────────────────────────────────
  let fullTranscript = "";
  let usageInput = 0;
  let usageOutput = 0;
  let geminiReady = false;
  let sessionComplete = false;
  let audioEnded = false;
  const pendingAudio: Uint8Array[] = []; // buffer before geminiReady

  // ── Gemini Live WS ────────────────────────────────────────────────────────
  const systemText =
    `Transcribe the audio in ${lang}. Remove filler words. Add punctuation. Output only the transcribed text.`;

  const geminiSetup = {
    model: `models/${LIVE_MODEL}`,
    generationConfig: {
      responseModalities: ["AUDIO"],
      speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName: "Aoede" } } },
    },
    inputAudioTranscription: {},
    systemInstruction: { parts: [{ text: systemText }] },
  };

  const geminiWs = new WebSocket(`${GEMINI_LIVE_WS_URL}?key=${geminiKey}`);

  // Promise that resolves when Gemini sends setupComplete
  let resolveReady!: () => void;
  let rejectReady!: (err: Error) => void;
  const readyPromise = new Promise<void>((res, rej) => {
    resolveReady = res;
    rejectReady = rej;
  });
  const readyTimeout = setTimeout(
    () => rejectReady(new Error("Gemini setup timeout (15s)")),
    15000,
  );

  // Central Gemini message handler — persistent for lifetime of session
  geminiWs.addEventListener("message", async (evt: MessageEvent) => {
    let text: string;
    try {
      text = await msgToText(evt.data);
    } catch {
      return;
    }

    let msg: Record<string, unknown>;
    try {
      msg = JSON.parse(text);
    } catch {
      console.warn("[wallet-stream] Non-JSON from Gemini:", text.slice(0, 100));
      return;
    }

    // Setup complete
    if (msg.setupComplete !== undefined) {
      clearTimeout(readyTimeout);
      geminiReady = true;
      resolveReady();
      return;
    }

    // Server content (transcripts, turn complete)
    if (msg.serverContent) {
      const sc = msg.serverContent as Record<string, unknown>;

      const transcriptText =
        (sc.inputTranscription as Record<string, unknown> | undefined)?.text as string | undefined;

      if (transcriptText && transcriptText.trim()) {
        fullTranscript = (fullTranscript + " " + transcriptText).trim();
        send(clientWs, { type: "transcript", text: transcriptText });
      }

      if (sc.turnComplete === true) {
        sessionComplete = true;
      }
    }

    // Usage metadata
    if (msg.usageMetadata) {
      const u = msg.usageMetadata as Record<string, number>;
      usageInput += u.promptTokenCount ?? 0;
      usageOutput += u.candidatesTokenCount ?? 0;
    }

    // Session resumption handles — just ignore
    // if (msg.sessionResumptionUpdate) { /* ignore */ }
  });

  geminiWs.addEventListener("open", () => {
    console.log("[wallet-stream] Gemini WS open, sending setup");
    geminiWs.send(JSON.stringify({ setup: geminiSetup }));
  });

  geminiWs.addEventListener("error", (e: Event) => {
    console.error("[wallet-stream] Gemini WS error:", (e as ErrorEvent).message);
    rejectReady(new Error(`Gemini WS error: ${(e as ErrorEvent).message}`));
  });

  geminiWs.addEventListener("close", (e: CloseEvent) => {
    console.log(`[wallet-stream] Gemini WS closed: ${e.code} ${e.reason}`);
    if (!geminiReady) {
      rejectReady(new Error(`Gemini closed before ready: ${e.code} ${e.reason}`));
    } else {
      // May arrive before turnComplete — treat as session end
      sessionComplete = true;
    }
  });

  // ── Wire C# client messages ───────────────────────────────────────────────
  clientWs.addEventListener("message", async (evt: MessageEvent) => {
    // Binary = PCM audio chunk
    if (evt.data instanceof ArrayBuffer || evt.data instanceof Uint8Array) {
      const chunk = evt.data instanceof ArrayBuffer
        ? new Uint8Array(evt.data)
        : (evt.data as Uint8Array);

      if (!geminiReady) {
        pendingAudio.push(chunk);
      } else {
        await sendAudio(geminiWs, chunk);
      }
      return;
    }

    // Text = control message
    if (typeof evt.data === "string") {
      let msg: Record<string, unknown>;
      try {
        msg = JSON.parse(evt.data);
      } catch {
        return;
      }

      if (msg.type === "end_audio") {
        audioEnded = true;
        if (geminiReady) {
          sendAudioEnd(geminiWs);
        }
      }
    }
  });

  // ── Wait for Gemini setup, flush buffer ───────────────────────────────────
  try {
    await readyPromise;
    console.log(
      `[wallet-stream] Gemini ready. Flushing ${pendingAudio.length} buffered chunks...`,
    );

    // Tell C# we're ready (it's now in live-streaming mode)
    send(clientWs, { type: "ready" });

    // Flush audio buffer
    for (const chunk of pendingAudio) {
      await sendAudio(geminiWs, chunk);
    }
    pendingAudio.length = 0;

    // If end_audio was already sent before ready, relay it now
    if (audioEnded) {
      sendAudioEnd(geminiWs);
    }
  } catch (err) {
    console.error("[wallet-stream] Gemini initialization failed:", err);
    send(clientWs, { type: "error", message: String(err), code: 502 });
    clientWs.close(1011, "Gemini init failed");
    return;
  }

  // ── Wait for turn complete (up to 60s) ───────────────────────────────────
  const deadline = Date.now() + 60_000;
  while (!sessionComplete && Date.now() < deadline) {
    if (clientWs.readyState !== WebSocket.OPEN) break;
    await sleep(100);
  }

  // ── Cost accounting and final response ────────────────────────────────────
  const costDollars = usageInput * COST_PER_INPUT_TOKEN + usageOutput * COST_PER_OUTPUT_TOKEN;
  // Minimum estimate: $0.0005 for any non-empty transcription
  let costMicro = Math.max(1, Math.round(costDollars * 1_000_000));
  if (usageInput === 0 && fullTranscript.length > 0) {
    costMicro = 500;
  }

  const { data: deductResult } = await db.rpc("deduct_wallet_balance", {
    p_user_id: userId,
    p_amount_micro: costMicro,
    p_type: "USAGE",
    p_metadata: { service: "gemini-live" },
  });
  const newBalance = deductResult?.balance_micro ?? (currentBalance - costMicro);

  await db.from("proxy_audit_log").insert({
    user_id: userId,
    service: "gemini-live",
    cost_micro: costMicro,
    input_tokens: usageInput || null,
    output_tokens: usageOutput || null,
    status_code: 200,
  });

  // Send final result
  send(clientWs, {
    type: "final",
    text: fullTranscript,
    cost: costMicro,
    balance: newBalance,
  });

  // Clean close
  try { geminiWs.close(1000, "Done"); } catch { /* ignore */ }
  try { clientWs.close(1000, "Done"); } catch { /* ignore */ }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function send(ws: WebSocket, msg: Record<string, unknown>) {
  try {
    if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
  } catch { /* ignore */ }
}

async function sendAudio(geminiWs: WebSocket, pcm: Uint8Array) {
  if (geminiWs.readyState !== WebSocket.OPEN) return;

  // Encode PCM bytes to base64 without spreading large arrays (avoids stack overflow)
  const b64 = uint8ToBase64(pcm);

  geminiWs.send(JSON.stringify({
    realtimeInput: {
      audio: { mimeType: "audio/pcm;rate=16000", data: b64 },
    },
  }));
}

function sendAudioEnd(geminiWs: WebSocket) {
  if (geminiWs.readyState !== WebSocket.OPEN) return;
  geminiWs.send(JSON.stringify({ realtimeInput: { audioStreamEnd: true } }));
}

async function msgToText(data: unknown): Promise<string> {
  if (typeof data === "string") return data;
  if (data instanceof Blob) return new TextDecoder().decode(await data.arrayBuffer());
  if (data instanceof ArrayBuffer) return new TextDecoder().decode(data);
  throw new Error("Unknown message data type");
}

// Safe base64 encoding for large Uint8Arrays (no stack overflow)
function uint8ToBase64(bytes: Uint8Array): string {
  let binary = "";
  const len = bytes.length;
  for (let i = 0; i < len; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
