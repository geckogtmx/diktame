// newsletter-webhook — receives Resend event webhooks (Svix-signed).
// POST → verifies signature, writes newsletter_events row, updates subscriber
// status on bounce/complaint.
//
// Env: RESEND_WEBHOOK_SECRET (from Resend dashboard → Webhooks → Signing Secret)
// verify_jwt is disabled (Resend does not send a Supabase JWT). Auth is via HMAC.

import { serve } from "https://deno.land/std@0.168.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const WEBHOOK_SECRET = Deno.env.get("RESEND_WEBHOOK_SECRET");

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

// Svix signature verification
// Docs: https://docs.svix.com/receiving/verifying-payloads/how-manual
async function verifySvixSignature(
  secret: string,
  svixId: string,
  svixTimestamp: string,
  svixSignature: string,
  rawBody: string,
): Promise<boolean> {
  // Secret format: "whsec_<base64>"
  const secretBytes = secret.startsWith("whsec_")
    ? Uint8Array.from(atob(secret.slice(6)), (c) => c.charCodeAt(0))
    : new TextEncoder().encode(secret);

  const signedPayload = `${svixId}.${svixTimestamp}.${rawBody}`;
  const key = await crypto.subtle.importKey(
    "raw",
    secretBytes as BufferSource,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sigBuffer = await crypto.subtle.sign(
    "HMAC",
    key,
    new TextEncoder().encode(signedPayload),
  );
  const computedB64 = btoa(String.fromCharCode(...new Uint8Array(sigBuffer)));

  // Header format: "v1,SIG1 v1,SIG2 ..." — match any version-1 signature
  const candidates = svixSignature
    .split(" ")
    .map((part) => part.trim())
    .filter((part) => part.startsWith("v1,"))
    .map((part) => part.slice(3));

  return candidates.includes(computedB64);
}

// Map Resend event type to our newsletter_events.event_type CHECK values
function mapResendEventType(resendType: string): string | null {
  const t = resendType.toLowerCase();
  if (t.includes("delivered")) return "delivered";
  if (t.includes("bounced")) return "bounced";
  if (t.includes("complained")) return "complained";
  if (t.includes("opened")) return "opened";
  if (t.includes("clicked")) return "clicked";
  if (t === "email.unsubscribed" || t.endsWith(".unsubscribed")) return "unsubscribed";
  if (t.includes("sent") || t === "email.sent") return "sent";
  if (t.includes("failed")) return "failed";
  return null;
}

serve(async (req) => {
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }
  if (!SUPABASE_URL || !SERVICE_ROLE_KEY) {
    return jsonResponse({ error: "Server misconfigured" }, 500);
  }

  const svixId = req.headers.get("svix-id");
  const svixTimestamp = req.headers.get("svix-timestamp");
  const svixSignature = req.headers.get("svix-signature");
  const rawBody = await req.text();

  if (!svixId || !svixTimestamp || !svixSignature) {
    return jsonResponse({ error: "Missing Svix headers" }, 400);
  }

  if (WEBHOOK_SECRET) {
    const valid = await verifySvixSignature(
      WEBHOOK_SECRET,
      svixId,
      svixTimestamp,
      svixSignature,
      rawBody,
    );
    if (!valid) {
      console.error("Invalid webhook signature for event", svixId);
      return jsonResponse({ error: "Invalid signature" }, 401);
    }
  } else {
    console.warn("RESEND_WEBHOOK_SECRET not set — skipping signature verification");
  }

  let event: {
    type?: string;
    created_at?: string;
    data?: {
      email_id?: string;
      to?: string[] | string;
      from?: string;
      subject?: string;
      tags?: Record<string, string>;
      [k: string]: unknown;
    };
  };
  try {
    event = JSON.parse(rawBody);
  } catch {
    return jsonResponse({ error: "Invalid JSON" }, 400);
  }

  const supabase = createClient(SUPABASE_URL, SERVICE_ROLE_KEY, { auth: { persistSession: false } });

  const eventType = mapResendEventType(event.type ?? "");
  if (!eventType) {
    console.log("Ignoring unknown Resend event type:", event.type);
    return jsonResponse({ status: "ignored", type: event.type }, 200);
  }

  // Match the recipient back to a subscriber row so we can attribute the event
  const recipient = Array.isArray(event.data?.to)
    ? event.data?.to[0]
    : typeof event.data?.to === "string" ? event.data.to : null;

  let subscriberId: string | null = null;
  if (recipient) {
    const { data: sub } = await supabase
      .from("newsletter_subscribers")
      .select("id")
      .eq("email", recipient.toLowerCase())
      .limit(1)
      .maybeSingle();
    if (sub) subscriberId = sub.id;
  }

  // Insert the event (dedupes on resend_event_id UNIQUE constraint)
  const { error: insertErr } = await supabase
    .from("newsletter_events")
    .insert({
      subscriber_id: subscriberId,
      event_type: eventType,
      resend_event_id: svixId,
      event_data: event as unknown as Record<string, unknown>,
    });

  if (insertErr) {
    // Duplicate? 23505 = unique_violation
    const code = (insertErr as { code?: string }).code;
    if (code === "23505") {
      return jsonResponse({ status: "duplicate", svix_id: svixId }, 200);
    }
    console.error("Event insert error:", insertErr);
    return jsonResponse({ error: "Database error" }, 500);
  }

  // Auto-suppress on hard bounce / complaint
  if (subscriberId && (eventType === "bounced" || eventType === "complained")) {
    const newStatus = eventType === "bounced" ? "bounced" : "complained";
    const { error: updateErr } = await supabase
      .from("newsletter_subscribers")
      .update({ status: newStatus })
      .eq("id", subscriberId);
    if (updateErr) {
      console.error("Subscriber status update error:", updateErr);
    }
  }

  return jsonResponse({ status: "ok", event_type: eventType, subscriber_id: subscriberId }, 200);
});
