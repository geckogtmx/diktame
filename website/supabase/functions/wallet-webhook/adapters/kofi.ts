import { type CreditRequest, resolveUserByEmail } from "../core.ts";

/**
 * Validate a Ko-fi webhook using the verification token.
 * Ko-fi sends the token in the POST body as `verification_token`.
 */
export function validateKofiToken(bodyToken: string): boolean {
  const expected = Deno.env.get("KOFI_VERIFICATION_TOKEN") ?? "";
  if (!expected || !bodyToken) return false;

  // Constant-time comparison
  if (expected.length !== bodyToken.length) return false;
  let mismatch = 0;
  for (let i = 0; i < expected.length; i++) {
    mismatch |= expected.charCodeAt(i) ^ bodyToken.charCodeAt(i);
  }
  return mismatch === 0;
}

/**
 * Parse a Ko-fi webhook event and produce a CreditRequest.
 * Ko-fi POST body is `data=JSON_STRING` (form-encoded).
 * Returns null if the event is not a donation/purchase.
 */
export async function parseKofiEvent(
  rawBody: string,
): Promise<CreditRequest | null> {
  // Ko-fi sends form-encoded: data={json}
  const params = new URLSearchParams(rawBody);
  const dataStr = params.get("data");
  if (!dataStr) return null;

  const data = JSON.parse(dataStr);

  // Only process completed donations/purchases
  if (!["Donation", "Commission", "Shop Order"].includes(data.type)) {
    return null;
  }

  const email = data.email;
  const amount = parseFloat(data.amount); // in donor's currency (usually USD)
  const kofiId = data.kofi_transaction_id;

  if (!email || !amount || !kofiId) {
    console.error("Ko-fi webhook missing required fields");
    return null;
  }

  // Convert USD to microdollars
  const amountMicro = Math.round(amount * 1_000_000);

  // Resolve user by email
  const userId = await resolveUserByEmail(email);
  if (!userId) {
    console.error(`No dIKta.me account found for Ko-fi email: ${email}`);
    return null;
  }

  return {
    user_id: userId,
    amount_micro: amountMicro,
    gateway: "kofi",
    order_ref: `kofi_${kofiId}`,
    metadata: {
      email,
      kofi_transaction_id: kofiId,
      type: data.type,
      message: data.message ?? "",
      amount_usd: amount,
    },
  };
}
