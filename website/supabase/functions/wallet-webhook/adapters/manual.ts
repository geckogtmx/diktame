// Manual grant adapter — admin-initiated credits for Ko-fi donations,
// Substack supporters, beta testers, support cases, etc.
//
// Protected by ADMIN_SECRET (constant-time comparison).
// This is the "catch-all" adapter for payment sources that don't have
// automated webhook support.
//
// POST body:
// {
//   "email": "user@example.com",
//   "amount_micro": 5000000,     // $5.00 credit
//   "reason": "kofi_donation",   // or "beta_tester", "support_case", etc.
//   "order_ref": "kofi_abc123"   // unique ref to prevent double-crediting
// }

import { type CreditRequest, resolveUserByEmail } from "../core.ts";

/**
 * Validate the admin secret using constant-time comparison.
 */
export function validateAdminSecret(provided: string): boolean {
  const expected = Deno.env.get("ADMIN_SECRET") ?? "";
  if (provided.length !== expected.length) return false;
  let mismatch = 0;
  for (let i = 0; i < provided.length; i++) {
    mismatch |= provided.charCodeAt(i) ^ expected.charCodeAt(i);
  }
  return mismatch === 0;
}

/**
 * Parse a manual grant request into a CreditRequest.
 */
export async function parseManualGrant(
  body: Record<string, unknown>,
): Promise<CreditRequest | null> {
  const email = body.email as string;
  const amountMicro = body.amount_micro as number;
  const reason = (body.reason as string) ?? "manual_grant";
  const orderRef = body.order_ref as string;

  if (!email || !amountMicro || !orderRef) {
    return null;
  }

  if (amountMicro <= 0 || amountMicro > 50_000_000) {
    return null;
  }

  const userId = await resolveUserByEmail(email);
  if (!userId) {
    console.error(`Manual grant: no account for email ${email}`);
    return null;
  }

  return {
    user_id: userId,
    amount_micro: amountMicro,
    gateway: "manual",
    order_ref: `manual_${orderRef}`,
    metadata: {
      email,
      reason,
      admin_initiated: true,
    },
  };
}
