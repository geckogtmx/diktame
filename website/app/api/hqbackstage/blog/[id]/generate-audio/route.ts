import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

// Allow up to 60s — Gemini TTS for a full article takes 15–30s
export const maxDuration = 60;

const GEMINI_TTS_MODEL = 'gemini-3.1-flash-tts-preview';
const GEMINI_TTS_URL = `https://generativelanguage.googleapis.com/v1beta/models/${GEMINI_TTS_MODEL}:generateContent`;
const TTS_VOICE = 'Kore';
const SAMPLE_RATE = 24_000;
const BITS_PER_SAMPLE = 16;
const CHANNELS = 1;

/**
 * Strips Markdown syntax from body/closing text before sending to TTS.
 * No LLM pass — just regex cleanup so the model doesn't read "**bold**" aloud.
 */
function stripMarkdown(text: string): string {
  return text
    .replace(/```[\s\S]*?```/g, '')          // fenced code blocks
    .replace(/^#{1,6}\s+/gm, '')             // headings
    .replace(/\*\*(.+?)\*\*/g, '$1')         // **bold**
    .replace(/\*(.+?)\*/g, '$1')             // *italic*
    .replace(/__(.+?)__/g, '$1')             // __bold__
    .replace(/_(.+?)_/g, '$1')               // _italic_
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1') // [link](url) → text
    .replace(/^>\s*/gm, '')                  // blockquotes
    .replace(/^[-*+]\s+/gm, '')              // bullet lists
    .replace(/^\d+\.\s+/gm, '')              // numbered lists
    .replace(/^---+$/gm, '')                 // horizontal rules
    .replace(/`([^`]+)`/g, '$1')             // `inline code`
    .replace(/\n{3,}/g, '\n\n')             // normalise blank lines
    .trim();
}

/**
 * Assembles the final TTS script with expressive tags applied per section:
 *  - Title:   [excited]    — headline energy
 *  - Hook:    [curious]    — draws the listener in
 *  - Body:    (no tag)     — flat, neutral news delivery
 *  - Closing: [thoughtful] — reflective landing
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
  return parts.join('\n\n');
}

/**
 * Wraps raw Gemini PCM output (24 kHz, 16-bit, mono) in a standard WAV container.
 * Browsers cannot play raw PCM — the 44-byte header is required.
 */
function buildWav(pcm: Buffer): Buffer {
  const dataSize = pcm.length;
  const byteRate = SAMPLE_RATE * CHANNELS * (BITS_PER_SAMPLE / 8);
  const blockAlign = CHANNELS * (BITS_PER_SAMPLE / 8);
  const header = Buffer.alloc(44);

  header.write('RIFF', 0, 'ascii');
  header.writeUInt32LE(36 + dataSize, 4);
  header.write('WAVE', 8, 'ascii');
  header.write('fmt ', 12, 'ascii');
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);                // PCM format
  header.writeUInt16LE(CHANNELS, 22);
  header.writeUInt32LE(SAMPLE_RATE, 24);
  header.writeUInt32LE(byteRate, 28);
  header.writeUInt16LE(blockAlign, 32);
  header.writeUInt16LE(BITS_PER_SAMPLE, 34);
  header.write('data', 36, 'ascii');
  header.writeUInt32LE(dataSize, 40);

  return Buffer.concat([header, pcm]);
}

export async function POST(
  request: Request,
  { params }: { params: Promise<{ id: string }> },
) {
  try {
    const { supabase } = await requireAdmin(request);
    const { id } = await params;

    const body = await request.json() as { lang?: string };
    const lang = body.lang;
    if (lang !== 'en' && lang !== 'es') {
      return NextResponse.json({ error: 'lang must be "en" or "es"' }, { status: 400 });
    }

    const apiKey = process.env.GEMINI_API_KEY;
    if (!apiKey) {
      return NextResponse.json({ error: 'GEMINI_API_KEY not configured' }, { status: 500 });
    }

    // Fetch just the content columns we need
    const { data: post, error: fetchError } = await supabase
      .from('blog_posts')
      .select('slug, title_en, hook_en, body_en, closing_en, title_es, hook_es, body_es, closing_es')
      .eq('id', id)
      .single();

    if (fetchError || !post) {
      return NextResponse.json({ error: 'Post not found' }, { status: 404 });
    }

    // Prevent path traversal in storage file name
    if (!/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/.test(post.slug)) {
      return NextResponse.json({ error: 'Post has invalid slug format' }, { status: 400 });
    }

    const title   = lang === 'en' ? post.title_en   : post.title_es;
    const hook    = lang === 'en' ? post.hook_en    : post.hook_es;
    const bodyTxt = lang === 'en' ? post.body_en    : post.body_es;
    const closing = lang === 'en' ? post.closing_en : post.closing_es;

    if (!title || !bodyTxt) {
      return NextResponse.json(
        { error: `Missing ${lang.toUpperCase()} content (title and body required)` },
        { status: 400 },
      );
    }

    const ttsScript = buildTtsScript(title, hook, bodyTxt, closing);

    // ── Call Gemini 3.1 Flash TTS ────────────────────────────────────────────
    const geminiRes = await fetch(GEMINI_TTS_URL, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-goog-api-key': apiKey,
      },
      body: JSON.stringify({
        contents: [{ parts: [{ text: ttsScript }] }],
        generationConfig: {
          responseModalities: ['AUDIO'],
          speechConfig: {
            voiceConfig: {
              prebuiltVoiceConfig: { voiceName: TTS_VOICE },
            },
          },
        },
      }),
    });

    if (!geminiRes.ok) {
      const errBody = await geminiRes.text();
      console.error('Gemini TTS error:', geminiRes.status, errBody.slice(0, 300));
      return NextResponse.json(
        { error: `Gemini TTS returned ${geminiRes.status}` },
        { status: 502 },
      );
    }

    const geminiData = await geminiRes.json() as {
      candidates?: { content?: { parts?: { inlineData?: { data?: string } }[] } }[];
    };
    const base64Audio = geminiData?.candidates?.[0]?.content?.parts?.[0]?.inlineData?.data;
    if (!base64Audio) {
      console.error('Gemini TTS: no audio data in response', JSON.stringify(geminiData).slice(0, 300));
      return NextResponse.json({ error: 'No audio data in Gemini response' }, { status: 502 });
    }

    // ── Wrap PCM in WAV and upload ────────────────────────────────────────────
    const pcm = Buffer.from(base64Audio, 'base64');
    const wav = buildWav(pcm);

    const fileName = `${post.slug}-${lang}.wav`;
    const { error: uploadError } = await supabase.storage
      .from('blog-audio')
      .upload(fileName, wav, { contentType: 'audio/wav', upsert: true });

    if (uploadError) {
      console.error('Blog audio upload error:', uploadError);
      return NextResponse.json({ error: 'Failed to upload audio to storage' }, { status: 500 });
    }

    const audioUrl = `${process.env.NEXT_PUBLIC_SUPABASE_URL}/storage/v1/object/public/blog-audio/${fileName}?t=${Date.now()}`;

    // ── Persist URL on the post row ───────────────────────────────────────────
    const column = lang === 'en' ? 'audio_url_en' : 'audio_url_es';
    const { error: updateError } = await supabase
      .from('blog_posts')
      .update({ [column]: audioUrl, updated_at: new Date().toISOString() })
      .eq('id', id);

    if (updateError) {
      console.error('Blog post audio URL update error:', updateError);
      return NextResponse.json({ error: 'Audio uploaded but failed to save URL to post' }, { status: 500 });
    }

    console.log(`Blog audio generated: ${fileName} (${Math.round(wav.length / 1024)}KB)`);
    return NextResponse.json({ url: audioUrl });
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Generate audio POST error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
