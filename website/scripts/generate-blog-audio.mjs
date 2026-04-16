#!/usr/bin/env node
// generate-blog-audio.mjs
//
// Generates MP3 audio for blog posts using Gemini 3.1 Flash TTS, uploads to
// Cloudflare R2, and stores the public URL on the post row.
//
// Chunking strategy: each section is its own Gemini TTS call to preserve
// voice quality over long content. Chunks are: title, hook, each body
// paragraph, closing. PCM outputs are concatenated, then encoded to MP3 via
// ffmpeg, then uploaded.
//
// Usage:
//   node --env-file=.env.local scripts/generate-blog-audio.mjs
//   node --env-file=.env.local scripts/generate-blog-audio.mjs --post-id=<uuid>
//   node --env-file=.env.local scripts/generate-blog-audio.mjs --post-id=<uuid> --lang=en
//   node --env-file=.env.local scripts/generate-blog-audio.mjs --force
//   node --env-file=.env.local scripts/generate-blog-audio.mjs --yes   # skip confirmation

import { createClient } from '@supabase/supabase-js';
import { S3Client, PutObjectCommand } from '@aws-sdk/client-s3';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline/promises';
import { stdin, stdout } from 'node:process';

// ── Config ───────────────────────────────────────────────────────────────────
const GEMINI_TTS_MODEL = 'gemini-3.1-flash-tts-preview';
const GEMINI_TTS_URL = `https://generativelanguage.googleapis.com/v1beta/models/${GEMINI_TTS_MODEL}:generateContent`;
const TTS_VOICE = 'Kore';
const SAMPLE_RATE = 24_000;
const BITS_PER_SAMPLE = 16;
const CHANNELS = 1;
const MP3_BITRATE = '64k';

// ── Env ──────────────────────────────────────────────────────────────────────
const ENV = {
  NEXT_PUBLIC_SUPABASE_URL: process.env.NEXT_PUBLIC_SUPABASE_URL,
  SUPABASE_SERVICE_ROLE_KEY: process.env.SUPABASE_SERVICE_ROLE_KEY,
  GEMINI_API_KEY: process.env.GEMINI_API_KEY,
  R2_ACCOUNT_ID: process.env.R2_ACCOUNT_ID,
  R2_ACCESS_KEY_ID: process.env.R2_ACCESS_KEY_ID,
  R2_SECRET_ACCESS_KEY: process.env.R2_SECRET_ACCESS_KEY,
  R2_BUCKET: process.env.R2_BUCKET,
  R2_PUBLIC_BASE_URL: process.env.R2_PUBLIC_BASE_URL,
};
for (const [k, v] of Object.entries(ENV)) {
  if (!v) {
    console.error(`Missing env var: ${k}`);
    console.error('Run with: node --env-file=.env.local scripts/generate-blog-audio.mjs');
    process.exit(1);
  }
}

// ── CLI args ─────────────────────────────────────────────────────────────────
function parseArgs() {
  const args = { postId: null, lang: null, force: false, yes: false };
  for (const arg of process.argv.slice(2)) {
    if (arg.startsWith('--post-id=')) args.postId = arg.slice('--post-id='.length);
    else if (arg.startsWith('--lang=')) args.lang = arg.slice('--lang='.length);
    else if (arg === '--force') args.force = true;
    else if (arg === '--yes' || arg === '-y') args.yes = true;
    else if (arg === '--help' || arg === '-h') {
      console.log('Usage: node --env-file=.env.local scripts/generate-blog-audio.mjs [options]');
      console.log('  --post-id=<uuid>  Process a single post (default: all pending)');
      console.log('  --lang=en|es      Limit to one language (default: both)');
      console.log('  --force           Regenerate even if audio already exists');
      console.log('  --yes, -y         Skip interactive confirmation');
      process.exit(0);
    }
    else { console.error(`Unknown arg: ${arg}`); process.exit(1); }
  }
  if (args.lang && args.lang !== 'en' && args.lang !== 'es') {
    console.error(`Invalid --lang: must be "en" or "es"`);
    process.exit(1);
  }
  return args;
}

// ── Text processing ──────────────────────────────────────────────────────────
function stripMarkdown(text) {
  return text
    .replace(/```[\s\S]*?```/g, '')
    .replace(/^#{1,6}\s+/gm, '')
    .replace(/\*\*(.+?)\*\*/g, '$1')
    .replace(/\*(.+?)\*/g, '$1')
    .replace(/__(.+?)__/g, '$1')
    .replace(/_(.+?)_/g, '$1')
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    .replace(/^>\s*/gm, '')
    .replace(/^[-*+]\s+/gm, '')
    .replace(/^\d+\.\s+/gm, '')
    .replace(/^---+$/gm, '')
    .replace(/`([^`]+)`/g, '$1')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

// Splits the post into one Gemini TTS call per section. All chunks go bare —
// no style prefixes — because prefixes were unreliable (EN prefixes were read
// aloud, ES closing prefix caused dialect drift). Body paragraphs are further
// split in half at a sentence boundary to keep Gemini's in-chunk speed drift
// from accumulating over long passages.
function splitInHalf(paragraph) {
  const sentences = paragraph.split(/(?<=[.!?…])\s+/).filter(Boolean);
  if (sentences.length < 2) return [paragraph];
  const halfLen = paragraph.length / 2;
  let acc = 0;
  let splitAt = 1;
  for (let i = 0; i < sentences.length - 1; i++) {
    acc += sentences[i].length + 1; // +1 for the space
    splitAt = i + 1;
    if (acc >= halfLen) break;
  }
  const first = sentences.slice(0, splitAt).join(' ').trim();
  const second = sentences.slice(splitAt).join(' ').trim();
  if (!first || !second) return [paragraph];
  return [first, second];
}

function buildChunks(title, hook, body, closing) {
  const chunks = [];
  chunks.push({ label: 'title', text: title.trim() });
  if (hook?.trim()) {
    chunks.push({ label: 'hook', text: stripMarkdown(hook.trim()) });
  }
  const paragraphs = stripMarkdown(body).split(/\n{2,}/).map(s => s.trim()).filter(Boolean);
  paragraphs.forEach((p, i) => {
    const halves = splitInHalf(p);
    halves.forEach((h, j) => {
      const suffix = halves.length > 1 ? `.${j + 1}` : '';
      chunks.push({ label: `body-${i + 1}${suffix}`, text: h });
    });
  });
  if (closing?.trim()) {
    chunks.push({ label: 'closing', text: stripMarkdown(closing.trim()) });
  }
  return chunks;
}

// ── Gemini TTS ───────────────────────────────────────────────────────────────
async function callGeminiTts(text) {
  const res = await fetch(GEMINI_TTS_URL, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-goog-api-key': ENV.GEMINI_API_KEY,
    },
    body: JSON.stringify({
      contents: [{ parts: [{ text }] }],
      generationConfig: {
        responseModalities: ['AUDIO'],
        speechConfig: {
          voiceConfig: { prebuiltVoiceConfig: { voiceName: TTS_VOICE } },
        },
      },
    }),
  });
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`Gemini TTS ${res.status}: ${body.slice(0, 300)}`);
  }
  const data = await res.json();
  const base64 = data?.candidates?.[0]?.content?.parts?.[0]?.inlineData?.data;
  if (!base64) {
    const snippet = JSON.stringify(data).slice(0, 500);
    throw new Error(`No audio in Gemini response. Body: ${snippet}`);
  }
  return Buffer.from(base64, 'base64');
}

// ── ffmpeg MP3 encode ────────────────────────────────────────────────────────
function encodeMp3(pcmBuffer) {
  return new Promise((resolve, reject) => {
    const ff = spawn('ffmpeg', [
      '-hide_banner', '-loglevel', 'error',
      '-f', 's16le',
      '-ar', String(SAMPLE_RATE),
      '-ac', String(CHANNELS),
      '-i', 'pipe:0',
      '-b:a', MP3_BITRATE,
      '-f', 'mp3',
      'pipe:1',
    ], { stdio: ['pipe', 'pipe', 'pipe'] });

    const out = [];
    const err = [];
    ff.stdout.on('data', c => out.push(c));
    ff.stderr.on('data', c => err.push(c));
    ff.on('error', reject);
    ff.on('close', code => {
      if (code === 0) resolve(Buffer.concat(out));
      else reject(new Error(`ffmpeg exited ${code}: ${Buffer.concat(err).toString().slice(-500)}`));
    });
    ff.stdin.end(pcmBuffer);
  });
}

// ── R2 upload ────────────────────────────────────────────────────────────────
const s3 = new S3Client({
  region: 'auto',
  endpoint: `https://${ENV.R2_ACCOUNT_ID}.r2.cloudflarestorage.com`,
  credentials: {
    accessKeyId: ENV.R2_ACCESS_KEY_ID,
    secretAccessKey: ENV.R2_SECRET_ACCESS_KEY,
  },
});

async function uploadToR2(key, body, contentType) {
  await s3.send(new PutObjectCommand({
    Bucket: ENV.R2_BUCKET,
    Key: key,
    Body: body,
    ContentType: contentType,
    CacheControl: 'public, max-age=31536000, immutable',
  }));
  return `${ENV.R2_PUBLIC_BASE_URL}/${key}`;
}

// ── Supabase ─────────────────────────────────────────────────────────────────
const supabase = createClient(ENV.NEXT_PUBLIC_SUPABASE_URL, ENV.SUPABASE_SERVICE_ROLE_KEY, {
  auth: { persistSession: false },
});

const POST_COLUMNS = 'id, slug, title_en, hook_en, body_en, closing_en, title_es, hook_es, body_es, closing_es, audio_url_en, audio_url_es, published_at, status';

async function fetchPost(id) {
  const { data, error } = await supabase
    .from('blog_posts')
    .select(POST_COLUMNS)
    .eq('id', id)
    .single();
  if (error) throw new Error(`Fetch post ${id}: ${error.message}`);
  return data;
}

async function findPending(force) {
  let query = supabase
    .from('blog_posts')
    .select(POST_COLUMNS)
    .eq('status', 'published')
    .order('published_at', { ascending: false });
  if (!force) query = query.or('audio_url_en.is.null,audio_url_es.is.null');
  const { data, error } = await query;
  if (error) throw new Error(`Query pending: ${error.message}`);
  return data || [];
}

async function saveAudioUrl(postId, lang, url) {
  const column = lang === 'en' ? 'audio_url_en' : 'audio_url_es';
  const { error } = await supabase
    .from('blog_posts')
    .update({ [column]: url, updated_at: new Date().toISOString() })
    .eq('id', postId);
  if (error) throw new Error(`Update post ${postId} ${column}: ${error.message}`);
}

// ── Per-post processing ──────────────────────────────────────────────────────
async function processPostLang(post, lang) {
  const title   = lang === 'en' ? post.title_en   : post.title_es;
  const hook    = lang === 'en' ? post.hook_en    : post.hook_es;
  const body    = lang === 'en' ? post.body_en    : post.body_es;
  const closing = lang === 'en' ? post.closing_en : post.closing_es;

  if (!title || !body) {
    throw new Error(`Missing ${lang.toUpperCase()} content`);
  }

  const chunks = buildChunks(title, hook, body, closing);
  const start = Date.now();
  const pcmParts = [];

  for (let i = 0; i < chunks.length; i++) {
    const c = chunks[i];
    const t = Date.now();
    const pcm = await callGeminiTts(c.text);
    console.log(`    ${i + 1}/${chunks.length} ${c.label}: ${pcm.length} bytes (${Date.now() - t}ms)`);
    pcmParts.push(pcm);
  }

  const pcmTotal = Buffer.concat(pcmParts);
  const durationSec = Math.round(pcmTotal.length / 2 / SAMPLE_RATE);

  const tEncode = Date.now();
  const mp3 = await encodeMp3(pcmTotal);
  console.log(`    mp3: ${Math.round(mp3.length / 1024)}KB (${Date.now() - tEncode}ms encode)`);

  const tUpload = Date.now();
  const key = `${post.slug}-${lang}.mp3`;
  const url = await uploadToR2(key, mp3, 'audio/mpeg');
  console.log(`    uploaded (${Date.now() - tUpload}ms)`);

  await saveAudioUrl(post.id, lang, url);

  return { url, sizeKb: Math.round(mp3.length / 1024), durationSec, totalMs: Date.now() - start };
}

// ── Main ─────────────────────────────────────────────────────────────────────
async function main() {
  const args = parseArgs();

  const posts = args.postId
    ? [await fetchPost(args.postId)]
    : await findPending(args.force);

  if (posts.length === 0) {
    console.log('No pending posts found.');
    return;
  }

  // Build work list: each entry is one post × one language.
  const work = [];
  for (const post of posts) {
    for (const lang of ['en', 'es']) {
      if (args.lang && args.lang !== lang) continue;
      const existing = lang === 'en' ? post.audio_url_en : post.audio_url_es;
      const title = lang === 'en' ? post.title_en : post.title_es;
      const body = lang === 'en' ? post.body_en : post.body_es;
      if (!title || !body) continue;
      if (existing && !args.force) continue;
      work.push({ post, lang });
    }
  }

  if (work.length === 0) {
    console.log('Nothing to do — all applicable posts already have audio. Use --force to regenerate.');
    return;
  }

  // Show plan.
  console.log(`\nPending audio generation:`);
  for (const { post, lang } of work) {
    const existing = lang === 'en' ? post.audio_url_en : post.audio_url_es;
    const mark = existing ? '↻' : '+';
    console.log(`  ${mark} ${post.slug} (${lang})`);
  }
  console.log(`\nTotal: ${work.length} file(s) · voice=${TTS_VOICE} · ${MP3_BITRATE} MP3`);
  console.log(`Estimated: ~${work.length * 2}-${work.length * 4} min\n`);

  if (!args.yes) {
    const rl = createInterface({ input: stdin, output: stdout });
    const ans = (await rl.question('Proceed? [y/N] ')).trim().toLowerCase();
    rl.close();
    if (ans !== 'y' && ans !== 'yes') {
      console.log('Aborted.');
      return;
    }
  }

  // Process sequentially. Failures are skipped and reported at the end.
  const results = { ok: [], failed: [] };
  const globalStart = Date.now();

  for (let i = 0; i < work.length; i++) {
    const { post, lang } = work[i];
    console.log(`\n[${i + 1}/${work.length}] ${post.slug} (${lang})`);
    try {
      const r = await processPostLang(post, lang);
      console.log(`  ok: ${r.sizeKb}KB, ${r.durationSec}s audio, ${Math.round(r.totalMs / 1000)}s wall`);
      console.log(`     ${r.url}`);
      results.ok.push({ slug: post.slug, lang, ...r });
    } catch (err) {
      console.error(`  FAILED: ${err.message}`);
      results.failed.push({ slug: post.slug, lang, error: err.message });
    }
  }

  const totalSec = Math.round((Date.now() - globalStart) / 1000);
  const totalKb = results.ok.reduce((s, r) => s + r.sizeKb, 0);
  console.log(`\n── Summary ──`);
  console.log(`  succeeded: ${results.ok.length} (${totalKb}KB total)`);
  if (results.failed.length) {
    console.log(`  failed:    ${results.failed.length}`);
    for (const f of results.failed) console.log(`    ${f.slug} (${f.lang}): ${f.error}`);
  }
  console.log(`  time:      ${Math.floor(totalSec / 60)}m ${totalSec % 60}s`);

  if (results.failed.length) process.exit(1);
}

main().catch(err => {
  console.error('Fatal:', err);
  process.exit(1);
});
