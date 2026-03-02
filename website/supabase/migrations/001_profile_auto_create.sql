-- Migration 001: Auto-create a profiles row when a new user signs up.
-- Run this in the Supabase Dashboard SQL Editor (or via `supabase db push`).
--
-- The trigger fires AFTER INSERT on auth.users and creates a default profile
-- with trial defaults (15 000 words, 15-day expiry). Trial fields can be
-- nulled out later when trial is disabled — the profile row itself is the
-- foundation for account management.

CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
BEGIN
  INSERT INTO public.profiles (
    id,
    email,
    name,
    trial_words_quota,
    trial_words_used,
    trial_expires_at,
    created_at,
    updated_at
  ) VALUES (
    NEW.id,
    NEW.email,
    COALESCE(NEW.raw_user_meta_data->>'full_name', NEW.raw_user_meta_data->>'name', ''),
    15000,
    0,
    NOW() + INTERVAL '15 days',
    NOW(),
    NOW()
  );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();
