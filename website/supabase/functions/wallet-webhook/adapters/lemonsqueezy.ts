// LemonSqueezy webhook adapter — validates HMAC-SHA256 signature,
// extracts order details, maps product_id to credit amount, and
// produces a normalized CreditRequest.

import { type CreditRequest, resolveUserByEmail } from "../core.ts";

/** Product ID → credit amount mapping (microdollars). */
const PRODUCT_CREDIT_MAP: Record<string, number> = {
  // These IDs are configured in the LemonSqueezy dashboard.
  // Update the keys when products are created.
  // Format: "product_id": amount_in_microdollars
  // Starter: $5.00 credit ($6.50 checkout)
  // Standard: $10.00 credit ($12.00 checkout)
  // Pro: $20.00 credit ($24.00 checkout)
  // Power: $50.00 credit ($60.00 checkout)
};

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
    // Fallback: derive from the total (in cents) minus service fee
    // This allows new products to work without code changes
    const totalCents = attrs.total ?? 0;
    // Approximate: assume ~30% is service fee, rest is credit
    // This is a safety fallback — production should use explicit mapping
    console.warn(
      `Unknown product_id ${productId}, falling back to total-based calculation`,
    );
    amountMicro = Math.round((totalCents / 130) * 1_000_000);
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
