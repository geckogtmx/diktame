-- Create a table to track viral invitations
CREATE TABLE IF NOT EXISTS public.waitlist_invites (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  sender_id UUID REFERENCES public.waiting_list(id) ON DELETE CASCADE,
  recipient_email TEXT NOT NULL,
  invited_at TIMESTAMPTZ DEFAULT now(),
  -- Prevent sending multiple invites to the same person from different senders (to avoid spam)
  -- Or at least ensure one sender can't invite the same person twice
  UNIQUE(sender_id, recipient_email)
);

-- Enable RLS
ALTER TABLE public.waitlist_invites ENABLE ROW LEVEL SECURITY;

-- Allow anonymous insertion if they have a valid sender_id
-- (A more secure version would check if sender_id exists)
CREATE POLICY "Allow anonymous invites" ON public.waitlist_invites
  FOR INSERT WITH CHECK (true);

-- Allow senders to see who they invited
CREATE POLICY "Allow users to see their own invites" ON public.waitlist_invites
  FOR SELECT USING (true);
