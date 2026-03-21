-- Migration 009: Add avatar_url column to profiles for custom profile pictures.
-- Email/password users upload via the website crop editor.
-- OAuth users get their avatar_url auto-populated from user_metadata at signup.

-- 1. Add the column
ALTER TABLE public.profiles ADD COLUMN IF NOT EXISTS avatar_url TEXT;

-- 2. Backfill existing OAuth users who already have an avatar_url in auth metadata
UPDATE public.profiles p
SET avatar_url = u.raw_user_meta_data->>'avatar_url'
FROM auth.users u
WHERE p.id = u.id
  AND p.avatar_url IS NULL
  AND u.raw_user_meta_data->>'avatar_url' IS NOT NULL
  AND u.raw_user_meta_data->>'avatar_url' != '';

-- 3. Update the trigger function to copy avatar_url for new OAuth signups
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
BEGIN
  INSERT INTO public.profiles (
    id, email, name, avatar_url,
    trial_words_quota, trial_words_used, trial_expires_at,
    created_at, updated_at
  ) VALUES (
    NEW.id,
    NEW.email,
    COALESCE(NEW.raw_user_meta_data->>'full_name', NEW.raw_user_meta_data->>'name', ''),
    NEW.raw_user_meta_data->>'avatar_url',
    15000,
    0,
    NOW() + INTERVAL '15 days',
    NOW(),
    NOW()
  );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
