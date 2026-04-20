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

function paragraphsFromBody(body: string): string {
  return body
    .split(/\n\s*\n/)
    .map((p) => p.trim())
    .filter((p) => p.length > 0)
    .map((p) => `<p>${escapeHtml(p).replace(/\n/g, "<br>")}</p>`)
    .join("\n");
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
        readOnSite: "Leer en el sitio",
        manage: "Gestionar preferencias",
        unsubscribe: "Cancelar suscripción",
      }
    : {
        listen: "Listen to the episode",
        readOnSite: "Read on the site",
        manage: "Manage preferences",
        unsubscribe: "Unsubscribe",
      };

  const heroImg = imageUrl
    ? `<img src="${escapeHtml(imageUrl)}" alt="" style="width:100%;max-width:600px;height:auto;border-radius:12px;margin:0 0 28px;display:block">`
    : "";

  const audioBlock = audioUrl
    ? `<div style="margin:28px 0"><a href="${escapeHtml(audioUrl)}" style="display:inline-block;background:#2563eb;color:#fff;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold">▶ ${labels.listen}</a></div>`
    : "";

  const hookBlock = hook
    ? `<p style="font-size:18px;line-height:1.6;color:#cbd5e1;margin:0 0 24px;font-style:italic">${escapeHtml(hook)}</p>`
    : "";

  const closingBlock = closing
    ? `<p style="font-size:16px;line-height:1.6;color:#94a3b8;margin:24px 0 0;font-style:italic">${escapeHtml(closing)}</p>`
    : "";

  const html = `<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>${escapeHtml(title)}</title><style>
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;background:#020617;color:#fff;margin:0;padding:40px 16px}
.c{max-width:600px;margin:0 auto;background:#0f172a;border:1px solid #1e293b;border-radius:16px;padding:40px}
.logo{font-size:20px;font-weight:bold;margin:0 0 28px}
h1{font-size:28px;font-weight:800;margin:0 0 16px;letter-spacing:-0.02em;line-height:1.2}
p{font-size:16px;line-height:1.7;color:#e2e8f0;margin:0 0 20px}
.foot{margin-top:40px;padding-top:24px;border-top:1px solid #1e293b;font-size:12px;color:#64748b;line-height:1.6}
.foot a{color:#94a3b8}
</style></head><body><div class="c">
<div class="logo">dIKta<span style="color:#2563eb">.</span>me</div>
${heroImg}
<h1>${escapeHtml(title)}</h1>
${hookBlock}
${audioBlock}
${paragraphsFromBody(body)}
${closingBlock}
<div style="margin:32px 0 0"><a href="${postUrl}" style="color:#60a5fa;text-decoration:none;font-weight:600">${labels.readOnSite} →</a></div>
<div class="foot">
<a href="${preferencesUrl}">${labels.manage}</a> · <a href="${unsubscribeUrl}">${labels.unsubscribe}</a><br><br>
San Francisco 1826-C-101, Del Valle, 03100, CDMX, México
</div>
</div></body></html>`;

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
    .select("id, slug, title_en, title_es, hook_en, hook_es, body_en, body_es, closing_en, closing_es, image_url_en, image_url_es, audio_url_en, audio_url_es, status")
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
