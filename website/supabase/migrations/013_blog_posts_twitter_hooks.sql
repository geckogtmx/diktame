-- Migration 013: Add per-language X/Twitter hook text for social media teasers.
ALTER TABLE blog_posts ADD COLUMN twitter_hook_en TEXT;
ALTER TABLE blog_posts ADD COLUMN twitter_hook_es TEXT;
