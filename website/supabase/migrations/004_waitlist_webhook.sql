-- Create a webhook to trigger the welcome email when a new row is added to waiting_list
-- This uses Supabase's built-in net extension for HTTP requests

-- 1. Enable net extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "net";

-- 2. Create the trigger function
CREATE OR REPLACE FUNCTION public.handle_waitlist_signup()
RETURNS TRIGGER AS $$
BEGIN
  -- We call our Edge Function via pg_net
  -- Note: The URL will need to be updated with the actual project ref in production
  -- or we can use the internal function URL if within the same cluster.
  PERFORM net.http_post(
    url := 'https://volwljbiyzvvcqqdojyf.supabase.co/functions/v1/waitlist-welcome',
    headers := jsonb_build_object(
      'Content-Type', 'application/json',
      'Authorization', 'Bearer ' || current_setting('request.headers', true)::json->>'apikey' -- Optional: use anon key
    ),
    body := jsonb_build_object('record', row_to_json(NEW))
  );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- 3. Create the trigger
DROP TRIGGER IF EXISTS on_waitlist_signup ON public.waiting_list;
CREATE TRIGGER on_waitlist_signup
  AFTER INSERT ON public.waiting_list
  FOR EACH ROW
  EXECUTE FUNCTION public.handle_waitlist_signup();
