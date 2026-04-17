// newsletter-subscribe — public endpoint for blog-sidebar (and similar) signups.
// Creates or reuses a pending subscriber row, then emails a confirmation link.
// Double opt-in: nothing is considered subscribed until /newsletter-confirm runs.

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const RESEND_API_KEY = Deno.env.get("RESEND_API_KEY");
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const SITE_URL = "https://www.dikta.me";
const FUNCTIONS_URL = `${SUPABASE_URL}/functions/v1`;
const CONSENT_VERSION = "v1-2026-04-17";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const VALID_LOCALES = ["en", "es"] as const;
const VALID_SOURCES = ["account-signup", "blog-sidebar", "landing", "admin"] as const;

type Locale = (typeof VALID_LOCALES)[number];

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
  });
}

function confirmationEmailHtml(confirmUrl: string, locale: Locale) {
  const copy = locale === "es"
    ? {
        title: "Confirma tu suscripción",
        intro: "Gracias por suscribirte a dIKta.me. Haz clic en el botón para confirmar tu dirección de correo y empezar a recibir el boletín.",
        button: "Confirmar suscripción",
        fallback: "Si el botón no funciona, copia y pega este enlace en tu navegador:",
        footer: "Si no te suscribiste, puedes ignorar este correo — nunca recibirás nada más.",
      }
    : {
        title: "Confirm your subscription",
        intro: "Thanks for subscribing to dIKta.me. Click the button below to confirm your email address and start receiving the newsletter.",
        button: "Confirm subscription",
        fallback: "If the button does not work, copy and paste this link into your browser:",
        footer: "If you did not subscribe, you can safely ignore this email — you will not hear from us again.",
      };

  return `<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><style>
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;background:#020617;color:#fff;margin:0;padding:40px}
.c{max-width:600px;margin:0 auto;background:#0f172a;border:1px solid #1e293b;border-radius:16px;padding:40px}
.logo{font-size:24px;font-weight:bold;margin-bottom:32px}
h1{font-size:26px;font-weight:800;margin:0 0 16px;letter-spacing:-0.02em}
p{font-size:16px;line-height:1.6;color:#94a3b8;margin:0 0 20px}
.btn{display:inline-block;background:#2563eb;color:#fff;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;margin:8px 0 24px}
.link{word-break:break-all;color:#60a5fa;font-size:13px}
.foot{margin-top:32px;padding-top:20px;border-top:1px solid #1e293b;font-size:12px;color:#64748b}
</style></head>
<body><div class="c">
<div class="logo">dIKta<span style="color:#2563eb">.</span>me</div>
<h1>${copy.title}</h1>
<p>${copy.intro}</p>
<a class="btn" href="${confirmUrl}">${copy.button}</a>
<p>${copy.fallback}</p>
<p class="link">${confirmUrl}</p>
<div class="foot">${copy.footer}<br><br>San Francisco 1826-C-101, Del Valle, 03100, CDMX, México</div>
</div></body></html>`;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: CORS_HEADERS });
  }
  if (req.method !== "POST") {
    return json({ error: "Method not allowed" }, 405);
  }

  if (!RESEND_API_KEY || !SUPABASE_URL || !SERVICE_ROLE_KEY) {
    console.error("Missing env vars");
    return json({ error: "Server misconfigured" }, 500);
  }

  let payload: { email?: string; locale?: string; source?: string } = {};
  try {
    payload = await req.json();
  } catch {
    return json({ error: "Invalid JSON" }, 400);
  }

  const rawEmail = (payload.email ?? "").trim().toLowerCase();
  const locale = payload.locale as Locale;
  const source = (payload.source ?? "blog-sidebar") as (typeof VALID_SOURCES)[number];

  if (!rawEmail || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(rawEmail)) {
    return json({ error: "Invalid email" }, 400);
  }
  if (!VALID_LOCALES.includes(locale)) {
    return json({ error: "Invalid locale (must be 'en' or 'es')" }, 400);
  }
  if (!VALID_SOURCES.includes(source)) {
    return json({ error: "Invalid source" }, 400);
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY, {
    auth: { persistSession: false },
  });

  // Request context for GDPR consent audit trail
  const signupIp = req.headers.get("x-forwarded-for")?.split(",")[0].trim()
    ?? req.headers.get("x-real-ip") ?? null;
  const signupUserAgent = req.headers.get("user-agent") ?? null;
  const signupUrl = req.headers.get("referer") ?? null;

  // Check for existing row on (email, locale)
  const { data: existing, error: selectErr } = await supabase
    .from("newsletter_subscribers")
    .select("id, status, confirm_token, unsubscribe_token")
    .eq("email", rawEmail)
    .eq("locale", locale)
    .maybeSingle();

  if (selectErr) {
    console.error("Select error:", selectErr);
    return json({ error: "Database error" }, 500);
  }

  let confirmToken: string;
  let subscriberId: string;

  if (existing) {
    if (existing.status === "confirmed") {
      return json({ status: "already_subscribed" }, 200);
    }
    // For pending/unsubscribed/bounced/complained/deleted: re-send a confirmation.
    // Regenerate confirm_token each time so older links expire.
    confirmToken = crypto.randomUUID();
    const unsubscribeToken = existing.unsubscribe_token ?? crypto.randomUUID();
    const { error: updateErr } = await supabase
      .from("newsletter_subscribers")
      .update({
        status: "pending",
        confirm_token: confirmToken,
        unsubscribe_token: unsubscribeToken,
        source,
        signup_ip: signupIp,
        signup_url: signupUrl,
        signup_user_agent: signupUserAgent,
        consent_text_version: CONSENT_VERSION,
        confirmed_at: null,
        unsubscribed_at: null,
      })
      .eq("id", existing.id);
    if (updateErr) {
      console.error("Update error:", updateErr);
      return json({ error: "Database error" }, 500);
    }
    subscriberId = existing.id;
  } else {
    confirmToken = crypto.randomUUID();
    const unsubscribeToken = crypto.randomUUID();
    const { data: inserted, error: insertErr } = await supabase
      .from("newsletter_subscribers")
      .insert({
        email: rawEmail,
        locale,
        status: "pending",
        source,
        signup_ip: signupIp,
        signup_url: signupUrl,
        signup_user_agent: signupUserAgent,
        consent_text_version: CONSENT_VERSION,
        confirm_token: confirmToken,
        unsubscribe_token: unsubscribeToken,
      })
      .select("id")
      .single();
    if (insertErr || !inserted) {
      console.error("Insert error:", insertErr);
      return json({ error: "Database error" }, 500);
    }
    subscriberId = inserted.id;
  }

  // Send confirmation email via Resend
  const confirmUrl = `${FUNCTIONS_URL}/newsletter-confirm?t=${confirmToken}`;
  const subject = locale === "es"
    ? "Confirma tu suscripción a dIKta.me"
    : "Confirm your dIKta.me subscription";

  const resendRes = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${RESEND_API_KEY}`,
    },
    body: JSON.stringify({
      from: "dIKta.me <newsletter@dikta.me>",
      replyTo: "newsletter@dikta.me",
      to: [rawEmail],
      subject,
      html: confirmationEmailHtml(confirmUrl, locale),
    }),
  });

  if (!resendRes.ok) {
    const errText = await resendRes.text();
    console.error("Resend error:", resendRes.status, errText);
    return json({ error: "Failed to send confirmation email" }, 500);
  }

  return json({ status: "pending_confirmation", id: subscriberId }, 200);
});
