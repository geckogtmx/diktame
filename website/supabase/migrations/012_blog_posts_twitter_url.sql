-- Migration 012: Add X/Twitter URL to blog posts for cross-platform linking.
ALTER TABLE blog_posts ADD COLUMN twitter_url TEXT;
