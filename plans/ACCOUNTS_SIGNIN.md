 # ACCOUNTS_SIGNIN Sprint Plan

## Context

The sign-in flow between the C# app and dikta.me is broken (DEV_HANDOFF: "Sign In broken / redesign needed"). The website dashboard is bare (no wallet info, no license management). JWT expires after ~1hr with no refresh. The standalone backend project at `E:\dIKtame\dikta-backend-&-dashboard` has good UI/UX ideas for a wallet dashboard but will **not** be deployed separately — its features merge into the existing Next.js website on Vercel.

**Decisions:** Wallet-only billing (drop trial quotas). LemonSqueezy for licenses, Ko-fi for donations. Merge all features into dikta.me (Next.js). ImprovMX + Resend for email management.

---

## What Already Exists (verified in code)

### Supabase Infrastructure (fully operational)
- **Migration 002** (`website/supabase/migrations/002_wallet_ledger.sql`): `wallet_ledger` table, `deduct_wallet_balance` + `credit_wallet_balance` RPCs, rate limits, audit log, signup trigger grants $1.00 promo credit
- **Edge Functions**: `wallet-proxy` (STT/LLM routing), `wallet-status` (balance + transactions), `wallet-webhook` (gateway-agnostic crediting with LemonSqueezy + manual adapters)
- **LemonSqueezy adapter** (`wallet-webhook/adapters/lemonsqueezy.ts`): HMAC validation, order parsing — `PRODUCT_CREDIT_MAP` is empty (needs product IDs when LemonSqueezy products are created)
- **Existing migrations**: 001–006 (profiles, wallet, waiting list, waitlist webhook, unique email, invites). Next migration = **007**.

### Website Auth Flow (working but incomplete)
- Login at `/[locale]/login` with Supabase Auth UI (Google, GitHub, magic link)
- `/auth/callback?mode=app` exchanges code, redirects `diktame://auth?token=JWT`
- `/api/auth/app-token` — already-signed-in shortcut to deeplink
- **Problem**: Only `access_token` sent via deeplink — `refresh_token` is discarded (line 51 of `callback/route.ts`)
- **Problem**: Safety-net INSERT still uses stale trial fields (lines 28-39)

### C# App Auth (working but incomplete)
- `AccountService` stores JWT via `SecureStorage` (key: `"trial_token"`), fires `AuthStateChanged`, sets `AuthMode.Wallet`
- `LoadingViewModel.SyncWalletBalanceAsync()` (line 570) syncs balance on startup via `wallet-status` Edge Function
- `WalletDeepgramProxy`/`WalletGeminiProxy` route through `wallet-proxy`, fire `SessionExpired` (line 34) / `BalanceUpdated` (line 40) events
- **Problem**: No refresh token → 1hr session death
- **Problem**: `SyncWalletBalanceAsync` only runs at startup (line 224–228), not after sign-in
- **Problem**: `SessionExpired` events are never subscribed to in UI — only `BalanceUpdated` is wired (line 636–657)

### Key Architecture References
- **Auth callback handler**: `website/app/auth/callback/route.ts` — 71 lines
- **App token shortcut**: `website/app/api/auth/app-token/route.ts` — 18 lines
- **Dual-auth Supabase client**: `website/lib/supabase/api.ts` — supports Bearer (app) + cookie (web)
- **C# deeplink handler**: `src/DiktaMe.App/App.xaml.cs:197-243` — `HandleDeepLink()` method
- **C# account service**: `src/DiktaMe.Core/Account/AccountService.cs` — 102 lines
- **C# JWT decoder**: `src/DiktaMe.Core/Account/JwtDecoder.cs` — has `ExtractEmail()`, `ExtractExpiry()`, needs `ExtractDisplayName()`
- **C# account settings**: `src/DiktaMe.Core/Config/AccountSettings.cs` — has `Email`, `WalletBalanceMicro`, needs `DisplayName`
- **C# DI registration**: `src/DiktaMe.App/App.xaml.cs:440-558` — `AccountService` at line 551, wallet proxies at 452/481

---

## Session 1: W.1–W.7 — Website Auth + API Routes

**Goal**: Fix the auth deeplink to include refresh tokens, clean up stale trial code, add wallet API routes.
**Commit prefix**: `fix(auth):` and `feat(api):`

---

### W.1: Add refresh_token to deeplink

**Why**: The C# app only receives `access_token` today. After ~1hr the JWT expires and the user is silently logged out with no way to refresh.

**File 1**: `website/app/auth/callback/route.ts`

Current code at line 51:
```ts
return NextResponse.redirect(`diktame://auth?token=${session.access_token}`);
```

Change to:
```ts
const deeplink = new URL('diktame://auth');
deeplink.searchParams.set('token', session.access_token);
deeplink.searchParams.set('refresh_token', session.refresh_token);
return NextResponse.redirect(deeplink.toString());
```

**File 2**: `website/app/api/auth/app-token/route.ts`

Current code at line 13:
```ts
return NextResponse.redirect(`diktame://auth?token=${session.access_token}`);
```

Same change — use `URL` + `searchParams.set()` to include `refresh_token`.

**Verification**: Build website locally (`npm run dev`), sign in with `mode=app`, confirm browser redirect URL includes both `token=` and `refresh_token=` query params.

- [ ] Done

---

### W.2: Fix safety-net profile INSERT

**Why**: Lines 28-39 of `callback/route.ts` insert stale trial fields (`trial_words_quota: 15000`, `trial_expires_at: 15 days`). Migration 002's `handle_new_user` trigger already handles this correctly (grants $1.00 wallet credit instead), but the safety-net path bypasses the trigger.

**File**: `website/app/auth/callback/route.ts`

Replace lines 28-40:
```ts
// BEFORE (stale trial fields):
await supabase.from('profiles').insert({
  id: user.id,
  email: user.email,
  name: user.user_metadata?.full_name ?? user.user_metadata?.name ?? '',
  trial_words_quota: 15000,
  trial_words_used: 0,
  trial_expires_at: new Date(Date.now() + 15 * 24 * 60 * 60 * 1000).toISOString(),
});
```

With:
```ts
// AFTER (zeroed trial fields — wallet system replaces trial):
await supabase.from('profiles').insert({
  id: user.id,
  email: user.email,
  name: user.user_metadata?.full_name ?? user.user_metadata?.name ?? '',
  trial_words_quota: 0,
  trial_words_used: 0,
  trial_expires_at: null,
});

// Grant $1.00 promo wallet credit (mirrors handle_new_user trigger logic)
// Uses service-role client to bypass RLS (wallet_ledger has no INSERT policy for authenticated)
const serviceSupabase = createClient(
  process.env.NEXT_PUBLIC_SUPABASE_URL!,
  process.env.SUPABASE_SERVICE_ROLE_KEY!,
  { auth: { persistSession: false } }
);
await serviceSupabase.from('wallet_ledger').insert({
  user_id: user.id,
  amount_micro: 1000000,
  balance_after_micro: 1000000,
  type: 'GRANT',
  expires_at: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString(),
  metadata: { reason: 'signup_promotional', amount_usd: '1.00' },
});
```

**Note**: Add `import { createClient } from '@supabase/supabase-js'` at the top (the existing import is `from '@/lib/supabase/server'` which creates a cookie-based client).

**Verification**: Delete your test profile from Supabase SQL Editor, sign up again, verify `profiles` row has `trial_words_quota=0` and `wallet_ledger` has a $1.00 GRANT row.

- [ ] Done

---

### W.3: Token refresh API route

**Why**: The C# app needs a server-side endpoint to refresh JWTs without knowing the Supabase anon key. This keeps the anon key out of the desktop app binary.

**New file**: `website/app/api/auth/refresh/route.ts`

```ts
import { createClient } from '@supabase/supabase-js';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  try {
    const { refresh_token } = await request.json();

    if (!refresh_token || typeof refresh_token !== 'string') {
      return NextResponse.json({ error: 'Missing refresh_token' }, { status: 400 });
    }

    const supabase = createClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL!,
      process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
      { auth: { persistSession: false } }
    );

    const { data, error } = await supabase.auth.refreshSession({ refresh_token });

    if (error || !data.session) {
      return NextResponse.json({ error: error?.message ?? 'Refresh failed' }, { status: 401 });
    }

    return NextResponse.json({
      access_token: data.session.access_token,
      refresh_token: data.session.refresh_token,
      expires_at: data.session.expires_at, // Unix epoch seconds
    });
  } catch (err) {
    console.error('Token refresh error:', err);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
```

**Verification**: Use `curl -X POST https://dikta.me/api/auth/refresh -H 'Content-Type: application/json' -d '{"refresh_token":"YOUR_RT"}'` — should return new tokens.

**Important**: Supabase rotates refresh tokens on each use. After calling this endpoint, the OLD refresh_token is invalidated. The C# app must store the NEW one atomically.

- [ ] Done

---

### W.4: Wallet status API route

**Why**: Backup endpoint for the C# app. The primary is the `wallet-status` Edge Function, but this Next.js route can be called if the Edge Function is down. Also used by the website dashboard.

**New file**: `website/app/api/wallet/status/route.ts`

```ts
import { createApiClient } from '@/lib/supabase/api';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const supabase = await createApiClient(request);
    const { data: { user }, error: authError } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    // Get latest balance from wallet_ledger (same query as wallet-status Edge Function)
    const { data: row } = await supabase
      .from('wallet_ledger')
      .select('balance_after_micro')
      .eq('user_id', user.id)
      .order('created_at', { ascending: false })
      .limit(1)
      .single();

    return NextResponse.json({
      balance_micro: row?.balance_after_micro ?? 0,
    });
  } catch (error) {
    console.error('Wallet status error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
```

**Note**: Uses `createApiClient(request)` from `lib/supabase/api.ts` (line 11) — already supports both Bearer token (C# app) and cookie (web browser) auth.

**Verification**: `curl https://dikta.me/api/wallet/status -H 'Authorization: Bearer YOUR_JWT'` — returns `{ "balance_micro": 1000000 }`.

- [ ] Done

---

### W.5: Wallet history API route

**Why**: Transaction history for the website dashboard wallet detail page (W.9). Also available to the C# app.

**New file**: `website/app/api/wallet/history/route.ts`

```ts
import { createApiClient } from '@/lib/supabase/api';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const supabase = await createApiClient(request);
    const { data: { user }, error: authError } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    const url = new URL(request.url);
    const limit = Math.min(parseInt(url.searchParams.get('limit') ?? '50', 10), 100);
    const offset = parseInt(url.searchParams.get('offset') ?? '0', 10);

    const { data: transactions, error } = await supabase
      .from('wallet_ledger')
      .select('id, amount_micro, balance_after_micro, type, created_at, expires_at, metadata')
      .eq('user_id', user.id)
      .order('created_at', { ascending: false })
      .range(offset, offset + limit - 1);

    if (error) {
      return NextResponse.json({ error: 'Failed to fetch transactions' }, { status: 500 });
    }

    return NextResponse.json({
      transactions: transactions ?? [],
      limit,
      offset,
    });
  } catch (error) {
    console.error('Wallet history error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
```

**Verification**: `curl 'https://dikta.me/api/wallet/history?limit=5' -H 'Authorization: Bearer YOUR_JWT'` — returns transaction array.

- [ ] Done

---

### W.6: Clean up stale trial routes

**Why**: These routes reference the old trial word-count system. The wallet system replaces it entirely.

**File 1**: `website/app/api/trial/status/route.ts` (currently 65 lines)

Replace the entire file. The new version returns wallet balance instead of word quotas:
```ts
import { createApiClient } from '@/lib/supabase/api';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const supabase = await createApiClient(request);
    const { data: { user }, error: authError } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    // Wallet balance replaces trial word quota
    const { data: row } = await supabase
      .from('wallet_ledger')
      .select('balance_after_micro')
      .eq('user_id', user.id)
      .order('created_at', { ascending: false })
      .limit(1)
      .single();

    const balanceMicro = row?.balance_after_micro ?? 0;

    return NextResponse.json({
      // Legacy fields (zero = trial system disabled)
      wordsUsed: 0,
      wordsQuota: 0,
      daysRemaining: 0,
      expiresAt: null,
      trialActive: false,
      hasCustomKey: false,
      // New wallet fields
      walletBalanceMicro: balanceMicro,
      walletActive: balanceMicro > 10000, // $0.01 minimum (matches deduct_wallet_balance threshold)
    });
  } catch (error) {
    console.error('Trial status error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
```

**File 2**: `website/app/api/trial/usage/route.ts` (currently 133 lines)

Replace entire file with deprecation notice:
```ts
import { NextResponse } from 'next/server';

export async function POST() {
  return NextResponse.json(
    {
      error: 'Gone',
      message: 'Trial usage tracking has been replaced by the wallet system. Use wallet-proxy Edge Function for API access.',
    },
    { status: 410 }
  );
}
```

**Verification**: `GET /api/trial/status` returns `walletBalanceMicro` field. `POST /api/trial/usage` returns 410.

- [ ] Done

---

### W.7: Update profile API

**Why**: The profile API (used by website dashboard + C# app) still returns stale `trialWordsQuota` and `trialExpiresAt` fields. Add wallet balance, remove trial fields.

**File**: `website/app/api/profile/route.ts`

**GET handler** (lines 4-43): Change the response object (lines 30-39).

Before:
```ts
return NextResponse.json({
  id: user.id,
  email: user.email,
  name: profile.name,
  trialWordsQuota: profile.trial_words_quota,
  trialExpiresAt: profile.trial_expires_at,
  hasCustomGeminiKey: !!profile.custom_gemini_key,
  createdAt: profile.created_at,
  updatedAt: profile.updated_at,
});
```

After:
```ts
// Query wallet balance
const { data: walletRow } = await supabase
  .from('wallet_ledger')
  .select('balance_after_micro')
  .eq('user_id', user.id)
  .order('created_at', { ascending: false })
  .limit(1)
  .single();

return NextResponse.json({
  id: user.id,
  email: user.email,
  name: profile.name,
  walletBalanceMicro: walletRow?.balance_after_micro ?? 0,
  hasCustomGeminiKey: !!profile.custom_gemini_key,
  createdAt: profile.created_at,
  updatedAt: profile.updated_at,
});
```

**PATCH handler** (lines 46-105): Same change — replace `trialWordsQuota`/`trialExpiresAt` with `walletBalanceMicro` in the response (lines 91-100). Query wallet balance same way as GET.

**Verification**: `GET /api/profile` returns `walletBalanceMicro` instead of `trialWordsQuota`/`trialExpiresAt`.

- [ ] Done

---

### Session 1 Checklist
- [x] W.1: `refresh_token` in deeplink (2 files)
- [x] W.2: Safety-net profile INSERT fixed + wallet grant
- [x] W.3: `/api/auth/refresh` route created
- [x] W.4: `/api/wallet/status` route created
- [x] W.5: `/api/wallet/history` route created
- [x] W.6: Trial routes cleaned up (status rewritten, usage → 410)
- [x] W.7: Profile API returns wallet balance
- [x] Commit: `feat(auth): add refresh_token to deeplink, wallet API routes, clean up trial [W.1-W.7]` (`12f70c0`)
- [x] Push + verify on Vercel preview deploy

---

## Session 2: W.8–W.12 — Website Dashboard UI

**Goal**: Revamp the bare dashboard with wallet balance, license status, transaction history. Add sidebar navigation.
**Commit prefix**: `feat(dashboard):`

---

### W.8: Dashboard page with wallet + license cards

**Why**: Current dashboard (`website/app/[locale]/dashboard/page.tsx`, 81 lines) only shows email, member-since date, and license tier text. Needs wallet balance, top-up link, and recent activity.

**File**: `website/app/[locale]/dashboard/page.tsx`

Rewrite the page as a server component with 3-card layout:

**Card 1 — Wallet Balance**:
- Query `wallet_ledger` for latest `balance_after_micro` (same pattern as W.4)
- Format: `$X.XX` using `(balanceMicro / 1_000_000).toFixed(2)`
- Color: green if >= $1.00, yellow if $0.50–$0.99, red if < $0.50
- "Top Up" button → external link to Ko-fi/LemonSqueezy store (hardcode URL for now)

**Card 2 — License Status**:
- Query `licenses` table for active license: `.select('tier, status, expires_at').eq('user_id', user.id).eq('status', 'active').single()`
- If found: show tier name (Starter/Power) + expiry date
- If not found: show "Free Version" with upgrade link
- **Note**: `licenses` table doesn't exist yet — it's created in W.10 (Session 5). For now, handle the query error gracefully (show "Free Version" if table doesn't exist)

**Card 3 — Recent Activity**:
- Count of total transactions: `SELECT count(*) FROM wallet_ledger WHERE user_id = ?`
- Last transaction date
- Link to wallet detail page `/dashboard/wallet`

**Design reference**: `E:\dIKtame\dikta-backend-&-dashboard\src\components\Dashboard.tsx` for layout inspiration (3-card grid with glass morphism cards).

**i18n**: Add keys to `messages/en.json` under `DashboardPage` namespace (existing pattern). At minimum: `walletTitle`, `topUpButton`, `licenseTitle`, `activityTitle`, `freeVersion`.

**Verification**: Navigate to `/dashboard` — see 3 cards with real data. Wallet balance matches Supabase.

- [ ] Done

---

### W.9: Wallet detail page

**Why**: Full transaction history with pagination for users who want to see their spending.

**New file**: `website/app/[locale]/dashboard/wallet/page.tsx`

Server component that:
1. Authenticates user (redirect to `/login` if not)
2. Queries `wallet_ledger` with pagination: `.select('id, amount_micro, balance_after_micro, type, created_at, expires_at, metadata').eq('user_id', user.id).order('created_at', { ascending: false }).range(offset, offset + limit - 1)`
3. Renders a table with columns:
   - **Date**: formatted from `created_at`
   - **Type**: GRANT / PURCHASE / USAGE / REFUND (badge-style)
   - **Amount**: `+$X.XX` (green) for positive, `-$X.XX` (red) for negative
   - **Balance**: running balance after transaction
   - **Details**: expand row to show `metadata` JSON (optional)
4. Balance header at top: same color coding as W.8
5. Pagination: Next/Prev buttons, 20 rows per page (use `searchParams` for `?page=N`)

**Verification**: Navigate to `/dashboard/wallet` — see transaction table with correct amounts and colors.

- [ ] Done

---

### W.10: Licenses table migration

**Why**: Need a table to track software licenses (free/starter/power tiers) that the C# app validates.

**New file**: `website/supabase/migrations/007_licenses_table.sql`

```sql
-- Migration 007: Licenses table for desktop app license management.
-- Licenses are created by webhook (LemonSqueezy purchase) or admin gift.

CREATE TABLE IF NOT EXISTS public.licenses (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID        REFERENCES auth.users(id) ON DELETE CASCADE,
    key         TEXT        NOT NULL UNIQUE,
    status      TEXT        NOT NULL DEFAULT 'active'
                CHECK (status IN ('active', 'revoked', 'expired')),
    tier        TEXT        NOT NULL DEFAULT 'free'
                CHECK (tier IN ('free', 'starter', 'power')),
    machine_id  TEXT,                    -- bound on first activation, NULL until then
    order_ref   TEXT,                    -- gateway order ID for dedup
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at  TIMESTAMPTZ              -- NULL = perpetual license
);

-- Fast lookup by user
CREATE INDEX IF NOT EXISTS idx_licenses_user_id
    ON public.licenses (user_id);

-- Lookup by license key (for validation endpoint)
CREATE INDEX IF NOT EXISTS idx_licenses_key
    ON public.licenses (key);

-- RLS: authenticated users read own licenses only
ALTER TABLE public.licenses ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users read own licenses"
    ON public.licenses FOR SELECT
    TO authenticated
    USING (auth.uid() = user_id);

-- No INSERT/UPDATE/DELETE policies for authenticated role
-- (service_role only — licenses created by webhooks or admin)

-- ── Pending Gifts table (for gifting licenses to non-existing users) ──
CREATE TABLE IF NOT EXISTS public.pending_gifts (
    id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    email             TEXT        NOT NULL,
    license_key       TEXT        NOT NULL,
    tier              TEXT        NOT NULL DEFAULT 'starter'
                      CHECK (tier IN ('starter', 'power')),
    wallet_credit_micro BIGINT   DEFAULT 0,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    claimed_at        TIMESTAMPTZ              -- NULL until claimed
);

CREATE INDEX IF NOT EXISTS idx_pending_gifts_email
    ON public.pending_gifts (email);

ALTER TABLE public.pending_gifts ENABLE ROW LEVEL SECURITY;
-- Service role only — no user-facing policies
```

**Deploy**: Run `supabase db push` from `website/` directory, or paste into Supabase SQL Editor.

**Verification**: Check Supabase Table Editor — `licenses` and `pending_gifts` tables exist with correct columns.

- [ ] Done

---

### W.11: Dashboard layout + navigation

**Why**: Current layout (`website/app/[locale]/dashboard/layout.tsx`, 31 lines) only has Navbar + content. Needs sidebar navigation for Dashboard / Wallet / Profile sub-pages.

**File**: `website/app/[locale]/dashboard/layout.tsx`

Add a sidebar with links:
- **Dashboard** → `/dashboard` (icon: home/grid)
- **Wallet** → `/dashboard/wallet` (icon: wallet/credit-card)
- **Profile** → `/dashboard/profile` (icon: user/settings)

Use a flexbox layout:
```
┌────────────────────────────────────────┐
│ Navbar (existing)                      │
├────────┬───────────────────────────────┤
│ Sidebar│ Content (children)            │
│ Links  │                               │
│        │                               │
└────────┴───────────────────────────────┘
```

The sidebar should use `usePathname()` (client component) to highlight the active link. Import `Link` from `@/i18n/navigation` (existing pattern in dashboard page).

**Also update**: `website/app/[locale]/dashboard/profile/page.tsx` — Replace trial quota display with wallet balance. Remove the trial-related state variables (`trialWordsQuota`, `trialExpiresAt`, `daysRemaining`). Add a wallet balance display using the updated `/api/profile` response (which now returns `walletBalanceMicro` from W.7).

**Verification**: Navigate between Dashboard / Wallet / Profile using sidebar links. Active link is highlighted.

- [ ] Done

---

### W.12: Remove COMING_SOON

**Why**: The `NEXT_PUBLIC_COMING_SOON` env var on Vercel shows a "Coming Soon" overlay. Already removed from codebase — just needs deleting from Vercel dashboard.

**Action**: Go to Vercel dashboard → Settings → Environment Variables → Delete `NEXT_PUBLIC_COMING_SOON`.

**No code changes needed.**

- [ ] Done

---

### Session 2 Checklist
- [x] W.8: Dashboard page with 3 wallet/license/activity cards
- [x] W.9: Wallet detail page with transaction history table
- [x] W.10: `007_licenses_table.sql` migration created (**not yet deployed to Supabase**)
- [x] W.11: Dashboard sidebar navigation + profile page updated
- [ ] W.12: `NEXT_PUBLIC_COMING_SOON` deleted from Vercel (manual step)
- [x] Commit: `feat(dashboard): wallet cards, transaction history, sidebar nav, licenses migration [W.8-W.12]` (`600666d`)

---

## Bug Fix: Profile Page "Failed to fetch profile" (discovered 2026-03-21)

**Root cause investigation** (Session 3 pre-work):

The profile page (`website/app/[locale]/dashboard/profile/page.tsx`) is a `'use client'` component that calls `fetch('/api/profile')`. The API route uses `createApiClient(request)` which falls back to `createCookieClient()` for browser requests (cookie-based auth). This code path is **unchanged** from when it was working — `api.ts` was last modified in commit `8aee820` (pre-Session 2).

**Likely cause**: Stale Supabase session cookie. The middleware at `website/middleware.ts` calls `supabase.auth.getUser()` to refresh cookies, but if the refresh token in the cookie is stale (e.g. rotated by another client, or expired), `getUser()` returns an auth error → profile API returns 401 → page shows "Failed to fetch profile".

**Fixes applied**:
1. `website/app/api/profile/route.ts`: `getWalletBalance()` changed `.single()` → `.maybeSingle()` — `.single()` returns an error when no wallet rows exist (new users), `.maybeSingle()` returns null cleanly
2. `website/app/[locale]/dashboard/profile/page.tsx`: Added 401 → redirect to `/login` instead of showing generic error. Removed dead `export const dynamic = 'force-dynamic'` (only applies to server components, not `'use client'`)

- [x] Done

---

## Session 3: A.1, A.3, A.5, W.3 — App Deeplink + Sync + Display Name

**Goal**: C# app accepts refresh token from deeplink, syncs wallet after sign-in, shows display name.
**Commit prefix**: `feat(auth):`
**Depends on**: Session 1 (W.1 deployed — deeplink now includes `refresh_token`)

---

### A.1: Accept refresh_token from deeplink

**Why**: The deeplink now sends `diktame://auth?token=JWT&refresh_token=RT` (from W.1). The C# app needs to extract and store both.

**Step 1 — Update `HandleDeepLink()`**

**File**: `src/DiktaMe.App/App.xaml.cs` (lines 197-243)

Current code at line 218:
```csharp
string? token = query["token"];
```

Add after line 218:
```csharp
string? refreshToken = query["refresh_token"];
```

Current code at line 233:
```csharp
await accountService.HandleAuthCallbackAsync(token).ConfigureAwait(false);
```

Change to:
```csharp
await accountService.HandleAuthCallbackAsync(token, refreshToken).ConfigureAwait(false);
```

**Step 2 — Update `IAccountService` interface**

**File**: `src/DiktaMe.Core/Account/IAccountService.cs` (line 18)

Change:
```csharp
Task HandleAuthCallbackAsync(string token, CancellationToken cancellationToken = default);
```

To:
```csharp
Task HandleAuthCallbackAsync(string token, string? refreshToken = null, CancellationToken cancellationToken = default);
```

**Step 3 — Update `AccountService` implementation**

**File**: `src/DiktaMe.Core/Account/AccountService.cs`

Add constant at line 15 (after `TokenKey`):
```csharp
private const string RefreshTokenKey = "refresh_token";
```

Change method signature at line 55:
```csharp
public async Task HandleAuthCallbackAsync(string token, string? refreshToken = null, CancellationToken cancellationToken = default)
```

Add after line 60 (`_secureStorage.StoreKey(TokenKey, token);`):
```csharp
// Store refresh token for silent JWT renewal
if (!string.IsNullOrWhiteSpace(refreshToken))
{
    _secureStorage.StoreKey(RefreshTokenKey, refreshToken);
}
```

Extract display name from JWT — add after line 63 (`string? email = JwtDecoder.ExtractEmail(token);`):
```csharp
string? displayName = JwtDecoder.ExtractDisplayName(token);
string? avatarUrl = JwtDecoder.ExtractAvatarUrl(token);
```

Update settings write at lines 68-72:
```csharp
await _settings.UpdateAsync(_settings.Current with
{
    AuthMode = AuthMode.Wallet,
    Account = _settings.Current.Account with
    {
        Email = email ?? string.Empty,
        DisplayName = displayName ?? string.Empty,
        AvatarUrl = avatarUrl ?? string.Empty,
    },
}, cancellationToken).ConfigureAwait(false);
```

Update `LogoutAsync()` — add after line 83 (`_secureStorage.DeleteKey(TokenKey);`):
```csharp
_secureStorage.DeleteKey(RefreshTokenKey);
```

**Step 4 — Add `ExtractDisplayName()` and `ExtractAvatarUrl()` to JwtDecoder**

**File**: `src/DiktaMe.Core/Account/JwtDecoder.cs`

Add after `ExtractExpiry()` method (after line 48):
```csharp
/// <summary>
/// Extracts a display name from the JWT. Tries user_metadata.full_name,
/// then user_metadata.name, then falls back to the email prefix.
/// </summary>
public static string? ExtractDisplayName(string jwt)
{
    var payload = DecodePayload(jwt);
    if (payload is null) return null;

    using var doc = JsonDocument.Parse(payload);
    var root = doc.RootElement;

    // Try user_metadata.full_name → user_metadata.name
    if (root.TryGetProperty("user_metadata", out var meta))
    {
        if (meta.TryGetProperty("full_name", out var fn) && fn.ValueKind == JsonValueKind.String)
        {
            string name = fn.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        if (meta.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
        {
            string name = n.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
    }

    // Fall back to email prefix
    if (root.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String)
    {
        string email = emailEl.GetString() ?? "";
        int at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? email[..at] : email;
    }

    return null;
}

/// <summary>
/// Extracts the avatar URL from user_metadata.avatar_url (set by Google/GitHub OAuth).
/// </summary>
public static string? ExtractAvatarUrl(string jwt)
{
    var payload = DecodePayload(jwt);
    if (payload is null) return null;

    using var doc = JsonDocument.Parse(payload);
    if (doc.RootElement.TryGetProperty("user_metadata", out var meta) &&
        meta.TryGetProperty("avatar_url", out var url) &&
        url.ValueKind == JsonValueKind.String)
    {
        return url.GetString();
    }

    return null;
}
```

**Step 5 — Add `DisplayName` and `AvatarUrl` to AccountSettings**

**File**: `src/DiktaMe.Core/Config/AccountSettings.cs` (currently 26 lines)

Add after `Email` property (line 10):
```csharp
/// <summary>Display name from OAuth provider (or email prefix fallback).</summary>
public string DisplayName { get; init; } = string.Empty;

/// <summary>Avatar URL from OAuth provider (Google/GitHub profile picture).</summary>
public string AvatarUrl { get; init; } = string.Empty;
```

**Verification**: Build solution (`dotnet build DiktaMe.sln`). Sign in via browser → app receives deeplink → check settings.json has `DisplayName` and `AvatarUrl` populated.

- [ ] Done

---

### A.3: Wallet sync after sign-in

**Why**: `SyncWalletBalanceAsync()` only runs during startup (line 224-228 of `LoadingViewModel.cs`). If the user signs in after the app has loaded, the wallet balance never syncs.

**File**: `src/DiktaMe.App/App.xaml.cs` (lines 231-233 of `HandleDeepLink()`)

After the `HandleAuthCallbackAsync` call, dispatch wallet sync to UI thread:

Current code at line 233:
```csharp
await accountService.HandleAuthCallbackAsync(token).ConfigureAwait(false);
```

After this line (which will now include `refreshToken` from A.1), add:
```csharp
// Sync wallet balance + refresh HUD after sign-in
var loadingVm = Services.GetRequiredService<LoadingViewModel>();
_ = Task.Run(async () =>
{
    try
    {
        // LoadingViewModel.SyncWalletBalanceAsync is private — expose a public wrapper
        await loadingVm.SyncWalletAfterSignInAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "App: post-sign-in wallet sync failed");
    }
});
```

**Add public method to LoadingViewModel**:

**File**: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

Add after `CacheWalletBalanceAsync()` (after line 630):
```csharp
/// <summary>
/// Public entry point for post-sign-in wallet sync.
/// Called from App.xaml.cs HandleDeepLink after auth callback completes.
/// </summary>
public async Task SyncWalletAfterSignInAsync()
{
    await SyncWalletBalanceAsync();

    // Refresh ControlPanel HUD on UI thread
    _uiDispatcher?.TryEnqueue(() =>
    {
        _controlPanel.LoadFromSettings(_settings.Current);

        // Show toast confirming sign-in
        string email = _accountService.Email ?? "Unknown";
        _notifications.ShowToast($"Signed in as {email}", suppressTts: true);
    });
}
```

**Verification**: Start app (not signed in) → sign in via browser → wallet balance appears in HUD immediately, toast shows "Signed in as user@example.com".

- [ ] Done

---

### A.5: Display name in UserPaneFooter

**Why**: Currently extracts display name from email prefix (line 76-77 of `UserPaneFooter.xaml.cs`). Should use the OAuth display name when available.

**File**: `src/DiktaMe.App/Views/Settings/UserPaneFooter.xaml.cs`

Replace lines 73-77:
```csharp
// BEFORE:
string email = _accountService.Email!;
int atIndex = email.IndexOf('@', StringComparison.Ordinal);
string displayName = atIndex > 0 ? email[..atIndex] : email;
```

With:
```csharp
// AFTER — use OAuth display name if available:
string email = _accountService.Email!;
var settings = App.Current.Services.GetRequiredService<DiktaMe.Core.Config.SettingsManager>();
string displayName = settings.Current.Account.DisplayName;
if (string.IsNullOrWhiteSpace(displayName))
{
    // Fallback to email prefix
    int atIndex = email.IndexOf('@', StringComparison.Ordinal);
    displayName = atIndex > 0 ? email[..atIndex] : email;
}
```

Add `using DiktaMe.Core.Config;` at the top if not present (already has `using DiktaMe.Core.Account;`).

**Verification**: Sign in with Google (which provides `full_name`) → UserPaneFooter shows real name, not email prefix.

- [ ] Done

---

### Session 3 Checklist
- [ ] A.1: `HandleDeepLink` extracts `refresh_token`, `AccountService` stores it, `JwtDecoder` has `ExtractDisplayName`/`ExtractAvatarUrl`, `AccountSettings` has `DisplayName`/`AvatarUrl`
- [ ] A.3: `SyncWalletAfterSignInAsync()` method + called from `HandleDeepLink`
- [ ] A.5: `UserPaneFooter` uses `DisplayName` from settings
- [ ] Build: `dotnet build DiktaMe.sln` — 0 errors
- [ ] Commit: `feat(auth): refresh token storage, post-sign-in wallet sync, display name [A.1,A.3,A.5]`

---

## Session 4: A.2, A.4, T.1 — JWT Refresh Service + Session Handling + Tests

**Goal**: Background JWT refresh, SessionExpired handling, unit tests.
**Commit prefix**: `feat(auth):` and `test(auth):`
**Depends on**: Session 1 (W.3 deployed — `/api/auth/refresh` endpoint), Session 3 (A.1 — refresh token stored)

---

### A.2: JWT refresh service

**Why**: Supabase JWTs expire after ~1 hour. Without refresh, the user is silently logged out. This service refreshes proactively (timer-based) and reactively (on 401).

**New file**: `src/DiktaMe.Core/Account/TokenRefreshService.cs`

```csharp
using System.Net.Http.Json;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.Core.Account;

/// <summary>
/// Background service that refreshes Supabase JWTs before they expire.
/// Timer checks every 5 minutes; refreshes when &lt; 10 min remaining.
/// Also provides reactive refresh (call <see cref="TryRefreshAsync"/> on 401).
/// </summary>
public sealed class TokenRefreshService : IDisposable
{
    private const string TokenKey = "trial_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string RefreshUrl = "https://dikta.me/api/auth/refresh";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(10);

    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly HttpClient _http;
    private Timer? _timer;

    /// <summary>Raised when refresh fails and the user must re-authenticate.</summary>
    public event Action? SessionExpired;

    public TokenRefreshService(SecureStorage secureStorage, SettingsManager settings, HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _settings = settings;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>Starts the background refresh timer.</summary>
    public void Start()
    {
        _timer?.Dispose();
        _timer = new Timer(_ => _ = CheckAndRefreshAsync(), null, CheckInterval, CheckInterval);
        Log.Information("TokenRefreshService: started (check every {Interval})", CheckInterval);
    }

    /// <summary>Stops the background refresh timer.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Checks if the current JWT is near expiry and refreshes if needed.
    /// Called by the timer and can also be called manually.
    /// </summary>
    public async Task CheckAndRefreshAsync()
    {
        try
        {
            string? token = _secureStorage.RetrieveKey(TokenKey);
            if (string.IsNullOrEmpty(token)) return;

            var expiry = JwtDecoder.ExtractExpiry(token);
            if (expiry is null) return;

            var remaining = expiry.Value - DateTimeOffset.UtcNow;
            if (remaining > RefreshThreshold)
            {
                Log.Debug("TokenRefreshService: JWT expires in {Remaining} — no refresh needed", remaining);
                return;
            }

            Log.Information("TokenRefreshService: JWT expires in {Remaining} — refreshing", remaining);
            bool success = await TryRefreshAsync();
            if (!success)
            {
                Log.Warning("TokenRefreshService: proactive refresh failed");
                SessionExpired?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TokenRefreshService: check-and-refresh error");
        }
    }

    /// <summary>
    /// Attempts to refresh the JWT using the stored refresh token.
    /// Returns true on success. On failure, does NOT fire SessionExpired
    /// (caller decides what to do).
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        string? refreshToken = _secureStorage.RetrieveKey(RefreshTokenKey);
        if (string.IsNullOrEmpty(refreshToken))
        {
            Log.Warning("TokenRefreshService: no refresh token available");
            return false;
        }

        try
        {
            var response = await _http.PostAsJsonAsync(
                RefreshUrl,
                new { refresh_token = refreshToken }
            ).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("TokenRefreshService: refresh returned {StatusCode}", (int)response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<RefreshResponse>().ConfigureAwait(false);
            if (result is null || string.IsNullOrEmpty(result.access_token))
            {
                Log.Warning("TokenRefreshService: empty refresh response");
                return false;
            }

            // Store new tokens atomically
            _secureStorage.StoreKey(TokenKey, result.access_token);
            if (!string.IsNullOrEmpty(result.refresh_token))
            {
                _secureStorage.StoreKey(RefreshTokenKey, result.refresh_token);
            }

            // Update cached email/display name from new JWT
            string? email = JwtDecoder.ExtractEmail(result.access_token);
            string? displayName = JwtDecoder.ExtractDisplayName(result.access_token);
            await _settings.UpdateAsync(_settings.Current with
            {
                Account = _settings.Current.Account with
                {
                    Email = email ?? _settings.Current.Account.Email,
                    DisplayName = displayName ?? _settings.Current.Account.DisplayName,
                },
            }).ConfigureAwait(false);

            Log.Information("TokenRefreshService: refreshed successfully — new expiry = {Expiry}",
                JwtDecoder.ExtractExpiry(result.access_token));
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TokenRefreshService: HTTP refresh failed");
            return false;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _http.Dispose();
    }

    // JSON shape returned by POST /api/auth/refresh
    private sealed record RefreshResponse(string access_token, string refresh_token, long? expires_at);
}
```

**DI Registration** — add to `src/DiktaMe.App/App.xaml.cs` after line 552 (after `IAccountService`):
```csharp
services.AddSingleton<TokenRefreshService>();
```

**Start the service** — in `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`, after `WireWalletBalanceEvents()` (line 231):
```csharp
// Start JWT refresh timer (if signed in)
if (_accountService.HasValidToken)
{
    var tokenRefresh = App.Current.Services.GetRequiredService<TokenRefreshService>();
    tokenRefresh.Start();
}
```

Add `TokenRefreshService` to LoadingViewModel constructor (new parameter + field assignment).

**Verification**: Sign in → wait for JWT to approach expiry (or manually shorten `RefreshThreshold` to 50 min for testing) → verify logs show "refreshed successfully".

- [ ] Done

---

### A.4: Handle SessionExpired events

**Why**: `WalletDeepgramProxy.SessionExpired` (line 34 of `WalletDeepgramProxy.cs`) and `WalletGeminiProxy.SessionExpired` fire when the proxy returns 401. Currently nothing subscribes to them.

**File**: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

In `WireWalletBalanceEvents()` (line 636), add after the existing `BalanceUpdated` subscription:

```csharp
// Subscribe to session expiry — attempt refresh before showing error
var walletStt = App.Current.Services.GetRequiredService<WalletDeepgramProxy>();
var tokenRefresh = App.Current.Services.GetRequiredService<TokenRefreshService>();

void HandleSessionExpired()
{
    _ = Task.Run(async () =>
    {
        bool refreshed = await tokenRefresh.TryRefreshAsync();
        if (!refreshed)
        {
            _uiDispatcher?.TryEnqueue(() =>
            {
                _notifications.ShowToast("Session expired. Please sign in again.");
            });
        }
    });
}

walletStt.SessionExpired += HandleSessionExpired;
_walletProxy.SessionExpired += HandleSessionExpired;
tokenRefresh.SessionExpired += HandleSessionExpired;
```

**Note**: `_walletProxy` is the `WalletGeminiProxy` field (line 46). `walletStt` is the `WalletDeepgramProxy` resolved from DI.

**Verification**: Manually invalidate the JWT (change a character in SecureStorage) → perform a dictation → should attempt refresh → if refresh fails, toast "Session expired. Please sign in again."

- [ ] Done

---

### T.1: C# unit tests (~15 new tests)

**Why**: Validate the new JWT decoder methods and account service changes.

**New file**: `src/DiktaMe.Core.Tests/Account/JwtDecoderExtendedTests.cs`

Test cases for `ExtractDisplayName()`:
1. JWT with `user_metadata.full_name` → returns full name
2. JWT with `user_metadata.name` (no full_name) → returns name
3. JWT with email only (no user_metadata) → returns email prefix
4. JWT with empty `user_metadata.full_name` → falls back to `name`
5. Malformed JWT → returns null

Test cases for `ExtractAvatarUrl()`:
1. JWT with `user_metadata.avatar_url` → returns URL string
2. JWT without `avatar_url` → returns null
3. Malformed JWT → returns null

**Helper**: Create JWTs for testing with `Convert.ToBase64String(Encoding.UTF8.GetBytes(json))` — no signature verification needed since `JwtDecoder` is a payload-only decoder.

**New file**: `src/DiktaMe.Core.Tests/Account/AccountServiceExtendedTests.cs`

Test cases for `HandleAuthCallbackAsync` with refresh token:
1. Valid token + refresh token → both stored in SecureStorage
2. Valid token + null refresh → only access token stored
3. Verify `DisplayName` saved to settings
4. Verify `AvatarUrl` saved to settings

**New file**: `src/DiktaMe.Core.Tests/Account/TokenRefreshServiceTests.cs`

Test cases (mock `HttpClient` via `HttpMessageHandler`):
1. `TryRefreshAsync` success → new tokens stored
2. `TryRefreshAsync` 401 response → returns false, tokens unchanged
3. `CheckAndRefreshAsync` skips when expiry > 10 min away
4. `CheckAndRefreshAsync` refreshes when expiry < 10 min away

**Verification**: `dotnet test DiktaMe.sln` — all new tests pass, no regressions.

- [ ] Done

---

### Session 4 Checklist
- [ ] A.2: `TokenRefreshService.cs` created, DI registered, started in LoadingViewModel
- [ ] A.4: `SessionExpired` events wired (both proxies + refresh service)
- [ ] T.1: ~15 unit tests passing
- [ ] Build: `dotnet build DiktaMe.sln` + `dotnet test DiktaMe.sln` — 0 errors
- [ ] Commit: `feat(auth): JWT refresh service with proactive/reactive refresh [A.2,A.4,T.1]`

---

## Session 5: W.10, W.13–W.14 — Licenses Migration + Webhook + Validation

**Goal**: Deploy licenses table, extend webhook for license provisioning, add validation endpoint.
**Commit prefix**: `feat(licenses):`
**Depends on**: Session 2 (W.10 migration deployed — or deploy it here if not done in Session 2)

---

### W.13: Extend wallet-webhook for license provisioning

**Why**: When a user buys a license via LemonSqueezy, the webhook should also create a license row (not just credit wallet).

**Step 1 — Add `provisionLicense()` to core**

**File**: `website/supabase/functions/wallet-webhook/core.ts`

Add after `resolveUserByEmail()` (after line 118):
```ts
/**
 * Generate a formatted license key: DKTM-XXXX-XXXX-XXXX
 */
function generateLicenseKey(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // no I/O/0/1 to avoid confusion
  let key = 'DKTM';
  for (let i = 0; i < 3; i++) {
    key += '-';
    for (let j = 0; j < 4; j++) {
      key += chars[Math.floor(Math.random() * chars.length)];
    }
  }
  return key;
}

/**
 * Provision a license for a user after a purchase.
 * Inserts into the `licenses` table. Idempotent via order_ref.
 */
export async function provisionLicense(
  userId: string,
  tier: string,
  orderRef: string,
): Promise<{ key: string; duplicate: boolean }> {
  const db = createServiceClient();

  // Dedup: check if license already exists for this order
  const { data: existing } = await db
    .from('licenses')
    .select('key')
    .eq('order_ref', orderRef)
    .single();

  if (existing) {
    return { key: existing.key, duplicate: true };
  }

  const key = generateLicenseKey();
  await db.from('licenses').insert({
    user_id: userId,
    key,
    status: 'active',
    tier,
    order_ref: orderRef,
  });

  return { key, duplicate: false };
}
```

**Step 2 — Map LemonSqueezy products to tiers**

**File**: `website/supabase/functions/wallet-webhook/adapters/lemonsqueezy.ts`

Populate `PRODUCT_CREDIT_MAP` (line 8) when LemonSqueezy products are created. For now, add a `PRODUCT_TIER_MAP` alongside it:
```ts
/** Product ID → license tier mapping. */
const PRODUCT_TIER_MAP: Record<string, string> = {
  // Populate when LemonSqueezy products are created:
  // "product_id": "starter" | "power"
};
```

Export a helper to get the tier:
```ts
export function getProductTier(productId: string): string | null {
  return PRODUCT_TIER_MAP[productId] ?? null;
}
```

**Step 3 — Call provisionLicense in webhook handler**

**File**: `website/supabase/functions/wallet-webhook/index.ts`

Add import at top:
```ts
import { provisionLicense } from "./core.ts";
import { getProductTier } from "./adapters/lemonsqueezy.ts";
```

In the `lemonsqueezy` case (after line 83, after `processCredit`):
```ts
const result = await processCredit(credit);

// Provision license if product maps to a tier
const tier = getProductTier(String(credit.metadata.product_id ?? ""));
let licenseKey: string | undefined;
if (tier && result.success) {
  const license = await provisionLicense(credit.user_id, tier, credit.order_ref);
  licenseKey = license.key;
}

return jsonResponse({ ...result, license_key: licenseKey }, result.success ? 200 : 422);
```

**Verification**: Use the manual grant adapter to simulate a purchase → check `licenses` table has a new row.

- [ ] Done

---

### W.14: License validation API route

**Why**: The C# app needs to validate its license key on startup or on-demand.

**New file**: `website/app/api/licenses/validate/route.ts`

```ts
import { createClient } from '@supabase/supabase-js';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  const url = new URL(request.url);
  const key = url.searchParams.get('key');
  const machineId = url.searchParams.get('machine_id');

  if (!key) {
    return NextResponse.json({ error: 'Missing key parameter' }, { status: 400 });
  }

  // Service-role client to read licenses (RLS only allows users to read own)
  const supabase = createClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.SUPABASE_SERVICE_ROLE_KEY!,
    { auth: { persistSession: false } }
  );

  const { data: license, error } = await supabase
    .from('licenses')
    .select('id, user_id, key, status, tier, machine_id, expires_at')
    .eq('key', key)
    .single();

  if (error || !license) {
    return NextResponse.json({ valid: false, error: 'License not found' }, { status: 404 });
  }

  if (license.status !== 'active') {
    return NextResponse.json({ valid: false, error: `License ${license.status}`, tier: license.tier });
  }

  if (license.expires_at && new Date(license.expires_at) < new Date()) {
    // Mark as expired
    await supabase.from('licenses').update({ status: 'expired' }).eq('id', license.id);
    return NextResponse.json({ valid: false, error: 'License expired', tier: license.tier });
  }

  // Bind to machine on first use
  if (machineId && !license.machine_id) {
    await supabase.from('licenses').update({ machine_id: machineId }).eq('id', license.id);
    license.machine_id = machineId;
  }

  // Check machine binding
  if (license.machine_id && machineId && license.machine_id !== machineId) {
    return NextResponse.json({
      valid: false,
      error: 'License bound to different machine',
      tier: license.tier,
      bound_machine_id: license.machine_id,
    });
  }

  return NextResponse.json({
    valid: true,
    tier: license.tier,
    machine_id: license.machine_id,
    expires_at: license.expires_at,
  });
}
```

**Verification**: Insert a test license row in Supabase → `curl 'https://dikta.me/api/licenses/validate?key=DKTM-TEST-XXXX-YYYY'` → returns `{ valid: true, tier: "starter" }`.

- [ ] Done

---

### Session 5 Checklist
- [ ] W.10: Migration 007 deployed (**manual step**: run `007_licenses_table.sql` in Supabase SQL Editor)
- [x] W.13: `provisionLicense()` in core, tier mapping in LemonSqueezy adapter, webhook calls it
- [x] W.14: `/api/licenses/validate` route created
- [ ] Commit: `feat(licenses): license provisioning via webhook + validation endpoint [W.13-W.14]`

---

## Session 6: D.1, D.2, D.7 — Admin Foundation

**Goal**: Admin role, auth guard, overview dashboard page.
**Commit prefix**: `feat(admin):`

---

### D.1: Admin role in profiles + middleware guard

**Step 1 — Migration**

**New file**: `website/supabase/migrations/008_admin_role.sql`

```sql
-- Migration 008: Add admin role to profiles + pending_gifts claim on signup.

-- Add is_admin column (default false)
ALTER TABLE public.profiles ADD COLUMN IF NOT EXISTS is_admin BOOLEAN DEFAULT false;

-- Update handle_new_user trigger to auto-claim pending gifts on signup
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
DECLARE
  v_gift RECORD;
BEGIN
    -- Create profile row
    INSERT INTO public.profiles (
        id, email, name,
        trial_words_quota, trial_words_used, trial_expires_at,
        created_at, updated_at
    ) VALUES (
        NEW.id, NEW.email,
        COALESCE(NEW.raw_user_meta_data->>'full_name', NEW.raw_user_meta_data->>'name', ''),
        0, 0, NULL,
        NOW(), NOW()
    );

    -- Grant $1.00 promotional wallet credit, expires in 90 days
    INSERT INTO public.wallet_ledger (
        user_id, amount_micro, balance_after_micro, type, expires_at, metadata
    ) VALUES (
        NEW.id, 1000000, 1000000, 'GRANT',
        NOW() + INTERVAL '90 days',
        '{"reason":"signup_promotional","amount_usd":"1.00"}'::jsonb
    );

    -- Auto-claim any pending gifts for this email
    FOR v_gift IN
        SELECT * FROM public.pending_gifts
        WHERE email = NEW.email AND claimed_at IS NULL
    LOOP
        -- Create license
        INSERT INTO public.licenses (user_id, key, status, tier)
        VALUES (NEW.id, v_gift.license_key, 'active', v_gift.tier);

        -- Grant wallet credit if specified
        IF v_gift.wallet_credit_micro > 0 THEN
            INSERT INTO public.wallet_ledger (
                user_id, amount_micro, balance_after_micro, type, metadata
            ) VALUES (
                NEW.id,
                v_gift.wallet_credit_micro,
                1000000 + v_gift.wallet_credit_micro,  -- promo + gift
                'GRANT',
                '{"reason":"admin_gift"}'::jsonb
            );
        END IF;

        -- Mark gift as claimed
        UPDATE public.pending_gifts SET claimed_at = NOW() WHERE id = v_gift.id;
    END LOOP;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
```

**Manual step**: After deploying, run in Supabase SQL Editor:
```sql
UPDATE profiles SET is_admin = true WHERE email = 'YOUR_EMAIL@example.com';
```

**Step 2 — Admin layout guard**

**New file**: `website/app/[locale]/admin/layout.tsx`

```tsx
import { createClient } from '@/lib/supabase/server';
import { redirect } from 'next/navigation';
import { Link } from '@/i18n/navigation';

export const dynamic = 'force-dynamic';

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  const supabase = await createClient();
  const { data: { user } } = await supabase.auth.getUser();

  if (!user) redirect('/login');

  const { data: profile } = await supabase
    .from('profiles')
    .select('is_admin')
    .eq('id', user.id)
    .single();

  if (!profile?.is_admin) redirect('/dashboard');

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black text-white">
      <div className="flex">
        {/* Sidebar */}
        <nav className="w-56 min-h-screen border-r border-gray-800 p-4 space-y-2">
          <h2 className="text-lg font-bold mb-4 px-2">Admin</h2>
          <Link href="/admin" className="block px-3 py-2 rounded hover:bg-gray-800">Overview</Link>
          <Link href="/admin/users" className="block px-3 py-2 rounded hover:bg-gray-800">Users</Link>
          <Link href="/admin/licenses" className="block px-3 py-2 rounded hover:bg-gray-800">Licenses</Link>
          <Link href="/admin/sales" className="block px-3 py-2 rounded hover:bg-gray-800">Sales</Link>
          <Link href="/admin/support" className="block px-3 py-2 rounded hover:bg-gray-800">Support</Link>
        </nav>
        {/* Content */}
        <main className="flex-1 p-8">{children}</main>
      </div>
    </div>
  );
}
```

**Verification**: Navigate to `/admin` as non-admin → redirected to `/dashboard`. As admin → see sidebar.

- [ ] Done

---

### D.7: Admin API auth guard utility

**Why**: All `/api/admin/*` routes need the same auth check. Extract to a reusable function.

**New file**: `website/lib/admin.ts`

```ts
import { createApiClient } from '@/lib/supabase/api';
import { createClient } from '@supabase/supabase-js';

/**
 * Validates that the request comes from an authenticated admin user.
 * Returns the user + a service-role Supabase client (bypasses RLS for admin queries).
 * Throws Response on auth failure (caller should catch and return it).
 */
export async function requireAdmin(request: Request) {
  const supabase = await createApiClient(request);
  const { data: { user }, error } = await supabase.auth.getUser();

  if (error || !user) {
    throw new Response(JSON.stringify({ error: 'Unauthorized' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  const { data: profile } = await supabase
    .from('profiles')
    .select('is_admin')
    .eq('id', user.id)
    .single();

  if (!profile?.is_admin) {
    throw new Response(JSON.stringify({ error: 'Forbidden' }), {
      status: 403,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  // Return service-role client for admin operations (bypasses RLS)
  const serviceClient = createClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.SUPABASE_SERVICE_ROLE_KEY!,
    { auth: { persistSession: false } }
  );

  return { user, supabase: serviceClient };
}
```

**Usage pattern** in admin API routes:
```ts
export async function GET(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);
    // ... admin-only queries using service-role client
  } catch (error) {
    if (error instanceof Response) return error;
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
```

- [ ] Done

---

### D.2: Admin Overview page

**New file**: `website/app/[locale]/admin/page.tsx`

Server component with KPI cards:

1. **Total Users**: `SELECT count(*) FROM profiles` via service-role client
2. **Total Revenue**: `SELECT COALESCE(SUM(amount_micro), 0) FROM wallet_ledger WHERE type = 'PURCHASE'`
3. **Active Licenses**: `SELECT count(*) FROM licenses WHERE status = 'active'` (handle table-not-exists gracefully)
4. **Outstanding Credits**: Aggregate latest balance per user (complex query — simplify to `SUM(amount_micro) FROM wallet_ledger WHERE amount_micro > 0`)
5. **Recent Signups**: Last 10 from `profiles` ordered by `created_at DESC`
6. **Vercel Analytics**: Link to `https://vercel.com/YOUR_TEAM/dikta-me/analytics` (no inline embed without Pro plan)

**Note**: Admin pages use service-role Supabase client to bypass RLS. Create the client inline:
```ts
const supabase = createClient(
  process.env.NEXT_PUBLIC_SUPABASE_URL!,
  process.env.SUPABASE_SERVICE_ROLE_KEY!,
  { auth: { persistSession: false } }
);
```

Format microdollars as USD: `(micro / 1_000_000).toFixed(2)`.

**Verification**: Navigate to `/admin` as admin → see KPI cards with real data from Supabase.

- [ ] Done

---

### Session 6 Checklist
- [x] D.1: Migration 008 created, admin layout guard with `is_admin` check (**manual step**: deploy migration + set `is_admin=true`)
- [x] D.7: `lib/admin.ts` with `requireAdmin()` utility
- [x] D.2: Admin overview page with KPI cards + recent signups
- [ ] Commit: `feat(admin): admin role, layout guard, overview dashboard [D.1,D.2,D.7]`

---

## Session 7: D.3, D.4 — Admin Sales + Users Pages

**Goal**: Sales data from LemonSqueezy/Ko-fi APIs, user management page.
**Commit prefix**: `feat(admin):`
**Depends on**: Session 6 (admin layout + guard in place)

---

### D.3: Sales page — LemonSqueezy + Ko-fi

**New file**: `website/app/[locale]/admin/sales/page.tsx`
**New file**: `website/app/api/admin/sales/route.ts`

**API route** (`route.ts`):
- Use `requireAdmin(request)` guard
- Fetch LemonSqueezy orders: `GET https://api.lemonsqueezy.com/v1/orders` with `Authorization: Bearer ${process.env.LEMON_SQUEEZY_API_KEY}`, `Accept: application/vnd.api+json`
- LemonSqueezy response follows JSON:API spec: `data[].attributes.{total, status, created_at, user_email}`
- Ko-fi: Try `GET https://ko-fi.com/api/v1/donations?access_token=${process.env.KOFI_API_TOKEN}` — if not available, skip (Ko-fi API is limited)
- Cache with Next.js `unstable_cache` or `revalidate: 300` (5 min)
- Return combined sales summary: `{ lemonSqueezy: { orders, totalRevenue, count }, kofi: { donations, totalDonations, count } }`

**Page** (`page.tsx`):
- Fetch from `/api/admin/sales` using `fetch()` on server side
- Two sections: LemonSqueezy Orders table + Ko-fi Donations table
- Summary cards: Total Revenue, Order Count, Average Order Value
- Handle empty data gracefully (show "No orders yet" / "Configure API key")

**Env vars needed**: `LEMON_SQUEEZY_API_KEY` (add to `.env.local.example` and Vercel)

**Verification**: Navigate to `/admin/sales` → see LemonSqueezy orders (or "Configure API key" message if key not set).

- [ ] Done

---

### D.4: Users management page

**New file**: `website/app/[locale]/admin/users/page.tsx`
**New file**: `website/app/api/admin/users/route.ts`

**API route** (`route.ts`):
- Use `requireAdmin(request)` guard
- Query `profiles` with service-role client (bypasses RLS)
- Support `?search=EMAIL&page=1&limit=20` query params
- For each user, also query latest `wallet_ledger.balance_after_micro`
- Return: `{ users: [{ id, email, name, walletBalance, isAdmin, createdAt }], total, page, limit }`

**Page** (`page.tsx`):
- Search bar (by email)
- Users table: Email, Name, Wallet Balance, Signup Date, Admin badge
- Pagination: Next/Prev with page numbers
- Click row → expandable detail showing wallet transactions + licenses for that user
  - Wallet: last 10 transactions from `wallet_ledger`
  - Licenses: all from `licenses` table

**Verification**: Navigate to `/admin/users` → see user list with wallet balances. Search by email works.

- [ ] Done

---

### Session 7 Checklist
- [x] D.3: Sales page + API route (LemonSqueezy + Ko-fi data)
- [x] D.4: Users page + API route (search, pagination, user detail)
- [x] `.env.local.example` updated with `LEMON_SQUEEZY_API_KEY`
- [ ] Commit: `feat(admin): sales dashboard + user management [D.3,D.4]`

---

## Session 8: D.5, D.8 — Admin License Gifting + Ko-fi Webhook

**Goal**: Gift licenses to users (existing + future), Ko-fi donation webhook.
**Commit prefix**: `feat(admin):`

---

### D.5: Licenses management + gift licenses

**New file**: `website/app/[locale]/admin/licenses/page.tsx`
**New file**: `website/app/api/admin/licenses/route.ts`
**New file**: `website/app/api/admin/licenses/gift/route.ts`

**License list API** (`route.ts`):
- Use `requireAdmin(request)` guard
- GET: Query all `licenses` with service-role client, join `profiles.email` on `user_id`
- Support `?status=active|revoked|expired` filter
- DELETE (revoke): Set `status='revoked'` by license `id`

**Gift API** (`gift/route.ts`):
- Use `requireAdmin(request)` guard
- POST body: `{ email, tier, walletCreditMicro? }`
- Logic:
  1. Look up `profiles` by email
  2. If user exists:
     - Generate license key (`DKTM-XXXX-XXXX-XXXX` — same algorithm as `core.ts`)
     - Insert into `licenses` (user_id, key, tier, status='active')
     - If `walletCreditMicro > 0`: call `credit_wallet_balance` RPC with `type='GRANT'`, `metadata: { reason: 'admin_gift', gifted_by: 'admin' }`
  3. If user doesn't exist:
     - Generate license key
     - Insert into `pending_gifts` (email, license_key, tier, wallet_credit_micro)
     - Return `{ success: true, pending: true, message: 'Gift will be claimed when user signs up' }`
  4. Optionally send email via Resend (if `RESEND_API_KEY` is configured):
     ```ts
     await fetch('https://api.resend.com/emails', {
       method: 'POST',
       headers: {
         'Authorization': `Bearer ${process.env.RESEND_API_KEY}`,
         'Content-Type': 'application/json',
       },
       body: JSON.stringify({
         from: 'dIKta.me <noreply@dikta.me>',
         to: email,
         subject: 'You received a dIKta.me license!',
         html: `<p>You've been gifted a ${tier} license for dIKta.me! Sign in at https://dikta.me to claim it.</p>`,
       }),
     });
     ```

**Page** (`page.tsx`):
- Two sections: License List (table) + Gift Form
- License table: Key, User Email, Tier, Status, Machine ID, Created, Revoke button
- Gift form: Email input, Tier dropdown (starter/power), Wallet credit input (optional, in USD), Submit button
- Show pending gifts in a separate section

**Verification**: Gift a license to a test email → see it in license list. If user exists, verify `licenses` + `wallet_ledger` rows. If user doesn't exist, verify `pending_gifts` row.

- [ ] Done

---

### D.8: Ko-fi webhook adapter

**Why**: Ko-fi sends webhooks on donations. This adapter converts them to wallet credits.

**New file**: `website/supabase/functions/wallet-webhook/adapters/kofi.ts`

```ts
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
```

**Register in webhook router** — `website/supabase/functions/wallet-webhook/index.ts`:

Add import:
```ts
import { parseKofiEvent, validateKofiToken } from "./adapters/kofi.ts";
```

Add to auto-detect (after line 44, inside the `if (!gateway)` block):
```ts
// Check for Ko-fi (sends form-encoded data with verification_token in body)
// Ko-fi doesn't have a distinctive header, so check content-type
const contentType = req.headers.get("content-type") ?? "";
if (contentType.includes("application/x-www-form-urlencoded")) {
  gateway = "kofi";
}
```

Add case in the switch (after the `manual` case):
```ts
case "kofi": {
  // ── Ko-fi ────────────────────────────────────────────────
  const credit = await parseKofiEvent(rawBody);
  if (!credit) {
    return jsonResponse({ ok: true, skipped: true }, 200);
  }

  // Validate token (included in the parsed body)
  const params = new URLSearchParams(rawBody);
  const dataStr = params.get("data");
  if (dataStr) {
    const data = JSON.parse(dataStr);
    if (!validateKofiToken(data.verification_token ?? "")) {
      return jsonResponse({ error: "Invalid verification token" }, 401);
    }
  }

  const result = await processCredit(credit);
  return jsonResponse(result, result.success ? 200 : 422);
}
```

**Verification**: Simulate a Ko-fi webhook with `curl -X POST -d 'data={"type":"Donation","email":"test@test.com","amount":"5.00","kofi_transaction_id":"abc123","verification_token":"YOUR_TOKEN"}' https://YOUR_SUPABASE.supabase.co/functions/v1/wallet-webhook?gateway=kofi`

- [ ] Done

---

### Session 8 Checklist
- [x] D.5: License management page + gift form + pending gifts
- [x] D.8: Ko-fi webhook adapter + registered in router
- [x] `.env.local.example` updated with `KOFI_VERIFICATION_TOKEN`, `RESEND_API_KEY`
- [ ] Commit: `feat(admin): license gifting + Ko-fi webhook adapter [D.5,D.8]`

---

## Session 9: D.6, D.9, T.2, T.3 — Support Placeholder + Testing

**Goal**: Finish admin pages, configure env vars, manual testing, E2E sign-in flow.
**Commit prefix**: `feat(admin):` and `test(e2e):`

---

### D.6: Support tickets placeholder

**New file**: `website/app/[locale]/admin/support/page.tsx`

Simple placeholder page:
- Heading: "Support"
- Message: "Support ticket system — coming soon"
- Link to email inbox: "For now, check support emails via ImprovMX → your email"
- Future: integrate with GitHub Issues API or a simple ticket table

- [ ] Done

---

### D.9: Environment variables to add

Add to `website/.env.local.example`:
```env
# Admin Dashboard — LemonSqueezy API (for sales data)
LEMON_SQUEEZY_API_KEY=

# Ko-fi Webhooks
KOFI_VERIFICATION_TOKEN=
KOFI_API_TOKEN=
```

Add the actual values to Vercel dashboard → Settings → Environment Variables.

**Also add Supabase Edge Function secrets** (if not already):
```bash
supabase secrets set KOFI_VERIFICATION_TOKEN=YOUR_TOKEN
```

- [ ] Done

---

### T.2: Website API manual testing

Test each endpoint with `curl` or browser. Check both Bearer auth (C# app) and cookie auth (web):

| # | Endpoint | Method | Expected Result |
|---|----------|--------|-----------------|
| 1 | `/api/wallet/status` | GET + Bearer | `{ balance_micro: 1000000 }` |
| 2 | `/api/wallet/history?limit=5` | GET + Bearer | Transaction array |
| 3 | `/api/trial/status` | GET + Bearer | `walletBalanceMicro` field, `trialActive: false` |
| 4 | `/api/trial/usage` | POST | 410 Gone |
| 5 | `/api/auth/refresh` | POST + body | New tokens |
| 6 | `/api/profile` | GET + Bearer | `walletBalanceMicro` field, no `trialWordsQuota` |
| 7 | `/api/licenses/validate?key=DKTM-...` | GET | `{ valid: true, tier: "..." }` |
| 8 | `/api/admin/users` | GET + admin Bearer | User list |
| 9 | `/api/admin/sales` | GET + admin Bearer | Sales data |
| 10 | `/api/admin/licenses/gift` | POST + admin Bearer | Gift created |

- [ ] All 10 endpoints verified

---

### T.3: E2E sign-in flow

Full end-to-end test of the complete flow:

| Step | Action | Expected |
|------|--------|----------|
| 1 | Click "Sign In" in C# app | Browser opens `https://dikta.me/login?mode=app` |
| 2 | Authenticate (Google/GitHub) | Browser redirects to `diktame://auth?token=JWT&refresh_token=RT` |
| 3 | App receives deeplink | `HandleDeepLink()` fires → `HandleAuthCallbackAsync(token, refreshToken)` |
| 4 | Check settings.json | `AuthMode: "Wallet"`, `Email` populated, `DisplayName` populated |
| 5 | Check UserPaneFooter | Shows real name (not email prefix), avatar initial |
| 6 | Check wallet HUD | Balance appears in ControlPanel badge |
| 7 | Wait ~50min | TokenRefreshService logs "refreshed successfully" |
| 8 | Perform dictation | Wallet proxy works, balance decrements |
| 9 | Check balance after dictation | HUD updates via `BalanceUpdated` event |
| 10 | Invalidate refresh token | Toast: "Session expired. Please sign in again." |
| 11 | Sign out in app | Settings cleared, UserPaneFooter shows "Sign in" |
| 12 | Check website dashboard | Wallet balance matches, transaction history accurate |

- [ ] All 12 steps verified

---

### Session 9 Checklist
- [x] D.6: Support placeholder page
- [x] D.9: All env vars added to `.env.local.example` (**manual step**: add values to Vercel + Supabase)
- [ ] T.2: All 10 API endpoints verified (**manual testing step**)
- [ ] T.3: Full E2E sign-in flow verified (**manual testing step**)
- [ ] Commit: `feat(admin): support placeholder + env vars [D.6,D.9]`

---

## Risk Areas

| Risk | Mitigation |
|------|------------|
| Deeplink URL length (JWT ~800 chars + refresh ~300 chars) | Windows handles 2048+ chars. If issues, use code-exchange flow instead |
| Supabase refresh_token rotation (old token invalidated per use) | Store new tokens atomically in `TryRefreshAsync()`. Single-writer pattern (timer-based, not concurrent) |
| NRE on null DisplayName (existing users) | Default `= string.Empty` in `AccountSettings` property initializer. `SanitizeNulls()` in `SettingsManager` already handles null sub-objects |
| Cross-thread UI update after sign-in | Use existing `DispatcherQueue.TryEnqueue()` pattern (established in `WireWalletBalanceEvents`) |
| Admin route exposure | `is_admin` check in both layout (page-level) and API routes (`requireAdmin`). Service-role client only in admin APIs |
| LemonSqueezy/Ko-fi API rate limits | Cache responses with `revalidate: 300` (5min). Admin pages are low-traffic |
| Gift to non-existing user | `pending_gifts` table with auto-claim on signup via updated `handle_new_user` trigger |
| Profile safety-net INSERT race with trigger | Both insert zeroed trial fields + $1.00 wallet grant. `credit_wallet_balance` RPC has dedup via `order_ref` — use `signup_promo_{user_id}` as order_ref in both paths |

## Execution Order

| Session | Tasks | Scope | Dependencies |
|---------|-------|-------|-------------|
| 1 | W.1–W.7 | Website auth + API routes | None |
| 2 | W.8–W.12 | Website dashboard UI | None (W.10 handles missing `licenses` table gracefully) |
| 3 | A.1, A.3, A.5 | App deeplink + sync + display name | Session 1 deployed (W.1 deeplink has refresh_token) |
| 4 | A.2, A.4, T.1 | JWT refresh service + session handling + tests | Session 1 (W.3 refresh endpoint) + Session 3 (A.1 stores refresh token) |
| 5 | W.13–W.14 | License webhook + validation | Session 2 (W.10 migration) |
| 6 | D.1, D.2, D.7 | Admin foundation | None |
| 7 | D.3, D.4 | Admin sales + users | Session 6 (admin layout + guard) |
| 8 | D.5, D.8 | License gifting + Ko-fi webhook | Session 6 + Session 5 (licenses table) |
| 9 | D.6, D.9, T.2, T.3 | Support + env vars + testing | All prior sessions |

---

## Bonus: OAuth Signup Admin Notification

**Status**: Not started
**Priority**: Low (nice-to-have)
**Discovered**: 2026-03-21 — Kevin Notar signed up via Google OAuth, no admin notification was sent (only waitlist signups trigger notifications)

**Problem**: The `handle_new_user` trigger (migration 008) creates profiles + grants wallet credit on OAuth signup, but does NOT send any email notification. The waitlist welcome email + admin notification only fires on `waiting_list` INSERT (migration 004 trigger). This means organic signups via the login page go unnoticed.

**Proposed fix**: Add a `pg_net.http_post()` call to the `handle_new_user` trigger that calls a new `signup-notification` Edge Function (or reuses `waitlist-welcome` with a different payload shape). The Edge Function sends an admin-only email via Resend: "New User Signup: {name} ({email}) via {provider}".
