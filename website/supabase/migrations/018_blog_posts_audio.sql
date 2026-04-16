-- Migration 018: Add audio columns to blog_posts and create blog-audio storage bucket.
-- Enables Gemini 3.1 Flash TTS-generated audio versions of blog posts,
-- triggered manually per-post from the HQ admin panel.

ALTER TABLE blog_posts
  ADD COLUMN audio_url_en TEXT,  -- Public URL to generated EN audio (WAV)
  ADD COLUMN audio_url_es TEXT;  -- Public URL to generated ES audio (WAV)

-- Create the blog-audio storage bucket (public read, admin write via service role)
INSERT INTO storage.buckets (id, name, public)
VALUES ('blog-audio', 'blog-audio', true)
ON CONFLICT (id) DO NOTHING;
