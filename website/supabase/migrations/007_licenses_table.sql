-- Migration 007: Licenses table for desktop app license management.
-- Licenses are created by webhook (LemonSqueezy purchase) or admin gift.
-- Also creates pending_gifts table for gifting to non-existing users.
--
-- Run via: supabase db push  (or paste into SQL Editor)

-- ═══════════════════════════════════════════════════════════════════════
-- 1. LICENSES TABLE
-- ═══════════════════════════════════════════════════════════════════════

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

-- ═══════════════════════════════════════════════════════════════════════
-- 2. RLS POLICIES
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE public.licenses ENABLE ROW LEVEL SECURITY;

-- Users can read their own licenses
CREATE POLICY "Users read own licenses"
    ON public.licenses FOR SELECT
    TO authenticated
    USING (auth.uid() = user_id);

-- No INSERT/UPDATE/DELETE policies for authenticated role
-- (service_role only — licenses created by webhooks or admin)

-- ═══════════════════════════════════════════════════════════════════════
-- 3. PENDING GIFTS TABLE (for gifting licenses to non-existing users)
-- ═══════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS public.pending_gifts (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    email               TEXT        NOT NULL,
    license_key         TEXT        NOT NULL,
    tier                TEXT        NOT NULL DEFAULT 'starter'
                        CHECK (tier IN ('starter', 'power')),
    wallet_credit_micro BIGINT      DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    claimed_at          TIMESTAMPTZ              -- NULL until claimed
);

CREATE INDEX IF NOT EXISTS idx_pending_gifts_email
    ON public.pending_gifts (email);

ALTER TABLE public.pending_gifts ENABLE ROW LEVEL SECURITY;

-- Service role only — no user-facing policies
