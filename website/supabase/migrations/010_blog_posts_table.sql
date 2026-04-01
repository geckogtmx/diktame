-- Migration 010: blog_posts table for the literary news blog.
-- Bilingual posts (EN + ES) with voice system, image metadata, and RLS.

CREATE TABLE blog_posts (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  slug TEXT UNIQUE NOT NULL,
  status TEXT NOT NULL DEFAULT 'draft'
    CHECK (status IN ('draft', 'published', 'archived')),

  -- English
  title_en TEXT NOT NULL,
  hook_en TEXT,
  body_en TEXT NOT NULL,
  closing_en TEXT,

  -- Spanish
  title_es TEXT NOT NULL,
  hook_es TEXT,
  body_es TEXT NOT NULL,
  closing_es TEXT,

  -- Voice (internal only)
  voice_id TEXT NOT NULL,
  voice_label_en TEXT,
  voice_label_es TEXT,

  -- Images (one per language)
  image_url_en TEXT,
  image_url_es TEXT,
  image_prompt TEXT,
  image_anchor TEXT,

  -- Metadata
  thematic_arc TEXT,
  headlines_used JSONB,
  newsletter_sources JSONB,
  run_date DATE NOT NULL DEFAULT CURRENT_DATE,

  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  published_at TIMESTAMPTZ,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_blog_posts_status ON blog_posts(status);
CREATE INDEX idx_blog_posts_published_at ON blog_posts(published_at DESC);
CREATE INDEX idx_blog_posts_slug ON blog_posts(slug);

ALTER TABLE blog_posts ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Public read published" ON blog_posts
  FOR SELECT USING (status = 'published');

CREATE POLICY "Admin full access" ON blog_posts
  FOR ALL USING (
    EXISTS (SELECT 1 FROM profiles WHERE id = auth.uid() AND is_admin = true)
  );
