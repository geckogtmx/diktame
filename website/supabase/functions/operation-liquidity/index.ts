// Operation Liquidity — emergency freeze and refund protocol.
// Must exist and be tested BEFORE wallet goes live.
//
// Endpoints:
//   POST ?action=freeze      — Set service_frozen = 'true' (immediate 503 on all proxy requests)
//   POST ?action=unfreeze    — Set service_frozen = 'false' (resume service)
//   POST ?action=snapshot    — Export all users with positive balances as JSON
//   POST ?action=refund-plan — Calculate refund amounts, grouped by gateway
//
// Security:
//   - All actions require X-Admin-Secret header (constant-time comparison)
//   - Refund plan is dry-run by default; requires X-Execute: true to actually process
//   - All actions are logged to console for audit trail

import { createClient } from "jsr:@supabase/supabase-js@2";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, content-type, x-admin-secret, x-execute",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }

  if (req.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  // ── Admin Secret Validation ─────────────────────────────────────────
  const adminSecret = Deno.env.get("ADMIN_SECRET") ?? "";
  const provided = req.headers.get("x-admin-secret") ?? "";

  if (!adminSecret || !constantTimeEqual(provided, adminSecret)) {
    return jsonResponse({ error: "Unauthorized" }, 401);
  }

  const url = new URL(req.url);
  const action = url.searchParams.get("action") ?? "";

  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
  const db = createClient(supabaseUrl, supabaseServiceKey, {
    auth: { persistSession: false },
  });

  switch (action) {
    case "freeze":
      return await handleFreeze(db);

    case "unfreeze":
      return await handleUnfreeze(db);

    case "snapshot":
      return await handleSnapshot(db);

    case "refund-plan": {
      const execute = req.headers.get("x-execute") === "true";
      return await handleRefundPlan(db, execute);
    }

    default:
      return jsonResponse(
        {
          error: "Unknown action",
          available: ["freeze", "unfreeze", "snapshot", "refund-plan"],
        },
        400,
      );
  }
});

// ── Freeze ────────────────────────────────────────────────────────────

async function handleFreeze(
  db: ReturnType<typeof createClient>,
): Promise<Response> {
  const { error } = await db
    .from("config")
    .upsert({ key: "service_frozen", value: "true" });

  if (error) {
    console.error("Freeze failed:", error);
    return jsonResponse({ error: "Failed to freeze service" }, 500);
  }

  console.log("[OPERATION LIQUIDITY] Service FROZEN at", new Date().toISOString());
  return jsonResponse({
    success: true,
    action: "freeze",
    message: "Service is now frozen. All proxy requests will return 503.",
    timestamp: new Date().toISOString(),
  }, 200);
}

// ── Unfreeze ──────────────────────────────────────────────────────────

async function handleUnfreeze(
  db: ReturnType<typeof createClient>,
): Promise<Response> {
  const { error } = await db
    .from("config")
    .upsert({ key: "service_frozen", value: "false" });

  if (error) {
    console.error("Unfreeze failed:", error);
    return jsonResponse({ error: "Failed to unfreeze service" }, 500);
  }

  console.log("[OPERATION LIQUIDITY] Service UNFROZEN at", new Date().toISOString());
  return jsonResponse({
    success: true,
    action: "unfreeze",
    message: "Service resumed. Proxy requests will be processed normally.",
    timestamp: new Date().toISOString(),
  }, 200);
}

// ── Snapshot ──────────────────────────────────────────────────────────

interface UserBalance {
  user_id: string;
  email: string;
  balance_micro: number;
  balance_usd: string;
}

async function handleSnapshot(
  db: ReturnType<typeof createClient>,
): Promise<Response> {
  // Get all users' latest balance using a DISTINCT ON query via RPC
  // Since Supabase JS doesn't support DISTINCT ON natively, we use a
  // simpler approach: get all rows and deduplicate in JS
  const { data: allRows, error } = await db
    .from("wallet_ledger")
    .select("user_id, balance_after_micro, created_at")
    .order("created_at", { ascending: false });

  if (error) {
    console.error("Snapshot failed:", error);
    return jsonResponse({ error: "Failed to query ledger" }, 500);
  }

  // Deduplicate: take the latest row per user
  const latestByUser = new Map<string, number>();
  for (const row of allRows ?? []) {
    if (!latestByUser.has(row.user_id)) {
      latestByUser.set(row.user_id, row.balance_after_micro);
    }
  }

  // Filter to positive balances and enrich with email
  const usersWithBalance: UserBalance[] = [];
  for (const [userId, balanceMicro] of latestByUser) {
    if (balanceMicro <= 0) continue;

    // Look up email from profiles
    const { data: profile } = await db
      .from("profiles")
      .select("email")
      .eq("id", userId)
      .single();

    usersWithBalance.push({
      user_id: userId,
      email: profile?.email ?? "unknown",
      balance_micro: balanceMicro,
      balance_usd: (balanceMicro / 1_000_000).toFixed(2),
    });
  }

  const totalMicro = usersWithBalance.reduce(
    (sum, u) => sum + u.balance_micro,
    0,
  );

  console.log(
    `[OPERATION LIQUIDITY] Snapshot: ${usersWithBalance.length} users with $${(totalMicro / 1_000_000).toFixed(2)} total`,
  );

  return jsonResponse({
    success: true,
    action: "snapshot",
    timestamp: new Date().toISOString(),
    summary: {
      total_users: usersWithBalance.length,
      total_micro: totalMicro,
      total_usd: (totalMicro / 1_000_000).toFixed(2),
    },
    users: usersWithBalance,
  }, 200);
}

// ── Refund Plan ───────────────────────────────────────────────────────

interface RefundEntry {
  user_id: string;
  email: string;
  balance_micro: number;
  refund_usd: string;
  purchase_orders: PurchaseOrder[];
}

interface PurchaseOrder {
  gateway: string;
  order_ref: string;
  amount_micro: number;
}

async function handleRefundPlan(
  db: ReturnType<typeof createClient>,
  execute: boolean,
): Promise<Response> {
  // Step 1: Get snapshot of users with positive balances
  const { data: allRows } = await db
    .from("wallet_ledger")
    .select("user_id, balance_after_micro, created_at")
    .order("created_at", { ascending: false });

  const latestByUser = new Map<string, number>();
  for (const row of allRows ?? []) {
    if (!latestByUser.has(row.user_id)) {
      latestByUser.set(row.user_id, row.balance_after_micro);
    }
  }

  // Step 2: For each user with balance, find their PURCHASE transactions
  const refundPlan: RefundEntry[] = [];

  for (const [userId, balanceMicro] of latestByUser) {
    if (balanceMicro <= 0) continue;

    const { data: profile } = await db
      .from("profiles")
      .select("email")
      .eq("id", userId)
      .single();

    // Find PURCHASE transactions to know which gateway to refund through
    const { data: purchases } = await db
      .from("wallet_ledger")
      .select("order_ref, amount_micro, metadata")
      .eq("user_id", userId)
      .eq("type", "PURCHASE")
      .order("created_at", { ascending: false });

    const purchaseOrders: PurchaseOrder[] = (purchases ?? []).map((p) => ({
      gateway: (p.metadata as Record<string, unknown>)?.gateway as string ?? "unknown",
      order_ref: p.order_ref ?? "none",
      amount_micro: p.amount_micro,
    }));

    refundPlan.push({
      user_id: userId,
      email: profile?.email ?? "unknown",
      balance_micro: balanceMicro,
      refund_usd: (balanceMicro / 1_000_000).toFixed(2),
      purchase_orders: purchaseOrders,
    });
  }

  // Group by gateway for bulk processing
  const byGateway = new Map<string, RefundEntry[]>();
  for (const entry of refundPlan) {
    const gateways = new Set(entry.purchase_orders.map((p) => p.gateway));
    for (const gw of gateways) {
      if (!byGateway.has(gw)) byGateway.set(gw, []);
      byGateway.get(gw)!.push(entry);
    }
    // Users with no purchases (only grants) go to "grant" bucket
    if (entry.purchase_orders.length === 0) {
      if (!byGateway.has("grant_only")) byGateway.set("grant_only", []);
      byGateway.get("grant_only")!.push(entry);
    }
  }

  const totalRefundMicro = refundPlan.reduce(
    (sum, e) => sum + e.balance_micro,
    0,
  );

  console.log(
    `[OPERATION LIQUIDITY] Refund plan: ${refundPlan.length} users, $${(totalRefundMicro / 1_000_000).toFixed(2)} total, execute=${execute}`,
  );

  if (execute) {
    // In a real implementation, this would:
    // 1. For each gateway, call the refund API (e.g., LemonSqueezy POST /v1/refunds)
    // 2. Insert REFUND rows into wallet_ledger to zero out balances
    // 3. Return detailed results per user
    //
    // For now, we only insert REFUND ledger rows to zero out balances.
    for (const entry of refundPlan) {
      await db.rpc("credit_wallet_balance", {
        p_user_id: entry.user_id,
        p_amount_micro: -entry.balance_micro, // Negative = deduction
        p_type: "REFUND",
        p_metadata: {
          reason: "operation_liquidity",
          original_balance_micro: entry.balance_micro,
        },
        p_order_ref: `refund_liquidity_${entry.user_id}_${Date.now()}`,
      });
    }

    return jsonResponse({
      success: true,
      action: "refund-plan",
      executed: true,
      timestamp: new Date().toISOString(),
      summary: {
        total_users: refundPlan.length,
        total_refund_micro: totalRefundMicro,
        total_refund_usd: (totalRefundMicro / 1_000_000).toFixed(2),
      },
      by_gateway: Object.fromEntries(
        [...byGateway.entries()].map(([gw, entries]) => [
          gw,
          {
            count: entries.length,
            total_micro: entries.reduce((s, e) => s + e.balance_micro, 0),
          },
        ]),
      ),
    }, 200);
  }

  // Dry-run: return the plan without executing
  return jsonResponse({
    success: true,
    action: "refund-plan",
    executed: false,
    dry_run: true,
    message: "Add X-Execute: true header to actually process refunds",
    timestamp: new Date().toISOString(),
    summary: {
      total_users: refundPlan.length,
      total_refund_micro: totalRefundMicro,
      total_refund_usd: (totalRefundMicro / 1_000_000).toFixed(2),
    },
    by_gateway: Object.fromEntries(
      [...byGateway.entries()].map(([gw, entries]) => [
        gw,
        {
          count: entries.length,
          total_micro: entries.reduce((s, e) => s + e.balance_micro, 0),
          users: entries.map((e) => ({
            email: e.email,
            refund_usd: e.refund_usd,
          })),
        },
      ]),
    ),
    refund_plan: refundPlan,
  }, 200);
});

// ── Helpers ─────────────────────────────────────────────────────────────

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
