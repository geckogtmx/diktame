// newsletter-unsubscribe — public endpoint.
// Supports both GET (user clicks link in email) and POST (Gmail one-click
// via List-Unsubscribe-Post). Either way: flips status=unsubscribed on the
// row matching the unsubscribe_token.

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const SITE_URL = "https://www.dikta.me";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type, list-unsubscribe",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
};

function landingPage(locale: "en" | "es", kind: "success" | "invalid") {
  const copy = {
    en: {
      success: { h: "You've been unsubscribed", p: "Sorry to see you go. You will no longer receive emails from dIKta.me." },
      invalid: { h: "Invalid link", p: "This unsubscribe link is no longer valid." },
    },
    es: {
      success: { h: "Te has dado de baja", p: "Lamentamos verte partir. Ya no recibirás más correos de dIKta.me." },
      invalid: { h: "Enlace inválido", p: "Este enlace de cancelación ya no es válido." },
    },
  }[locale][kind];

  return `<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>${copy.h}</title><style>
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#020617;color:#fff;margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px}
.c{max-width:520px;background:#0f172a;border:1px solid #1e293b;border-radius:16px;padding:40px;text-align:center}
.logo{font-size:20px;font-weight:bold;margin-bottom:28px}
h1{font-size:28px;font-weight:800;margin:0 0 16px;letter-spacing:-0.02em}
p{font-size:16px;line-height:1.6;color:#94a3b8;margin:0 0 24px}
a{display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold}
</style></head><body><div class="c">
<div class="logo">dIKta<span style="color:#2563eb">.</span>me</div>
<h1>${copy.h}</h1><p>${copy.p}</p>
<a href="${SITE_URL}/${locale}/blog">${locale === "es" ? "Ir al blog" : "Go to the blog"}</a>
</div></body></html>`;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: CORS_HEADERS });
  }
  if (req.method !== "GET" && req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }
  if (!SUPABASE_URL || !SERVICE_ROLE_KEY) {
    return new Response("Server misconfigured", { status: 500 });
  }

  // Token can arrive via ?t= (GET link) or form body (Gmail one-click POST)
  let token: string | null = null;
  const url = new URL(req.url);
  token = url.searchParams.get("t");

  if (!token && req.method === "POST") {
    const contentType = req.headers.get("content-type") ?? "";
    try {
      if (contentType.includes("application/json")) {
        const body = await req.json();
        token = body.t ?? body.token ?? null;
      } else if (contentType.includes("application/x-www-form-urlencoded")) {
        const body = await req.formData();
        token = (body.get("t") ?? body.get("token"))?.toString() ?? null;
      }
    } catch {
      // ignore — fall through to missing-token handling
    }
  }

  if (!token) {
    if (req.method === "POST") {
      return new Response(JSON.stringify({ error: "Missing token" }), {
        status: 400,
        headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
      });
    }
    return new Response(landingPage("en", "invalid"), {
      status: 400,
      headers: { "Content-Type": "text/html; charset=utf-8" },
    });
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY, { auth: { persistSession: false } });

  const { data: sub, error: selectErr } = await supabase
    .from("newsletter_subscribers")
    .select("id, locale, status")
    .eq("unsubscribe_token", token)
    .maybeSingle();

  if (selectErr) {
    console.error("Select error:", selectErr);
    return new Response("Database error", { status: 500 });
  }
  if (!sub) {
    if (req.method === "POST") {
      return new Response(JSON.stringify({ error: "Invalid token" }), {
        status: 404,
        headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
      });
    }
    return new Response(landingPage("en", "invalid"), {
      status: 404,
      headers: { "Content-Type": "text/html; charset=utf-8" },
    });
  }

  const locale = sub.locale as "en" | "es";

  // Idempotent: if already unsubscribed, return success page without re-writing
  if (sub.status !== "unsubscribed") {
    const { error: updateErr } = await supabase
      .from("newsletter_subscribers")
      .update({
        status: "unsubscribed",
        unsubscribed_at: new Date().toISOString(),
      })
      .eq("id", sub.id);

    if (updateErr) {
      console.error("Update error:", updateErr);
      return new Response("Database error", { status: 500 });
    }
  }

  if (req.method === "POST") {
    return new Response(JSON.stringify({ status: "unsubscribed" }), {
      status: 200,
      headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
    });
  }

  return new Response(landingPage(locale, "success"), {
    status: 200,
    headers: { "Content-Type": "text/html; charset=utf-8" },
  });
});
