// Wallet Status Edge Function — returns current balance and recent transactions.
// Called by the C# app on startup to sync local ledger with server.
//
// GET /functions/v1/wallet-status
// Authorization: Bearer <jwt>
// Response: { balance_micro, transactions: [...] }

import { createClient } from "jsr:@supabase/supabase-js@2";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, content-type",
  "Access-Control-Allow-Methods": "GET, OPTIONS",
};

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }

  if (req.method !== "GET") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  // ── JWT Validation ──────────────────────────────────────────────────
  const authHeader = req.headers.get("authorization");
  if (!authHeader?.startsWith("Bearer ")) {
    return jsonResponse({ error: "Missing authorization" }, 401);
  }
  const token = authHeader.slice(7);

  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

  const authClient = createClient(supabaseUrl, supabaseServiceKey, {
    global: { headers: { Authorization: `Bearer ${token}` } },
    auth: { persistSession: false },
  });

  const { data: { user }, error: authError } = await authClient.auth.getUser(token);
  if (authError || !user) {
    return jsonResponse({ error: "Invalid or expired token" }, 401);
  }

  const userId = user.id;

  // Service client for DB reads
  const db = createClient(supabaseUrl, supabaseServiceKey, {
    auth: { persistSession: false },
  });

  // ── Get Balance ─────────────────────────────────────────────────────
  const { data: balanceRow } = await db
    .from("wallet_ledger")
    .select("balance_after_micro")
    .eq("user_id", userId)
    .order("created_at", { ascending: false })
    .limit(1)
    .single();

  const balanceMicro = balanceRow?.balance_after_micro ?? 0;

  // ── Get Recent Transactions ─────────────────────────────────────────
  const { data: transactions } = await db
    .from("wallet_ledger")
    .select("id, amount_micro, balance_after_micro, type, created_at, expires_at, metadata")
    .eq("user_id", userId)
    .order("created_at", { ascending: false })
    .limit(50);

  return jsonResponse({
    balance_micro: balanceMicro,
    transactions: transactions ?? [],
  }, 200);
});

function jsonResponse(
  body: Record<string, unknown>,
  status: number,
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...CORS_HEADERS,
    },
  });
}
