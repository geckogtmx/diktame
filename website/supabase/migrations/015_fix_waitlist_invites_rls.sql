-- Migration 015: Tighten waitlist_invites SELECT policy.
-- Previously: qual = "true" (any authenticated user could read ALL invite
-- recipient emails — PII leak risk). Fix: restrict to sender's own invites.

DROP POLICY IF EXISTS "Allow users to see their own invites" ON public.waitlist_invites;

CREATE POLICY "Users see own sent invites"
  ON public.waitlist_invites
  FOR SELECT
  TO public
  USING (auth.uid() = sender_id);
