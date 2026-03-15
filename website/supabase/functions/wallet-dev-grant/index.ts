// Development-only credit grant Edge Function.
// Protected by DEV_SECRET (constant-time comparison).
// For testing wallet flows without real payments.
//
// POST /functions/v1/wallet-dev-grant
// Headers: X-Dev-Secret: <secret>
// Body: { "user_id": "uuid", "amount_micro": 5000000 }
//
// IMPORTANT: This function should NOT be deployed to production.
// Remove or disable it before go-live.

import { createClient } from "jsr:@supabase/supabase-js@2";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, content-type, x-dev-secret",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }

  if (req.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  // ── Secret Validation (constant-time) ──────────────────────────────
  const devSecret = Deno.env.get("DEV_SECRET") ?? "";
  const provided = req.headers.get("x-dev-secret") ?? "";

  if (!devSecret || !constantTimeEqual(provided, devSecret)) {
    return jsonResponse({ error: "Unauthorized" }, 401);
  }

  // ── Parse Request ──────────────────────────────────────────────────
  const body = await req.json();
  const userId = body.user_id as string;
  const amountMicro = body.amount_micro as number;

  if (!userId || !amountMicro || amountMicro <= 0) {
    return jsonResponse({ error: "Invalid request" }, 400);
  }

  if (amountMicro > 50_000_000) {
    return jsonResponse({ error: "Amount exceeds $50 cap" }, 400);
  }

  // ── Credit via RPC ─────────────────────────────────────────────────
  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

  const db = createClient(supabaseUrl, supabaseServiceKey, {
    auth: { persistSession: false },
  });

  const { data: result, error } = await db.rpc("credit_wallet_balance", {
    p_user_id: userId,
    p_amount_micro: amountMicro,
    p_type: "GRANT",
    p_metadata: { reason: "dev_grant" },
    p_order_ref: `dev_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    p_expires_at: null,
  });

  if (error) {
    console.error("Dev grant error:", error);
    return jsonResponse({ error: "Database error" }, 500);
  }

  return jsonResponse({
    success: result.success,
    transaction_id: result.transaction_id,
    balance_micro: result.balance_micro,
    error: result.error,
  }, result.success ? 200 : 422);
});

function constantTimeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let mismatch = 0;
  for (let i = 0; i < a.length; i++) {
    mismatch |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }
  return mismatch === 0;
}

function jsonResponse(
  body: Record<string, unknown>,
  status: number,
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...CORS_HEADERS },
  });
}
