-- Add Spanish slug column for bilingual SEO
-- Enables /es/blog/{slug_es} alongside /blog/{slug} (EN)
-- Old links using EN slug on /es/ path still work (backward compat via OR query)
ALTER TABLE blog_posts ADD COLUMN slug_es TEXT;

-- Unique index (partial — nulls allowed for old posts without slug_es)
CREATE UNIQUE INDEX idx_blog_posts_slug_es ON blog_posts(slug_es) WHERE slug_es IS NOT NULL;
