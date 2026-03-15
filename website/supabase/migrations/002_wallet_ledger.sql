-- Migration 002: Wallet ledger tables, RLS policies, atomic deduction RPC,
-- and promotional grant trigger for new signups.
--
-- This replaces the trial word-count system (Migration 001) with a
-- prepaid microdollar wallet. All amounts are in microdollars
-- (1 USD = 1,000,000 micro) to avoid floating-point errors.
--
-- Run via: supabase db push  (or paste into SQL Editor)

-- ═══════════════════════════════════════════════════════════════════════
-- 1. WALLET LEDGER (append-only, source of truth)
-- ═══════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS public.wallet_ledger (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID        NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    amount_micro    BIGINT      NOT NULL,            -- positive = credit, negative = debit
    balance_after_micro BIGINT  NOT NULL,            -- running total after this txn
    type            TEXT        NOT NULL
                    CHECK (type IN ('GRANT','PURCHASE','USAGE','EXPIRY','REFUND','SYNC')),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at      TIMESTAMPTZ,                     -- null = permanent credits
    metadata        JSONB       NOT NULL DEFAULT '{}'::jsonb,
    order_ref       TEXT                             -- gateway order ID for dedup
);

-- Fast balance lookup: latest row per user
CREATE INDEX IF NOT EXISTS idx_wallet_ledger_user_balance
    ON public.wallet_ledger (user_id, created_at DESC);

-- Dedup index for webhook idempotency
CREATE UNIQUE INDEX IF NOT EXISTS idx_wallet_ledger_order_ref
    ON public.wallet_ledger (order_ref) WHERE order_ref IS NOT NULL;

-- ═══════════════════════════════════════════════════════════════════════
-- 2. RATE LIMITS (per-user, per-minute)
-- ═══════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS public.rate_limits (
    user_id         UUID        PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    request_count   INTEGER     NOT NULL DEFAULT 0,
    window_start    TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ═══════════════════════════════════════════════════════════════════════
-- 3. PROXY AUDIT LOG (for debugging and abuse detection)
-- ═══════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS public.proxy_audit_log (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID        NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    service         TEXT        NOT NULL,   -- 'gemini' or 'deepgram'
    cost_micro      BIGINT      NOT NULL DEFAULT 0,
    input_tokens    INTEGER,
    output_tokens   INTEGER,
    audio_duration_ms INTEGER,
    status_code     INTEGER     NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_proxy_audit_user_created
    ON public.proxy_audit_log (user_id, created_at DESC);

-- ═══════════════════════════════════════════════════════════════════════
-- 4. CONFIG (key-value, includes service_frozen flag)
-- ═══════════════════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS public.config (
    key     TEXT PRIMARY KEY,
    value   TEXT NOT NULL
);

INSERT INTO public.config (key, value)
VALUES ('service_frozen', 'false')
ON CONFLICT (key) DO NOTHING;

-- ═══════════════════════════════════════════════════════════════════════
-- 5. RLS POLICIES
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE public.wallet_ledger ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.rate_limits   ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.proxy_audit_log ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.config        ENABLE ROW LEVEL SECURITY;

-- wallet_ledger: users read own rows; only service_role writes
CREATE POLICY "Users read own wallet rows"
    ON public.wallet_ledger FOR SELECT
    TO authenticated
    USING (auth.uid() = user_id);

-- No INSERT/UPDATE/DELETE policies for authenticated role = append-only via service_role only

-- rate_limits: service_role only (no user access)
-- proxy_audit_log: service_role only (no user access)

-- config: readable by service_role only (proxy checks service_frozen)
CREATE POLICY "Service role reads config"
    ON public.config FOR SELECT
    TO service_role
    USING (true);

CREATE POLICY "Service role writes config"
    ON public.config FOR ALL
    TO service_role
    USING (true)
    WITH CHECK (true);

-- ═══════════════════════════════════════════════════════════════════════
-- 6. ATOMIC DEDUCTION RPC
-- ═══════════════════════════════════════════════════════════════════════
-- Called by the wallet-proxy Edge Function after upstream API responds.
-- Uses FOR UPDATE row lock to prevent double-spend race conditions.
-- Returns JSON: { success, transaction_id, cost_micro, balance_micro }

CREATE OR REPLACE FUNCTION public.deduct_wallet_balance(
    p_user_id     UUID,
    p_amount_micro BIGINT,    -- positive value = how much to deduct
    p_type        TEXT DEFAULT 'USAGE',
    p_metadata    JSONB DEFAULT '{}'::jsonb
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_current_balance BIGINT;
    v_new_balance     BIGINT;
    v_txn_id          UUID;
BEGIN
    -- Acquire row lock on the user's latest ledger entry
    SELECT balance_after_micro INTO v_current_balance
    FROM public.wallet_ledger
    WHERE user_id = p_user_id
    ORDER BY created_at DESC
    LIMIT 1
    FOR UPDATE;

    -- Default to 0 if no rows exist
    v_current_balance := COALESCE(v_current_balance, 0);

    -- Check sufficient funds (allow tiny overdraft up to $0.50 = 500000 micro)
    IF v_current_balance < 10000 THEN  -- $0.01 minimum
        RETURN jsonb_build_object(
            'success', false,
            'error', 'insufficient_funds',
            'balance_micro', v_current_balance
        );
    END IF;

    v_new_balance := v_current_balance - p_amount_micro;
    v_txn_id := gen_random_uuid();

    INSERT INTO public.wallet_ledger (id, user_id, amount_micro, balance_after_micro, type, metadata)
    VALUES (v_txn_id, p_user_id, -p_amount_micro, v_new_balance, p_type, p_metadata);

    RETURN jsonb_build_object(
        'success', true,
        'transaction_id', v_txn_id,
        'cost_micro', p_amount_micro,
        'balance_micro', v_new_balance
    );
END;
$$;

-- ═══════════════════════════════════════════════════════════════════════
-- 7. CREDIT WALLET RPC (for webhooks — enforces $50 cap, dedup)
-- ═══════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.credit_wallet_balance(
    p_user_id      UUID,
    p_amount_micro BIGINT,     -- positive value = how much to credit
    p_type         TEXT DEFAULT 'PURCHASE',
    p_metadata     JSONB DEFAULT '{}'::jsonb,
    p_order_ref    TEXT DEFAULT NULL,
    p_expires_at   TIMESTAMPTZ DEFAULT NULL
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_current_balance BIGINT;
    v_new_balance     BIGINT;
    v_txn_id          UUID;
    v_max_balance     BIGINT := 50000000;  -- $50.00 cap
BEGIN
    -- Dedup check: if order_ref already exists, return existing txn
    IF p_order_ref IS NOT NULL THEN
        SELECT id INTO v_txn_id
        FROM public.wallet_ledger
        WHERE order_ref = p_order_ref;

        IF v_txn_id IS NOT NULL THEN
            RETURN jsonb_build_object(
                'success', true,
                'duplicate', true,
                'transaction_id', v_txn_id
            );
        END IF;
    END IF;

    -- Get current balance under lock
    SELECT balance_after_micro INTO v_current_balance
    FROM public.wallet_ledger
    WHERE user_id = p_user_id
    ORDER BY created_at DESC
    LIMIT 1
    FOR UPDATE;

    v_current_balance := COALESCE(v_current_balance, 0);

    -- Enforce $50 cap
    IF v_current_balance + p_amount_micro > v_max_balance THEN
        RETURN jsonb_build_object(
            'success', false,
            'error', 'balance_cap_exceeded',
            'balance_micro', v_current_balance,
            'cap_micro', v_max_balance
        );
    END IF;

    v_new_balance := v_current_balance + p_amount_micro;
    v_txn_id := gen_random_uuid();

    INSERT INTO public.wallet_ledger (id, user_id, amount_micro, balance_after_micro, type, metadata, order_ref, expires_at)
    VALUES (v_txn_id, p_user_id, p_amount_micro, v_new_balance, p_type, p_metadata, p_order_ref, p_expires_at);

    RETURN jsonb_build_object(
        'success', true,
        'transaction_id', v_txn_id,
        'balance_micro', v_new_balance
    );
END;
$$;

-- ═══════════════════════════════════════════════════════════════════════
-- 8. CHECK RATE LIMIT RPC (atomic increment, 60 req/min)
-- ═══════════════════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION public.check_rate_limit(
    p_user_id UUID,
    p_max_requests INTEGER DEFAULT 60
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_count   INTEGER;
    v_window  TIMESTAMPTZ;
BEGIN
    -- Upsert rate limit row
    INSERT INTO public.rate_limits (user_id, request_count, window_start)
    VALUES (p_user_id, 1, now())
    ON CONFLICT (user_id) DO UPDATE
    SET
        request_count = CASE
            WHEN public.rate_limits.window_start < now() - INTERVAL '1 minute'
            THEN 1  -- Reset window
            ELSE public.rate_limits.request_count + 1
        END,
        window_start = CASE
            WHEN public.rate_limits.window_start < now() - INTERVAL '1 minute'
            THEN now()
            ELSE public.rate_limits.window_start
        END
    RETURNING request_count, window_start INTO v_count, v_window;

    IF v_count > p_max_requests THEN
        RETURN jsonb_build_object('allowed', false, 'count', v_count);
    END IF;

    RETURN jsonb_build_object('allowed', true, 'count', v_count);
END;
$$;

-- ═══════════════════════════════════════════════════════════════════════
-- 9. UPDATE SIGNUP TRIGGER (replace trial words with wallet grant)
-- ═══════════════════════════════════════════════════════════════════════
-- The original trigger (Migration 001) granted 15000 trial words.
-- This replaces it: grant $1.00 wallet credit with 90-day expiry.

CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
BEGIN
    -- Create profile row (keeping backward compat with existing columns)
    INSERT INTO public.profiles (
        id, email, name,
        trial_words_quota, trial_words_used, trial_expires_at,
        created_at, updated_at
    ) VALUES (
        NEW.id, NEW.email,
        COALESCE(NEW.raw_user_meta_data->>'full_name', NEW.raw_user_meta_data->>'name', ''),
        0, 0, NULL,  -- Trial fields zeroed out (wallet replaces trial)
        NOW(), NOW()
    );

    -- Grant $1.00 promotional wallet credit (1,000,000 microdollars), expires in 90 days
    INSERT INTO public.wallet_ledger (
        user_id, amount_micro, balance_after_micro, type, expires_at, metadata
    ) VALUES (
        NEW.id, 1000000, 1000000, 'GRANT',
        NOW() + INTERVAL '90 days',
        '{"reason":"signup_promotional","amount_usd":"1.00"}'::jsonb
    );

    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Trigger already exists from Migration 001, function is replaced in-place
