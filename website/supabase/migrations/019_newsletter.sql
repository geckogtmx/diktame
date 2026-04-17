-- Migration 019: Newsletter subscribers, sends, and events.
-- First-party newsletter on the existing Supabase + Resend stack,
-- eliminating Substack. Bilingual (EN + ES), double opt-in for blog-form
-- signups, single opt-in (post-Supabase-verify) for account signups.
-- GDPR-defensible: consent version, signup context, persistent unsubscribe
-- token, soft-delete audit trail.
--
-- All reads/writes go through edge functions with the service role key.
-- RLS locks the tables to admins only; edge functions bypass RLS.

-- =====================================================================
-- newsletter_subscribers — one row per (email, locale)
-- =====================================================================
CREATE TABLE newsletter_subscribers (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email TEXT NOT NULL,
  locale TEXT NOT NULL CHECK (locale IN ('en', 'es')),
  status TEXT NOT NULL DEFAULT 'pending'
    CHECK (status IN ('pending', 'confirmed', 'unsubscribed', 'bounced', 'complained', 'deleted')),

  user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL,
  source TEXT CHECK (source IN ('account-signup', 'blog-sidebar', 'landing', 'admin')),

  -- Consent audit trail (GDPR)
  signup_ip INET,
  signup_url TEXT,
  signup_user_agent TEXT,
  consent_text_version TEXT,

  -- Tokens
  confirm_token TEXT,        -- set on pending, nulled on confirm
  unsubscribe_token TEXT,    -- persistent

  confirmed_at TIMESTAMPTZ,
  unsubscribed_at TIMESTAMPTZ,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  UNIQUE (email, locale)
);

CREATE INDEX idx_newsletter_subscribers_status ON newsletter_subscribers(status);
CREATE INDEX idx_newsletter_subscribers_locale ON newsletter_subscribers(locale);
CREATE INDEX idx_newsletter_subscribers_user_id ON newsletter_subscribers(user_id);
CREATE INDEX idx_newsletter_subscribers_confirm_token ON newsletter_subscribers(confirm_token)
  WHERE confirm_token IS NOT NULL;
CREATE INDEX idx_newsletter_subscribers_unsubscribe_token ON newsletter_subscribers(unsubscribe_token)
  WHERE unsubscribe_token IS NOT NULL;

ALTER TABLE newsletter_subscribers ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Admin full access subscribers" ON newsletter_subscribers
  FOR ALL USING (
    EXISTS (SELECT 1 FROM profiles WHERE id = auth.uid() AND is_admin = true)
  );

-- =====================================================================
-- newsletter_sends — one row per (post, locale) broadcast
-- =====================================================================
CREATE TABLE newsletter_sends (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  post_id UUID NOT NULL REFERENCES blog_posts(id) ON DELETE CASCADE,
  locale TEXT NOT NULL CHECK (locale IN ('en', 'es')),
  subscriber_count INT NOT NULL DEFAULT 0,
  resend_batch_ids TEXT[] NOT NULL DEFAULT '{}',
  status TEXT NOT NULL DEFAULT 'queued'
    CHECK (status IN ('queued', 'sending', 'done', 'failed', 'partial')),
  started_at TIMESTAMPTZ,
  completed_at TIMESTAMPTZ,
  error TEXT,

  UNIQUE (post_id, locale)
);

CREATE INDEX idx_newsletter_sends_post_id ON newsletter_sends(post_id);
CREATE INDEX idx_newsletter_sends_status ON newsletter_sends(status);

ALTER TABLE newsletter_sends ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Admin full access sends" ON newsletter_sends
  FOR ALL USING (
    EXISTS (SELECT 1 FROM profiles WHERE id = auth.uid() AND is_admin = true)
  );

-- =====================================================================
-- newsletter_events — Resend webhook receipts
-- =====================================================================
CREATE TABLE newsletter_events (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  subscriber_id UUID REFERENCES newsletter_subscribers(id) ON DELETE SET NULL,
  send_id UUID REFERENCES newsletter_sends(id) ON DELETE SET NULL,
  event_type TEXT NOT NULL
    CHECK (event_type IN ('delivered', 'bounced', 'complained', 'opened', 'clicked', 'unsubscribed', 'sent', 'failed')),
  resend_event_id TEXT UNIQUE,
  event_data JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_newsletter_events_subscriber_id ON newsletter_events(subscriber_id);
CREATE INDEX idx_newsletter_events_send_id ON newsletter_events(send_id);
CREATE INDEX idx_newsletter_events_event_type ON newsletter_events(event_type);
CREATE INDEX idx_newsletter_events_created_at ON newsletter_events(created_at DESC);

ALTER TABLE newsletter_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Admin full access events" ON newsletter_events
  FOR ALL USING (
    EXISTS (SELECT 1 FROM profiles WHERE id = auth.uid() AND is_admin = true)
  );
