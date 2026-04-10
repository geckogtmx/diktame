// LemonSqueezy webhook adapter — validates HMAC-SHA256 signature,
// extracts order details, maps product_id to credit amount, and
// produces a normalized CreditRequest.

import { type CreditRequest, resolveUserByEmail } from "../core.ts";

/** Product ID → credit amount mapping (microdollars). */
const PRODUCT_CREDIT_MAP: Record<string, number> = {
  // These IDs are configured in the LemonSqueezy dashboard.
  // Format: "product_id": amount_in_microdollars
  "910127": 0, // dIKtame App Full — Power License (no wallet credit, license only)
  "964385": 4000000, // dIKta.me Credits — 4,000 credits ($5 pack, ~20% margin)
};

/** Product ID → license tier mapping. */
const PRODUCT_TIER_MAP: Record<string, string> = {
  "910127": "power", // dIKtame App Full — Power License
};

/** Returns the license tier for a product, or null if not a license product. */
export function getProductTier(productId: string): string | null {
  return PRODUCT_TIER_MAP[productId] ?? null;
}

/**
 * Validate a LemonSqueezy webhook signature using HMAC-SHA256.
 * @param rawBody The raw request body as string
 * @param signature The X-Signature header value
 * @param secret The LemonSqueezy signing secret
 */
export async function validateSignature(
  rawBody: string,
  signature: string,
  secret: string,
): Promise<boolean> {
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );

  const sig = await crypto.subtle.sign("HMAC", key, encoder.encode(rawBody));
  const computed = Array.from(new Uint8Array(sig))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");

  // Constant-time comparison
  if (computed.length !== signature.length) return false;
  let mismatch = 0;
  for (let i = 0; i < computed.length; i++) {
    mismatch |= computed.charCodeAt(i) ^ signature.charCodeAt(i);
  }
  return mismatch === 0;
}

/**
 * Parse a LemonSqueezy webhook event and produce a CreditRequest.
 * Returns null if the event is not relevant (e.g., not order_created).
 */
export async function parseLemonSqueezyEvent(
  rawBody: string,
): Promise<CreditRequest | null> {
  const event = JSON.parse(rawBody);
  const eventName = event?.meta?.event_name;

  // Only process order_created events
  if (eventName !== "order_created") {
    return null;
  }

  const data = event?.data;
  const attrs = data?.attributes;

  if (!attrs) return null;

  const status = attrs.status;
  if (status !== "paid") return null;

  const email = attrs.user_email;
  const orderId = String(data.id);
  const productId = String(attrs.first_order_item?.product_id ?? "");

  // Map product to credit amount
  let amountMicro = PRODUCT_CREDIT_MAP[productId];

  if (!amountMicro) {
    // Reject unknown products — all valid products must be in PRODUCT_CREDIT_MAP.
    // A fallback calculation could grant incorrect credit amounts.
    console.error(
      `Unknown product_id ${productId} (total=${attrs.total}c) — rejecting. Add to PRODUCT_CREDIT_MAP.`,
    );
    return null;
  }

  if (!email) {
    console.error("LemonSqueezy webhook missing user_email");
    return null;
  }

  // Resolve user ID from email
  const userId = await resolveUserByEmail(email);
  if (!userId) {
    console.error(`No dIKta.me account found for email: ${email}`);
    return null;
  }

  return {
    user_id: userId,
    amount_micro: amountMicro,
    gateway: "lemonsqueezy",
    order_ref: `ls_${orderId}`,
    metadata: {
      product_id: productId,
      email,
      lemon_order_id: orderId,
      total_cents: attrs.total,
    },
  };
}
