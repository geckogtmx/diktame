// newsletter-send — admin-authed broadcast.
// POST {post_id, locale, dry_run?} → fetches confirmed subscribers for the
// locale, renders the post as HTML, batch-sends via Resend, writes one row
// to newsletter_sends. Idempotent via UNIQUE(post_id, locale).
//
// Deployed with verify_jwt=false. The gateway's built-in JWT verifier does
// not accept the new `sb_secret_...` API key format, only the legacy JWT-
// based service_role key; so auth is enforced in-function by comparing the
// Authorization bearer to SUPABASE_SERVICE_ROLE_KEY. Only callers holding
// the service role key (the hqbackstage server-side PATCH/resend routes)
// can reach the handler body.

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const RESEND_API_KEY = Deno.env.get("RESEND_API_KEY");
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const SITE_URL = "https://www.dikta.me";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

type Locale = "en" | "es";
const BATCH_SIZE = 100; // Resend batch limit

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
  });
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

const SERIF_STACK = "Georgia, 'Iowan Old Style', 'Apple Garamond', Baskerville, 'Times New Roman', Times, serif";
const SANS_STACK = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

function applyInlineMarkdown(html: string): string {
  // Bold **text** → <strong>text</strong>. Body already HTML-escaped.
  return html.replace(/\*\*([^*\n]+)\*\*/g, "<strong>$1</strong>");
}

function paragraphsFromBody(body: string): string {
  return body
    .split(/\n\s*\n/)
    .map((p) => p.trim())
    .filter((p) => p.length > 0)
    .map((p) => {
      const inner = applyInlineMarkdown(escapeHtml(p).replace(/\n/g, "<br>"));
      return `<p style="margin:0 0 22px;font-family:${SERIF_STACK};font-size:17px;line-height:1.75;color:#1f2937">${inner}</p>`;
    })
    .join("\n");
}

function formatDate(dateIso: string | null, locale: Locale): string {
  if (!dateIso) return "";
  const d = new Date(dateIso);
  if (Number.isNaN(d.getTime())) return "";
  const lang = locale === "es" ? "es-MX" : "en-US";
  // Full month name, uppercase: "APRIL 17, 2026" / "ABRIL 17, 2026"
  return d.toLocaleDateString(lang, { month: "long", day: "numeric", year: "numeric" }).toUpperCase();
}

// Words-per-minute baseline for silent reading of long-form prose.
const WPM = 225;

function estimateReadMinutes(...parts: (string | null | undefined)[]): number {
  const text = parts.filter((s): s is string => typeof s === "string" && s.length > 0).join(" ");
  if (!text) return 1;
  // Count whitespace-separated tokens; strip markdown bold markers first.
  const words = text.replace(/\*\*/g, "").split(/\s+/).filter(Boolean).length;
  return Math.max(1, Math.round(words / WPM));
}

type Post = {
  id: string;
  slug: string;
  title_en: string;
  title_es: string;
  hook_en: string | null;
  hook_es: string | null;
  body_en: string;
  body_es: string;
  closing_en: string | null;
  closing_es: string | null;
  image_url_en: string | null;
  image_url_es: string | null;
  audio_url_en: string | null;
  audio_url_es: string | null;
  published_at: string | null;
};

function renderPostEmail(
  post: Post,
  locale: Locale,
  unsubscribeUrl: string,
  preferencesUrl: string,
): { subject: string; html: string } {
  const title = locale === "es" ? post.title_es : post.title_en;
  const hook = locale === "es" ? post.hook_es : post.hook_en;
  const body = locale === "es" ? post.body_es : post.body_en;
  const closing = locale === "es" ? post.closing_es : post.closing_en;
  const imageUrl = locale === "es" ? post.image_url_es : post.image_url_en;
  const audioUrl = locale === "es" ? post.audio_url_es : post.audio_url_en;
  const postUrl = `${SITE_URL}/${locale}/blog/${post.slug}`;

  const labels = locale === "es"
    ? {
        listen: "Escuchar el episodio",
        readOnSite: "Leer en el sitio →",
        manage: "Gestionar preferencias",
        unsubscribe: "Cancelar suscripción",
      }
    : {
        listen: "Listen to the episode",
        readOnSite: "Read on the site →",
        manage: "Manage preferences",
        unsubscribe: "Unsubscribe",
      };

  // Byline: DATE · N MIN READ · AUDIO AVAILABLE ✓ (last segment conditional).
  // Author attribution is intentionally omitted — the literary "voice" is a
  // stylistic register for the prose, not an authorship claim.
  const dateStr = formatDate(post.published_at, locale);
  const readMin = estimateReadMinutes(body, hook, closing);
  const readLabel = locale === "es" ? `${readMin} MIN DE LECTURA` : `${readMin} MIN READ`;
  const audioLabel = locale === "es" ? "AUDIO DISPONIBLE \u2713" : "AUDIO AVAILABLE \u2713";
  const bylineBits: string[] = [];
  if (dateStr) bylineBits.push(dateStr);
  bylineBits.push(readLabel);
  if (audioUrl) bylineBits.push(audioLabel);
  const bylineBlock = `<div style="font-family:${SANS_STACK};font-size:12px;letter-spacing:0.08em;text-transform:uppercase;color:#525866;margin:0 0 24px">${bylineBits.map(escapeHtml).join(" &middot; ")}</div>`;

  const heroImg = imageUrl
    ? `<img src="${escapeHtml(imageUrl)}" alt="" style="width:100%;max-width:620px;height:auto;margin:0 0 32px;display:block;border:0;outline:none;text-decoration:none">`
    : "";

  const audioBlock = audioUrl
    ? `<div style="margin:0 0 32px"><a href="${escapeHtml(audioUrl)}" style="display:inline-block;background:#111827;color:#ffffff;padding:10px 20px;border-radius:999px;text-decoration:none;font-family:${SANS_STACK};font-size:14px;font-weight:600"><span style="display:inline-block;margin-right:8px">&#9654;</span>${labels.listen}</a></div>`
    : "";

  // Pull-quote styling: left accent border, indent, italic serif. Used for
  // both the hook (at the top, before the body) and the closing meditation
  // (at the bottom, after the body).
  const hookBlock = hook
    ? `<blockquote style="margin:0 0 24px;padding:4px 0 4px 20px;border-left:3px solid #cbd5e1;font-family:${SERIF_STACK};font-size:18px;line-height:1.6;color:#475569;font-style:italic">${escapeHtml(hook)}</blockquote>`
    : "";

  const closingBlock = closing
    ? `<blockquote style="margin:32px 0 0;padding:4px 0 4px 20px;border-left:3px solid #cbd5e1;font-family:${SERIF_STACK};font-size:17px;line-height:1.7;color:#475569;font-style:italic">${escapeHtml(closing)}</blockquote>`
    : "";

  const html = `<!DOCTYPE html>
<html lang="${locale}"><head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="light">
<meta name="supported-color-schemes" content="light">
<title>${escapeHtml(title)}</title>
</head>
<body style="margin:0;padding:0;background-color:#ffffff;color:#1a1a1a;-webkit-font-smoothing:antialiased">
<div style="background-color:#ffffff;padding:32px 16px">
<table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="max-width:620px;width:100%;margin:0 auto;background-color:#ffffff">
<tr><td style="padding:0 8px">

<div style="text-align:center;padding:16px 0 32px">
<span style="font-family:${SANS_STACK};font-size:22px;font-weight:700;color:#0f172a;letter-spacing:-0.01em">dIKta<span style="color:#2563eb">.</span>me</span>
</div>

<h1 style="font-family:${SANS_STACK};font-size:32px;font-weight:800;color:#0f172a;letter-spacing:-0.02em;line-height:1.15;margin:0 0 20px">${escapeHtml(title)}</h1>

${hookBlock}

${bylineBlock}

<div style="height:1px;background:#e5e7eb;margin:0 0 28px;line-height:1px;font-size:1px">&nbsp;</div>

${audioBlock}

${heroImg}

${paragraphsFromBody(body)}

${closingBlock}

<div style="margin:32px 0 0"><a href="${postUrl}" style="font-family:${SANS_STACK};color:#2563eb;text-decoration:none;font-weight:600;font-size:15px">${labels.readOnSite}</a></div>

<div style="margin:48px 0 0;padding:24px 0 0;border-top:1px solid #e5e7eb;text-align:center;font-family:${SANS_STACK};font-size:12px;color:#64748b;line-height:1.7">
<a href="${preferencesUrl}" style="color:#525866;text-decoration:underline">${labels.manage}</a>
&nbsp;&middot;&nbsp;
<a href="${unsubscribeUrl}" style="color:#525866;text-decoration:underline">${labels.unsubscribe}</a>
<br><br>
San Francisco 1826-C-101, Del Valle, 03100, CDMX, México
</div>

</td></tr>
</table>
</div>
</body></html>`;

  return { subject: title, html };
}

// Constant-time string equality to blunt timing attacks on the bearer check.
function safeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: CORS_HEADERS });
  }
  if (req.method !== "POST") {
    return json({ error: "Method not allowed" }, 405);
  }
  if (!RESEND_API_KEY || !SUPABASE_URL || !SERVICE_ROLE_KEY) {
    return json({ error: "Server misconfigured" }, 500);
  }

  // In-function auth: replaces gateway verify_jwt (incompatible with sb_secret_ keys).
  const authHeader = req.headers.get("authorization") ?? req.headers.get("Authorization") ?? "";
  const bearer = authHeader.startsWith("Bearer ") ? authHeader.slice(7) : "";
  if (!bearer || !safeEqual(bearer, SERVICE_ROLE_KEY)) {
    return json({ error: "Unauthorized" }, 401);
  }

  let payload: { post_id?: string; locale?: string; dry_run?: boolean } = {};
  try {
    payload = await req.json();
  } catch {
    return json({ error: "Invalid JSON" }, 400);
  }

  const postId = payload.post_id;
  const locale = payload.locale as Locale;
  const dryRun = payload.dry_run === true;

  if (!postId) return json({ error: "post_id required" }, 400);
  if (locale !== "en" && locale !== "es") return json({ error: "locale must be 'en' or 'es'" }, 400);

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY, { auth: { persistSession: false } });
  const FUNCTIONS_URL = `${SUPABASE_URL}/functions/v1`;

  // Idempotency check
  const { data: existingSend, error: existingErr } = await supabase
    .from("newsletter_sends")
    .select("id, status, subscriber_count, completed_at")
    .eq("post_id", postId)
    .eq("locale", locale)
    .maybeSingle();

  if (existingErr) {
    console.error("Existing send lookup error:", existingErr);
    return json({ error: "Database error" }, 500);
  }
  if (existingSend) {
    return json({
      status: "already_sent",
      send_id: existingSend.id,
      previous_status: existingSend.status,
      subscriber_count: existingSend.subscriber_count,
      completed_at: existingSend.completed_at,
    }, 200);
  }

  // Fetch post
  const { data: post, error: postErr } = await supabase
    .from("blog_posts")
    .select("id, slug, title_en, title_es, hook_en, hook_es, body_en, body_es, closing_en, closing_es, image_url_en, image_url_es, audio_url_en, audio_url_es, published_at, status")
    .eq("id", postId)
    .maybeSingle();

  if (postErr) {
    console.error("Post fetch error:", postErr);
    return json({ error: "Database error" }, 500);
  }
  if (!post) return json({ error: "Post not found" }, 404);

  const title = locale === "es" ? post.title_es : post.title_en;
  const body = locale === "es" ? post.body_es : post.body_en;
  if (!title || !body) {
    return json({ error: `Post missing title_${locale} or body_${locale}` }, 400);
  }

  // Fetch confirmed subscribers for this locale
  const { data: subscribers, error: subsErr } = await supabase
    .from("newsletter_subscribers")
    .select("id, email, unsubscribe_token")
    .eq("locale", locale)
    .eq("status", "confirmed");

  if (subsErr) {
    console.error("Subscribers fetch error:", subsErr);
    return json({ error: "Database error" }, 500);
  }

  const subCount = subscribers?.length ?? 0;

  // Create send row (queued → sending)
  const { data: sendRow, error: insertErr } = await supabase
    .from("newsletter_sends")
    .insert({
      post_id: postId,
      locale,
      subscriber_count: subCount,
      status: dryRun ? "done" : "sending",
      started_at: new Date().toISOString(),
      completed_at: dryRun ? new Date().toISOString() : null,
    })
    .select("id")
    .single();

  if (insertErr || !sendRow) {
    console.error("Insert send row error:", insertErr);
    return json({ error: "Database error (could be duplicate send)" }, 500);
  }

  if (dryRun) {
    return json({
      status: "dry_run",
      send_id: sendRow.id,
      subscriber_count: subCount,
    }, 200);
  }

  if (subCount === 0) {
    await supabase
      .from("newsletter_sends")
      .update({ status: "done", completed_at: new Date().toISOString() })
      .eq("id", sendRow.id);
    return json({ status: "done", send_id: sendRow.id, subscriber_count: 0 }, 200);
  }

  // Build per-recipient emails with individualized unsubscribe URLs
  const emailPayloads = subscribers!.map((sub) => {
    // Links point at Next.js landing pages, not edge functions — better UX.
    const unsubscribeUrl = `${SITE_URL}/${locale}/newsletter/unsubscribe/${sub.unsubscribe_token}`;
    const preferencesUrl = `${SITE_URL}/${locale}/newsletter/preferences/${sub.unsubscribe_token}`;
    const { subject, html } = renderPostEmail(post as Post, locale, unsubscribeUrl, preferencesUrl);
    return {
      from: "dIKta.me <newsletter@dikta.me>",
      reply_to: "newsletter@dikta.me",
      to: [sub.email],
      subject,
      html,
      headers: {
        "List-Unsubscribe": `<${unsubscribeUrl}>`,
        "List-Unsubscribe-Post": "List-Unsubscribe=One-Click",
      },
    };
  });

  // Batch-send via Resend batch endpoint
  const batchIds: string[] = [];
  const errors: string[] = [];
  for (let i = 0; i < emailPayloads.length; i += BATCH_SIZE) {
    const chunk = emailPayloads.slice(i, i + BATCH_SIZE);
    const res = await fetch("https://api.resend.com/emails/batch", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${RESEND_API_KEY}`,
      },
      body: JSON.stringify(chunk),
    });
    const data = await res.json();
    if (!res.ok) {
      console.error("Resend batch error:", res.status, data);
      errors.push(`batch ${i / BATCH_SIZE}: ${JSON.stringify(data)}`);
      continue;
    }
    // Resend batch returns { data: [{ id }, ...] }
    if (Array.isArray(data.data)) {
      for (const item of data.data) {
        if (item?.id) batchIds.push(item.id);
      }
    } else if (data.id) {
      batchIds.push(data.id);
    }
  }

  const finalStatus = errors.length === 0
    ? "done"
    : batchIds.length > 0 ? "partial" : "failed";

  await supabase
    .from("newsletter_sends")
    .update({
      status: finalStatus,
      resend_batch_ids: batchIds,
      completed_at: new Date().toISOString(),
      error: errors.length > 0 ? errors.join("; ").slice(0, 2000) : null,
    })
    .eq("id", sendRow.id);

  return json({
    status: finalStatus,
    send_id: sendRow.id,
    subscriber_count: subCount,
    batch_count: batchIds.length,
    errors: errors.length > 0 ? errors : undefined,
  }, 200);
});
