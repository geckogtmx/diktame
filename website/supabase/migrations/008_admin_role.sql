-- Migration 008: Add admin role to profiles + pending_gifts claim on signup.
--
-- Run via: supabase db push  (or paste into SQL Editor)

-- ═══════════════════════════════════════════════════════════════════════
-- 1. ADD is_admin COLUMN
-- ═══════════════════════════════════════════════════════════════════════

ALTER TABLE public.profiles ADD COLUMN IF NOT EXISTS is_admin BOOLEAN DEFAULT false;

-- ═══════════════════════════════════════════════════════════════════════
-- 2. UPDATE handle_new_user TRIGGER
--    Now also auto-claims pending gifts on signup.
-- ═══════════════════════════════════════════════════════════════════════

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
                1000000 + v_gift.wallet_credit_micro,
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
