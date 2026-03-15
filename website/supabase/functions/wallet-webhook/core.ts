// Core webhook handler — gateway-agnostic credit processing.
// Every gateway adapter produces a CreditRequest; this module validates
// business rules and inserts the PURCHASE ledger row via atomic RPC.

import { createClient, type SupabaseClient } from "jsr:@supabase/supabase-js@2";

/** Normalized credit request — every gateway adapter produces this. */
export interface CreditRequest {
  /** Supabase auth user ID */
  user_id: string;
  /** Credits in microdollars (1 USD = 1,000,000) */
  amount_micro: number;
  /** Gateway identifier: "lemonsqueezy" | "kofi" | "stripe" | "manual" | ... */
  gateway: string;
  /** Unique order/transaction ID from gateway (for dedup) */
  order_ref: string;
  /** Gateway-specific metadata (product_id, email, etc.) */
  metadata: Record<string, unknown>;
}

export interface CreditResult {
  success: boolean;
  error?: string;
  transaction_id?: string;
  balance_micro?: number;
  duplicate?: boolean;
}

/** Creates a service-role Supabase client for DB operations. */
export function createServiceClient(): SupabaseClient {
  const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
  const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

  return createClient(supabaseUrl, supabaseServiceKey, {
    auth: { persistSession: false },
  });
}

/**
 * Process a credit request: validate, enforce caps, dedup, insert.
 * Returns a structured result with success/error status.
 */
export async function processCredit(
  credit: CreditRequest,
): Promise<CreditResult> {
  // ── Validation ──────────────────────────────────────────────────────
  if (!credit.user_id || credit.user_id.length < 10) {
    return { success: false, error: "invalid_user_id" };
  }

  if (credit.amount_micro <= 0) {
    return { success: false, error: "invalid_amount" };
  }

  if (credit.amount_micro > 50_000_000) {
    // Single credit > $50 — reject outright
    return { success: false, error: "amount_exceeds_cap" };
  }

  if (!credit.order_ref) {
    return { success: false, error: "missing_order_ref" };
  }

  // ── Atomic credit via RPC ───────────────────────────────────────────
  const db = createServiceClient();

  const { data: result, error: rpcError } = await db.rpc(
    "credit_wallet_balance",
    {
      p_user_id: credit.user_id,
      p_amount_micro: credit.amount_micro,
      p_type: "PURCHASE",
      p_metadata: {
        gateway: credit.gateway,
        ...credit.metadata,
      },
      p_order_ref: credit.order_ref,
    },
  );

  if (rpcError) {
    console.error("Credit RPC error:", rpcError);
    return { success: false, error: "database_error" };
  }

  if (!result.success) {
    return {
      success: false,
      error: result.error,
      balance_micro: result.balance_micro,
    };
  }

  return {
    success: true,
    transaction_id: result.transaction_id,
    balance_micro: result.balance_micro,
    duplicate: result.duplicate ?? false,
  };
}

/**
 * Look up a Supabase user ID by email address.
 * Many gateways only provide the customer email, not the Supabase user ID.
 */
export async function resolveUserByEmail(
  email: string,
): Promise<string | null> {
  const db = createServiceClient();

  const { data: profile } = await db
    .from("profiles")
    .select("id")
    .eq("email", email)
    .single();

  return profile?.id ?? null;
}
