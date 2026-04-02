-- Migration 011: Add LinkedIn URL to blog posts for cross-platform SEO linking.
ALTER TABLE blog_posts ADD COLUMN linkedin_url TEXT;
