// newsletter-preferences — public endpoint via persistent unsubscribe_token.
// GET ?t=TOKEN → renders a simple HTML preference page showing the subscription
//   and offering: resubscribe, unsubscribe, delete-my-data.
// POST ?t=TOKEN with form body action=unsubscribe|resubscribe|delete
//   → applies the change and re-renders the page.
// Phase E.4 will replace this with a Next.js page that calls back here.

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const SITE_URL = "https://www.dikta.me";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
};

type Locale = "en" | "es";

function preferencesPage(
  locale: Locale,
  email: string,
  status: string,
  token: string,
  flash: string | null,
) {
  const t = locale === "es"
    ? {
        title: "Preferencias de suscripción",
        emailLabel: "Correo",
        statusLabel: "Estado",
        subscribed: "Suscrito",
        unsubscribed: "Dado de baja",
        pending: "Pendiente de confirmación",
        deleted: "Eliminado",
        other: "Otro",
        actions: "Acciones",
        resubscribe: "Reactivar suscripción",
        unsubscribe: "Cancelar suscripción",
        delete: "Eliminar todos mis datos",
        deleteConfirm: "¿Seguro? Esta acción no se puede deshacer.",
        back: "Volver al blog",
        flashUnsub: "Te has dado de baja.",
        flashResub: "Suscripción reactivada.",
        flashDel: "Tus datos han sido eliminados.",
      }
    : {
        title: "Subscription preferences",
        emailLabel: "Email",
        statusLabel: "Status",
        subscribed: "Subscribed",
        unsubscribed: "Unsubscribed",
        pending: "Pending confirmation",
        deleted: "Deleted",
        other: "Other",
        actions: "Actions",
        resubscribe: "Resubscribe",
        unsubscribe: "Unsubscribe",
        delete: "Delete all my data",
        deleteConfirm: "Are you sure? This cannot be undone.",
        back: "Back to blog",
        flashUnsub: "You have been unsubscribed.",
        flashResub: "Subscription reactivated.",
        flashDel: "Your data has been deleted.",
      };

  const statusText =
    status === "confirmed" ? t.subscribed
      : status === "unsubscribed" ? t.unsubscribed
      : status === "pending" ? t.pending
      : status === "deleted" ? t.deleted
      : t.other;

  const canResub = status === "unsubscribed";
  const canUnsub = status === "confirmed" || status === "pending";
  const canDelete = status !== "deleted";

  const flashMap: Record<string, string> = {
    unsubscribed: t.flashUnsub,
    resubscribed: t.flashResub,
    deleted: t.flashDel,
  };
  const flashText = flash && flashMap[flash] ? flashMap[flash] : null;

  return `<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>${t.title}</title><style>
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#020617;color:#fff;margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px}
.c{max-width:560px;width:100%;background:#0f172a;border:1px solid #1e293b;border-radius:16px;padding:40px}
.logo{font-size:20px;font-weight:bold;margin-bottom:28px;text-align:center}
h1{font-size:26px;font-weight:800;margin:0 0 24px;letter-spacing:-0.02em;text-align:center}
.row{margin:14px 0;font-size:15px;color:#94a3b8}
.row b{color:#fff;font-weight:600}
.flash{background:#1e3a8a;border:1px solid #2563eb;border-radius:8px;padding:12px 16px;margin:0 0 20px;font-size:14px;color:#dbeafe}
h2{font-size:16px;font-weight:700;margin:28px 0 12px;color:#fff}
form{margin:10px 0}
button{display:block;width:100%;background:#1e293b;color:#fff;border:1px solid #334155;padding:12px 16px;border-radius:8px;font-size:15px;font-weight:600;cursor:pointer;text-align:left}
button:hover{background:#334155}
button.primary{background:#2563eb;border-color:#2563eb}
button.primary:hover{background:#1d4ed8}
button.danger{background:#450a0a;border-color:#7f1d1d;color:#fecaca}
button.danger:hover{background:#7f1d1d;color:#fff}
.foot{margin-top:28px;text-align:center;font-size:13px}
.foot a{color:#60a5fa;text-decoration:none}
</style></head><body><div class="c">
<div class="logo">dIKta<span style="color:#2563eb">.</span>me</div>
<h1>${t.title}</h1>
${flashText ? `<div class="flash">${flashText}</div>` : ""}
<div class="row"><b>${t.emailLabel}:</b> ${email}</div>
<div class="row"><b>${t.statusLabel}:</b> ${statusText}</div>
<h2>${t.actions}</h2>
${canResub ? `<form method="POST"><input type="hidden" name="t" value="${token}"><input type="hidden" name="action" value="resubscribe"><button class="primary" type="submit">${t.resubscribe}</button></form>` : ""}
${canUnsub ? `<form method="POST"><input type="hidden" name="t" value="${token}"><input type="hidden" name="action" value="unsubscribe"><button type="submit">${t.unsubscribe}</button></form>` : ""}
${canDelete ? `<form method="POST" onsubmit="return confirm('${t.deleteConfirm}')"><input type="hidden" name="t" value="${token}"><input type="hidden" name="action" value="delete"><button class="danger" type="submit">${t.delete}</button></form>` : ""}
<div class="foot"><a href="${SITE_URL}/${locale}/blog">${t.back}</a></div>
</div></body></html>`;
}

function notFoundPage() {
  return `<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>Not found</title><style>
body{font-family:-apple-system,BlinkMacSystemFont,sans-serif;background:#020617;color:#fff;margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px;text-align:center}
.c{max-width:420px}
h1{font-size:24px}p{color:#94a3b8}
</style></head><body><div class="c"><h1>Invalid link</h1><p>This preference link is no longer valid.</p></div></body></html>`;
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

  const url = new URL(req.url);
  let token = url.searchParams.get("t");
  let action: string | null = null;

  if (req.method === "POST") {
    const contentType = req.headers.get("content-type") ?? "";
    try {
      if (contentType.includes("application/x-www-form-urlencoded") || contentType.includes("multipart/form-data")) {
        const body = await req.formData();
        token = token ?? (body.get("t")?.toString() ?? null);
        action = body.get("action")?.toString() ?? null;
      } else if (contentType.includes("application/json")) {
        const body = await req.json();
        token = token ?? body.t ?? null;
        action = body.action ?? null;
      }
    } catch {
      // ignore
    }
  }

  if (!token) {
    return new Response(notFoundPage(), {
      status: 400,
      headers: { "Content-Type": "text/html; charset=utf-8" },
    });
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY, { auth: { persistSession: false } });

  const { data: sub, error: selectErr } = await supabase
    .from("newsletter_subscribers")
    .select("id, email, locale, status")
    .eq("unsubscribe_token", token)
    .maybeSingle();

  if (selectErr) {
    console.error("Select error:", selectErr);
    return new Response("Database error", { status: 500 });
  }
  if (!sub) {
    return new Response(notFoundPage(), {
      status: 404,
      headers: { "Content-Type": "text/html; charset=utf-8" },
    });
  }

  const locale = sub.locale as Locale;
  let flash: string | null = null;
  let currentStatus = sub.status as string;
  let displayEmail = sub.email as string;

  if (req.method === "POST" && action) {
    if (action === "unsubscribe" && currentStatus !== "unsubscribed") {
      const { error } = await supabase
        .from("newsletter_subscribers")
        .update({ status: "unsubscribed", unsubscribed_at: new Date().toISOString() })
        .eq("id", sub.id);
      if (error) {
        console.error("Unsubscribe error:", error);
        return new Response("Database error", { status: 500 });
      }
      currentStatus = "unsubscribed";
      flash = "unsubscribed";
    } else if (action === "resubscribe" && currentStatus === "unsubscribed") {
      const { error } = await supabase
        .from("newsletter_subscribers")
        .update({ status: "confirmed", confirmed_at: new Date().toISOString(), unsubscribed_at: null })
        .eq("id", sub.id);
      if (error) {
        console.error("Resubscribe error:", error);
        return new Response("Database error", { status: 500 });
      }
      currentStatus = "confirmed";
      flash = "resubscribed";
    } else if (action === "delete" && currentStatus !== "deleted") {
      // Soft-delete: anonymize email, null tokens, keep audit row for compliance
      const anonEmail = `deleted-${sub.id}@anon.local`;
      const { error } = await supabase
        .from("newsletter_subscribers")
        .update({
          status: "deleted",
          email: anonEmail,
          confirm_token: null,
          unsubscribe_token: null,
          signup_ip: null,
          signup_user_agent: null,
          unsubscribed_at: new Date().toISOString(),
        })
        .eq("id", sub.id);
      if (error) {
        console.error("Delete error:", error);
        return new Response("Database error", { status: 500 });
      }
      // After delete the token is nulled; show a final flash page without actions
      return new Response(
        preferencesPage(locale, anonEmail, "deleted", token, "deleted"),
        { status: 200, headers: { "Content-Type": "text/html; charset=utf-8" } },
      );
    }
  }

  return new Response(
    preferencesPage(locale, displayEmail, currentStatus, token, flash),
    { status: 200, headers: { "Content-Type": "text/html; charset=utf-8" } },
  );
});
