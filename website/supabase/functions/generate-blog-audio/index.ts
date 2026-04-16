// generate-blog-audio — Supabase Edge Function
// Generates a WAV audio file from a blog post using Gemini 3.1 Flash TTS,
// uploads it to the blog-audio storage bucket, and saves the URL on the post row.
//
// Called directly from the HQ admin panel (browser → Edge Function) to avoid
// Vercel's 10s serverless timeout. Edge Functions support up to 150s (free plan).

import { createClient } from "jsr:@supabase/supabase-js@2";
import { decodeBase64 } from "jsr:@std/encoding@1/base64";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const GEMINI_TTS_MODEL = "gemini-3.1-flash-tts-preview";
const GEMINI_TTS_URL = `https://generativelanguage.googleapis.com/v1beta/models/${GEMINI_TTS_MODEL}:generateContent`;
const TTS_VOICE = "Kore";
const SAMPLE_RATE = 24_000;
const BITS_PER_SAMPLE = 16;
const CHANNELS = 1;

// ── Text processing ──────────────────────────────────────────────────────────

function stripMarkdown(text: string): string {
  return text
    .replace(/```[\s\S]*?```/g, "")          // fenced code blocks
    .replace(/^#{1,6}\s+/gm, "")             // headings
    .replace(/\*\*(.+?)\*\*/g, "$1")         // **bold**
    .replace(/\*(.+?)\*/g, "$1")             // *italic*
    .replace(/__(.+?)__/g, "$1")             // __bold__
    .replace(/_(.+?)_/g, "$1")               // _italic_
    .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1") // [link](url) → text
    .replace(/^>\s*/gm, "")                  // blockquotes
    .replace(/^[-*+]\s+/gm, "")              // bullet lists
    .replace(/^\d+\.\s+/gm, "")              // numbered lists
    .replace(/^---+$/gm, "")                 // horizontal rules
    .replace(/`([^`]+)`/g, "$1")             // `inline code`
    .replace(/\n{3,}/g, "\n\n")             // normalise blank lines
    .trim();
}

/**
 * Assembles the TTS script with structural expressive tags:
 *  [excited]    → title     (headline energy)
 *  [curious]    → hook      (draws the listener in)
 *  (no tag)     → body      (flat, neutral news delivery)
 *  [thoughtful] → closing   (reflective landing)
 */
function buildTtsScript(
  title: string,
  hook: string | null,
  body: string,
  closing: string | null,
): string {
  const parts: string[] = [];
  parts.push(`[excited] ${title}`);
  if (hook?.trim()) parts.push(`[curious] ${hook.trim()}`);
  const strippedBody = stripMarkdown(body);
  if (strippedBody) parts.push(strippedBody);
  if (closing?.trim()) parts.push(`[thoughtful] ${stripMarkdown(closing.trim())}`);
  return parts.join("\n\n");
}

// ── WAV builder ──────────────────────────────────────────────────────────────

/** Wraps raw Gemini PCM (24 kHz, 16-bit, mono) in a standard 44-byte WAV header. */
function buildWav(pcm: Uint8Array): Uint8Array {
  const dataSize = pcm.byteLength;
  const byteRate = SAMPLE_RATE * CHANNELS * (BITS_PER_SAMPLE / 8);
  const blockAlign = CHANNELS * (BITS_PER_SAMPLE / 8);

  const header = new ArrayBuffer(44);
  const v = new DataView(header);
  const str = (s: string, offset: number) =>
    [...s].forEach((c, i) => v.setUint8(offset + i, c.charCodeAt(0)));

  str("RIFF", 0);  v.setUint32(4, 36 + dataSize, true);
  str("WAVE", 8);  str("fmt ", 12);
  v.setUint32(16, 16, true);           // fmt chunk size
  v.setUint16(20, 1, true);            // PCM
  v.setUint16(22, CHANNELS, true);
  v.setUint32(24, SAMPLE_RATE, true);
  v.setUint32(28, byteRate, true);
  v.setUint16(32, blockAlign, true);
  v.setUint16(34, BITS_PER_SAMPLE, true);
  str("data", 36); v.setUint32(40, dataSize, true);

  const wav = new Uint8Array(44 + dataSize);
  wav.set(new Uint8Array(header), 0);
  wav.set(pcm, 44);
  return wav;
}

// decodeBase64 is now provided by jsr:@std/encoding/base64 (native Rust, not a JS loop)

// ── Main handler ─────────────────────────────────────────────────────────────

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405, headers: CORS_HEADERS });
  }

  const t0 = performance.now();
  console.log("[generate-blog-audio] Request received");

  try {
    // ── Auth: validate JWT + check admin ──────────────────────────────────
    const authHeader = req.headers.get("authorization");
    if (!authHeader?.startsWith("Bearer ")) {
      console.log("[generate-blog-audio] Missing or invalid auth header");
      return new Response("Unauthorized", { status: 401, headers: CORS_HEADERS });
    }

    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const geminiApiKey = Deno.env.get("GEMINI_API_KEY");

    if (!geminiApiKey) {
      console.error("[generate-blog-audio] GEMINI_API_KEY secret not set");
      return new Response("GEMINI_API_KEY not configured", { status: 500, headers: CORS_HEADERS });
    }

    // Verify the user JWT
    const anonKey = Deno.env.get("SUPABASE_ANON_KEY")!;
    const userClient = createClient(supabaseUrl, anonKey, {
      global: { headers: { Authorization: authHeader } },
    });
    const { data: { user }, error: authError } = await userClient.auth.getUser();
    if (authError || !user) {
      console.log("[generate-blog-audio] JWT invalid:", authError?.message);
      return new Response("Unauthorized", { status: 401, headers: CORS_HEADERS });
    }

    // Check admin flag
    const adminClient = createClient(supabaseUrl, serviceRoleKey);
    const { data: profile } = await adminClient
      .from("profiles")
      .select("is_admin")
      .eq("id", user.id)
      .single();

    if (!profile?.is_admin) {
      console.log("[generate-blog-audio] User is not admin:", user.id);
      return new Response("Forbidden", { status: 403, headers: CORS_HEADERS });
    }

    console.log(`[generate-blog-audio] Admin verified: ${user.email} (${Math.round(performance.now() - t0)}ms)`);

    // ── Parse request ─────────────────────────────────────────────────────
    const body = await req.json() as { post_id?: string; lang?: string };
    const { post_id, lang } = body;

    if (!post_id || (lang !== "en" && lang !== "es")) {
      return new Response('post_id and lang ("en"|"es") are required', { status: 400, headers: CORS_HEADERS });
    }

    // ── Fetch post content ────────────────────────────────────────────────
    const { data: post, error: fetchError } = await adminClient
      .from("blog_posts")
      .select("slug, title_en, hook_en, body_en, closing_en, title_es, hook_es, body_es, closing_es")
      .eq("id", post_id)
      .single();

    if (fetchError || !post) {
      console.error("[generate-blog-audio] Post not found:", post_id);
      return new Response("Post not found", { status: 404, headers: CORS_HEADERS });
    }

    if (!/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/.test(post.slug)) {
      return new Response("Post has invalid slug format", { status: 400, headers: CORS_HEADERS });
    }

    const title   = lang === "en" ? post.title_en   : post.title_es;
    const hook    = lang === "en" ? post.hook_en    : post.hook_es;
    const bodyTxt = lang === "en" ? post.body_en    : post.body_es;
    const closing = lang === "en" ? post.closing_en : post.closing_es;

    if (!title || !bodyTxt) {
      return new Response(`Missing ${lang.toUpperCase()} content`, { status: 400, headers: CORS_HEADERS });
    }

    const ttsScript = buildTtsScript(title, hook, bodyTxt, closing);
    const wordCount = ttsScript.split(/\s+/).length;
    const charCount = ttsScript.length;
    console.log(`[generate-blog-audio] TTS script ready: ${wordCount} words, ${charCount} chars, lang=${lang}, post=${post.slug} (${Math.round(performance.now() - t0)}ms)`);

    // ── Call Gemini 3.1 Flash TTS ─────────────────────────────────────────
    const tGemini = performance.now();
    console.log(`[generate-blog-audio] Calling Gemini TTS...`);
    const geminiRes = await fetch(GEMINI_TTS_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": geminiApiKey,
      },
      body: JSON.stringify({
        contents: [{ parts: [{ text: ttsScript }] }],
        generationConfig: {
          responseModalities: ["AUDIO"],
          speechConfig: {
            voiceConfig: {
              prebuiltVoiceConfig: { voiceName: TTS_VOICE },
            },
          },
        },
      }),
    });

    console.log(`[generate-blog-audio] Gemini responded: ${geminiRes.status} (${Math.round(performance.now() - tGemini)}ms, total=${Math.round(performance.now() - t0)}ms)`);

    if (!geminiRes.ok) {
      const errBody = await geminiRes.text();
      console.error(`[generate-blog-audio] Gemini error ${geminiRes.status}:`, errBody.slice(0, 300));
      return new Response(`Gemini TTS failed: ${geminiRes.status}`, { status: 502, headers: CORS_HEADERS });
    }

    const geminiData = await geminiRes.json() as {
      candidates?: { content?: { parts?: { inlineData?: { data?: string } }[] } }[];
    };
    const base64Audio = geminiData?.candidates?.[0]?.content?.parts?.[0]?.inlineData?.data;

    if (!base64Audio) {
      console.error("[generate-blog-audio] No audio in Gemini response:", JSON.stringify(geminiData).slice(0, 300));
      return new Response("No audio data in Gemini response", { status: 502, headers: CORS_HEADERS });
    }

    // ── Build WAV and upload ──────────────────────────────────────────────
    const tDecode = performance.now();
    const pcm = decodeBase64(base64Audio);
    console.log(`[generate-blog-audio] Base64 decoded: ${pcm.byteLength} bytes PCM (${Math.round(performance.now() - tDecode)}ms)`);
    const wav = buildWav(pcm);
    const wavKb = Math.round(wav.byteLength / 1024);
    const durationSec = Math.round(pcm.byteLength / 2 / SAMPLE_RATE);
    console.log(`[generate-blog-audio] WAV built: ${wavKb}KB (~${durationSec}s audio)`);

    const fileName = `${post.slug}-${lang}.wav`;
    const tUpload = performance.now();
    const { error: uploadError } = await adminClient.storage
      .from("blog-audio")
      .upload(fileName, wav, { contentType: "audio/wav", upsert: true });

    if (uploadError) {
      console.error("[generate-blog-audio] Storage upload failed:", uploadError.message);
      return new Response("Failed to upload audio", { status: 500, headers: CORS_HEADERS });
    }
    console.log(`[generate-blog-audio] Uploaded ${fileName} (${Math.round(performance.now() - tUpload)}ms)`);

    const audioUrl = `${supabaseUrl}/storage/v1/object/public/blog-audio/${fileName}?t=${Date.now()}`;

    // ── Persist URL on post row ───────────────────────────────────────────
    const column = lang === "en" ? "audio_url_en" : "audio_url_es";
    const { error: updateError } = await adminClient
      .from("blog_posts")
      .update({ [column]: audioUrl, updated_at: new Date().toISOString() })
      .eq("id", post_id);

    if (updateError) {
      console.error("[generate-blog-audio] Post update failed:", updateError.message);
      return new Response("Audio uploaded but failed to save URL", { status: 500, headers: CORS_HEADERS });
    }

    const totalMs = Math.round(performance.now() - t0);
    console.log(`[generate-blog-audio] Done in ${totalMs}ms — ${audioUrl}`);

    return new Response(
      JSON.stringify({ url: audioUrl, durationSec, wavKb, totalMs }),
      { status: 200, headers: { ...CORS_HEADERS, "Content-Type": "application/json" } },
    );
  } catch (err) {
    console.error("[generate-blog-audio] Unhandled error:", err);
    return new Response("Internal server error", { status: 500, headers: CORS_HEADERS });
  }
});
