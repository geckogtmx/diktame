// Wallet Webhook Edge Function — gateway-agnostic payment webhook router.
// Dispatches to the correct adapter based on request headers/path,
// then processes the normalized CreditRequest through the core handler.
//
// Routes:
//   POST /wallet-webhook?gateway=lemonsqueezy  → LemonSqueezy adapter
//   POST /wallet-webhook?gateway=manual         → Manual grant adapter
//   POST /wallet-webhook                        → Auto-detect from headers
//
// Adding a new gateway:
// 1. Create adapters/<gateway>.ts
// 2. Add route in this file
// 3. No changes to core handler or C# app

import { processCredit, provisionLicense } from "./core.ts";
import {
  getProductTier,
  parseLemonSqueezyEvent,
  validateSignature,
} from "./adapters/lemonsqueezy.ts";
import { parseManualGrant, validateAdminSecret } from "./adapters/manual.ts";
import { parseKofiEvent, validateKofiToken } from "./adapters/kofi.ts";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, content-type, x-signature",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: CORS_HEADERS });
  }

  if (req.method !== "POST") {
    return jsonResponse({ error: "Method not allowed" }, 405);
  }

  const url = new URL(req.url);
  let gateway = url.searchParams.get("gateway") ?? "";

  // Auto-detect gateway from headers if not specified
  if (!gateway) {
    if (req.headers.has("x-signature")) {
      gateway = "lemonsqueezy";
    } else if (req.headers.has("x-admin-secret")) {
      gateway = "manual";
    } else if (
      (req.headers.get("content-type") ?? "").includes(
        "application/x-www-form-urlencoded",
      )
    ) {
      gateway = "kofi";
    } else {
      return jsonResponse(
        { error: "Missing gateway parameter or detection header" },
        400,
      );
    }
  }

  const rawBody = await req.text();

  try {
    switch (gateway) {
      case "lemonsqueezy": {
        // ── LemonSqueezy ────────────────────────────────────────────
        const signature = req.headers.get("x-signature") ?? "";
        const signingSecret = Deno.env.get("LEMON_SIGNING_SECRET") ?? "";

        if (!signature || !signingSecret) {
          return jsonResponse({ error: "Missing signature" }, 401);
        }

        const isValid = await validateSignature(
          rawBody,
          signature,
          signingSecret,
        );
        if (!isValid) {
          console.error("LemonSqueezy: invalid webhook signature");
          return jsonResponse({ error: "Invalid signature" }, 401);
        }

        const credit = await parseLemonSqueezyEvent(rawBody);
        if (!credit) {
          // Event type not relevant (e.g., subscription_updated) — acknowledge
          return jsonResponse({ ok: true, skipped: true }, 200);
        }

        const result = await processCredit(credit);

        // Provision license if product maps to a tier
        const tier = getProductTier(
          String(credit.metadata.product_id ?? ""),
        );
        let licenseKey: string | undefined;
        if (tier && result.success) {
          const license = await provisionLicense(
            credit.user_id,
            tier,
            credit.order_ref,
          );
          licenseKey = license.key;
        }

        return jsonResponse(
          { ...result, license_key: licenseKey },
          result.success ? 200 : 422,
        );
      }

      case "kofi": {
        // ── Ko-fi ────────────────────────────────────────────────────
        const credit = await parseKofiEvent(rawBody);
        if (!credit) {
          return jsonResponse({ ok: true, skipped: true }, 200);
        }

        // Validate verification token from parsed body
        const params = new URLSearchParams(rawBody);
        const dataStr = params.get("data");
        if (dataStr) {
          const data = JSON.parse(dataStr);
          if (!validateKofiToken(data.verification_token ?? "")) {
            return jsonResponse({ error: "Invalid verification token" }, 401);
          }
        }

        const kofiResult = await processCredit(credit);
        return jsonResponse(kofiResult, kofiResult.success ? 200 : 422);
      }

      case "manual": {
        // ── Manual Admin Grant ──────────────────────────────────────
        const adminSecret = req.headers.get("x-admin-secret") ?? "";
        if (!validateAdminSecret(adminSecret)) {
          return jsonResponse({ error: "Unauthorized" }, 401);
        }

        const body = JSON.parse(rawBody);
        const credit = await parseManualGrant(body);
        if (!credit) {
          return jsonResponse({ error: "Invalid manual grant request" }, 400);
        }

        const result = await processCredit(credit);
        return jsonResponse(result, result.success ? 200 : 422);
      }

      default:
        return jsonResponse({ error: `Unknown gateway: ${gateway}` }, 400);
    }
  } catch (err) {
    console.error(`Webhook error (${gateway}):`, err);
    return jsonResponse({ error: "Internal server error" }, 500);
  }
});

function jsonResponse(
  body: Record<string, unknown>,
  status: number,
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...CORS_HEADERS },
  });
}
