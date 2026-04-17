// newsletter-confirm — public endpoint.
// GET ?t=TOKEN → flips a pending row to confirmed, nulls the confirm_token,
// sends the welcome email, returns a simple HTML landing page.
// Phase E.5 will replace this with a Next.js page that calls back here.

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const RESEND_API_KEY = Deno.env.get("RESEND_API_KEY");
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const SITE_URL = "https://www.dikta.me";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "GET, OPTIONS",
};

function landingPage(locale: "en" | "es", kind: "success" | "already" | "invalid") {
  const copy = {
    en: {
      success: { h: "You're subscribed!", p: "Thanks for confirming. You'll receive future posts from dIKta.me in your inbox." },
      already: { h: "Already confirmed", p: "This email is already subscribed. No further action needed." },
      invalid: { h: "Invalid or expired link", p: "This confirmation link is no longer valid. Try subscribing again from the blog." },
    },
    es: {
      success: { h: "¡Suscripción confirmada!", p: "Gracias por confirmar. Recibirás las próximas publicaciones de dIKta.me en tu bandeja de entrada." },
      already: { h: "Ya confirmado", p: "Este correo ya está suscrito. No es necesario hacer nada más." },
      invalid: { h: "Enlace inválido o caducado", p: "Este enlace de confirmación ya no es válido. Intenta suscribirte de nuevo desde el blog." },
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

function welcomeEmailHtml(locale: "en" | "es", preferencesUrl: string, unsubscribeUrl: string) {
  const copy = locale === "es"
    ? {
        title: "¡Bienvenido a dIKta.me!",
        intro: "Estás dentro. A partir de ahora recibirás cada nueva publicación directamente en tu bandeja de entrada, con el audio incluido.",
        cta: "Visitar el blog",
        prefs: "Gestionar preferencias",
        unsub: "Cancelar suscripción",
      }
    : {
        title: "Welcome to dIKta.me!",
        intro: "You're in. From now on, every new post lands in your inbox — full text, with audio.",
        cta: "Visit the blog",
        prefs: "Manage preferences",
        unsub: "Unsubscribe",
      };

  return `<!DOCTYPE html>
<html><head><meta charset="utf-8"><style>
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#020617;color:#fff;margin:0;padding:40px}
.c{max-width:600px;margin:0 auto;background:#0f172a;border:1px solid #1e293b;border-radius:16px;padding:40px}
.logo{font-size:24px;font-weight:bold;margin-bottom:32px}
h1{font-size:26px;font-weight:800;margin:0 0 16px;letter-spacing:-0.02em}
p{font-size:16px;line-height:1.6;color:#94a3b8;margin:0 0 20px}
.btn{display:inline-block;background:#2563eb;color:#fff;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;margin:8px 0 24px}
.foot{margin-top:32px;padding-top:20px;border-top:1px solid #1e293b;font-size:12px;color:#64748b}
.foot a{color:#94a3b8}
</style></head><body><div class="c">
<div class="logo">dIKta<span style="color:#2563eb">.</span>me</div>
<h1>${copy.title}</h1>
<p>${copy.intro}</p>
<a class="btn" href="${SITE_URL}/${locale}/blog">${copy.cta}</a>
<div class="foot">
<a href="${preferencesUrl}">${copy.prefs}</a> · <a href="${unsubscribeUrl}">${copy.unsub}</a><br><br>
San Francisco 1826-C-101, Del Valle, 03100, CDMX, México
</div>
</div></body></html>`;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: CORS_HEADERS });
  }
  if (req.method !== "GET") {
    return new Response("Method not allowed", { status: 405 });
  }
  if (!SUPABASE_URL || !SERVICE_ROLE_KEY || !RESEND_API_KEY) {
    return new Response("Server misconfigured", { status: 500 });
  }

  const url = new URL(req.url);
  const token = url.searchParams.get("t");
  if (!token) {
    return new Response(landingPage("en", "invalid"), {
      status: 400,
      headers: { "Content-Type": "text/html; charset=utf-8" },
    });
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY, { auth: { persistSession: false } });

  const { data: sub, error: selectErr } = await supabase
    .from("newsletter_subscribers")
    .select("id, email, locale, status, unsubscribe_token")
    .eq("confirm_token", token)
    .maybeSingle();

  if (selectErr) {
    console.error("Select error:", selectErr);
    return new Response("Database error", { status: 500 });
  }

  const FUNCTIONS_URL = `${SUPABASE_URL}/functions/v1`;

  if (!sub) {
    // Token may have been consumed already; surface a neutral message.
    return new Response(landingPage("en", "invalid"), {
      status: 404,
      headers: { "Content-Type": "text/html; charset=utf-8" },
    });
  }

  const locale = sub.locale as "en" | "es";

  if (sub.status === "confirmed") {
    return new Response(landingPage(locale, "already"), {
      status: 200,
      headers: { "Content-Type": "text/html; charset=utf-8" },
    });
  }

  const { error: updateErr } = await supabase
    .from("newsletter_subscribers")
    .update({
      status: "confirmed",
      confirmed_at: new Date().toISOString(),
      confirm_token: null,
    })
    .eq("id", sub.id);

  if (updateErr) {
    console.error("Update error:", updateErr);
    return new Response("Database error", { status: 500 });
  }

  // Send welcome email (fire-and-forget; do not block the response on failure)
  const preferencesUrl = `${FUNCTIONS_URL}/newsletter-preferences?t=${sub.unsubscribe_token}`;
  const unsubscribeUrl = `${FUNCTIONS_URL}/newsletter-unsubscribe?t=${sub.unsubscribe_token}`;
  const subject = locale === "es" ? "¡Bienvenido a dIKta.me!" : "Welcome to dIKta.me!";

  fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${RESEND_API_KEY}`,
    },
    body: JSON.stringify({
      from: "dIKta.me <newsletter@dikta.me>",
      replyTo: "newsletter@dikta.me",
      to: [sub.email],
      subject,
      html: welcomeEmailHtml(locale, preferencesUrl, unsubscribeUrl),
      headers: {
        "List-Unsubscribe": `<${unsubscribeUrl}>`,
        "List-Unsubscribe-Post": "List-Unsubscribe=One-Click",
      },
    }),
  }).catch((err) => console.error("Welcome email failed:", err));

  return new Response(landingPage(locale, "success"), {
    status: 200,
    headers: { "Content-Type": "text/html; charset=utf-8" },
  });
});
