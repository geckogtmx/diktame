-- Migration 017: Add Facebook/Meta social media columns to blog_posts.
-- Enables Facebook post copy generation (A/B variants), FB image crops,
-- organic post ID storage, and Meta Ads campaign tracking.
ALTER TABLE blog_posts
  ADD COLUMN facebook_post_en   TEXT,   -- Variant A: FB post copy (EN)
  ADD COLUMN facebook_post_es   TEXT,   -- Variant A: FB post copy (ES)
  ADD COLUMN facebook_post_en_b TEXT,   -- Variant B: FB post copy (EN)
  ADD COLUMN facebook_post_es_b TEXT,   -- Variant B: FB post copy (ES)
  ADD COLUMN facebook_image_url_en TEXT, -- Cropped 1200×630 FB image (EN)
  ADD COLUMN facebook_image_url_es TEXT, -- Cropped 1200×630 FB image (ES)
  ADD COLUMN facebook_post_id   TEXT,   -- Organic FB page post ID (for Page Post Engagement campaigns)
  ADD COLUMN meta_campaign_id   TEXT;   -- Meta Ads campaign ID after boosting
