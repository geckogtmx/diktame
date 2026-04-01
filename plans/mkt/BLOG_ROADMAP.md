# BLOG_ROADMAP.md — Literary News Blog for diktame.me

## Vision

A daily bilingual literary news blog at `diktame.me/blog` where AI-curated tech headlines are transformed into long-form literary posts — each written in the voice of a recognized author, researched with real sources, and published in both English and Spanish.

**English voices** use English-language literary authors. **Spanish voices** use Spanish-language literary authors. Each language gets its own familiar tonal register, not a translation of the other.

No author names are displayed publicly. The voice is the product, not the attribution. If this trends, the workflow and voice system may be disclosed later. For now: the writing speaks for itself.

---

## Architecture

### Content Pipeline

```
/news-run (skill)
  → Gmail fetch + dedup + present
  → User picks headlines + assigns voice

/news-writer (skill)
  → Research + write (Spanish original, English adaptation — or vice versa for EN voices)
  → Image prompt generated
  → Post written to plans/mkt/content/NewsRun_MM-DD-YY.md (local working copy)

/news-publish (skill, NEW)
  → Parse the local file
  → Insert into Supabase `blog_posts` table as DRAFT
  → Skill completes — no manual publish step needed

User (manual, from any device)
  → Open /hqbackstage/blog
  → Review draft post
  → Toggle status: draft → published
  → Post goes live on diktame.me/blog
```

### Why DB, Not Files

- No git commit or Vercel redeploy needed per post
- Content and code stay separate
- Can publish/review from phone via Dispatch or admin panel
- ISR (Incremental Static Regeneration) or on-demand revalidation serves posts
- Supabase RLS protects drafts (only admin sees them)
- Easy to add metadata, tags, analytics later

---

## Database Schema

### Table: `blog_posts`

```sql
CREATE TABLE blog_posts (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  slug TEXT UNIQUE NOT NULL,                    -- URL-friendly slug
  status TEXT NOT NULL DEFAULT 'draft',         -- draft | published | archived

  -- English content
  title_en TEXT NOT NULL,
  hook_en TEXT,                                 -- italic opening paragraph
  body_en TEXT NOT NULL,                        -- full markdown body
  closing_en TEXT,                              -- closing meditation

  -- Spanish content
  title_es TEXT NOT NULL,
  hook_es TEXT,
  body_es TEXT NOT NULL,
  closing_es TEXT,

  -- Voice (no public author names)
  voice_id TEXT NOT NULL,                       -- internal voice key, e.g. "fuentes", "poniatowska", "vonnegut"
  voice_label_en TEXT,                          -- public-facing label if ever needed, e.g. "Voice I"
  voice_label_es TEXT,

  -- Image
  image_id TEXT,                                -- matches uploaded image filename (without extension)
  image_url TEXT,                               -- Supabase Storage public URL, set after upload
  image_prompt TEXT,                            -- the Nanobanana prompt (always stored)
  image_anchor TEXT,                            -- conceptual anchor explanation

  -- Metadata
  thematic_arc TEXT,                            -- one-sentence arc
  headlines_used JSONB,                         -- array of headline strings
  newsletter_sources JSONB,                     -- array of source newsletter names
  run_date DATE NOT NULL DEFAULT CURRENT_DATE,

  -- Timestamps
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  published_at TIMESTAMPTZ,                     -- set when status → published
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX idx_blog_posts_status ON blog_posts(status);
CREATE INDEX idx_blog_posts_published_at ON blog_posts(published_at DESC);
CREATE INDEX idx_blog_posts_slug ON blog_posts(slug);

-- RLS
ALTER TABLE blog_posts ENABLE ROW LEVEL SECURITY;

-- Public can read published posts only
CREATE POLICY "Public read published" ON blog_posts
  FOR SELECT USING (status = 'published');

-- Admin can do everything
CREATE POLICY "Admin full access" ON blog_posts
  FOR ALL USING (
    EXISTS (SELECT 1 FROM profiles WHERE id = auth.uid() AND is_admin = true)
  );
```

---

## Voice System

### English Voices (for EN posts)
Candidates — finalize before launch:

| Voice ID | Register | Tone |
|----------|----------|------|
| `vonnegut` | Dry, darkly comic, deceptively simple | American fatigue with systems |
| `didion` | Precise, cool, observational | California clarity, emotional restraint |
| `le-guin` | Speculative, anthropological, humane | Science fiction as political philosophy |
| `orwell` | Direct, angry, moral clarity | The essay as weapon |
| `pynchon` | Dense, paranoid, encyclopedic | Conspiracy as comedy |

### Spanish Voices (for ES posts)
Already established:

| Voice ID | Register | Tone |
|----------|----------|------|
| `fuentes` | Baroque, political, historical fatigue | Dispossession, landlord/tenant power |
| `poniatowska` | Testimonial, intimate, furious | The voice of those overlooked |
| `paz` | Essayistic, philosophical | The labyrinth of identity |
| `galeano` | Poetic, political, accumulative | Latin American extraction and memory |
| `bolaño` | Savage, detective-like, sprawling | Literature as crime scene |

### Voice Rules
- English posts use English voices, written in English first
- Spanish posts use Spanish voices, written in Spanish first
- Each post is a complete work in its language — NOT a translation of the other
- The two posts from the same run may cover the same headlines but the treatment is independent
- No author names appear on the blog. Voice is identified only by internal ID (for analytics/filtering)

---

## Image Workflow

### The Problem
Image generation (Nanobanana/Imagen) happens outside the skill pipeline. The user generates images manually from the prompts, possibly from a phone or different device.

### The Solution

1. **Skill always generates and stores the image prompt** in `image_prompt` column
2. **Image naming convention:** `{post_slug}.{ext}` (e.g., `the-week-compression-became-the-only-law.png`)
3. **Upload destination:** Supabase Storage bucket `blog-images/` (public bucket)
4. **Upload method:** User uploads from any device:
   - Admin panel (`/hqbackstage/blog/{id}/edit`) — drag-and-drop upload
   - Direct Supabase Storage upload (from phone/tablet)
   - Future: Supabase CLI or API from a script
5. **Matching logic:** When rendering a blog post:
   - If `image_url` is set → use it
   - If `image_url` is null → show no image (fail silently, no broken img tag)
   - Admin panel shows "Missing image" badge on posts without images
6. **After upload:** Admin panel sets `image_url` on the post record

### No Blocking
The publish flow never blocks on image availability. A post can go live without an image and have the image added later. The prompt is always available for the user to generate the image at any time.

---

## Frontend Routes

### Public
- `/en/blog` — English blog index (published posts, newest first)
- `/es/blog` — Spanish blog index
- `/en/blog/[slug]` — English post page
- `/es/blog/[slug]` — Spanish post page
- No `/blog/author/*` pages (no public attribution)

### Admin (`/hqbackstage`)
- `/hqbackstage/blog` — All posts list (drafts + published + archived)
- `/hqbackstage/blog/[id]` — Post detail: preview both languages, edit metadata, upload image, toggle status

### Rendering
- ISR with on-demand revalidation (revalidate on publish/unpublish)
- Or: server components with `unstable_cache` + manual revalidation
- Blog index: paginated, 10 posts per page
- Each post page: full markdown rendered, bilingual toggle or side-by-side
- SEO: proper `<title>`, `<meta description>` (from hook), `hreflang` tags, OG image from `image_url`

---

## `/news-publish` Skill (NEW)

### Trigger
After `/news-writer` completes, or manually: "publish this post", "push to blog"

### What It Does
1. Read the local `NewsRun_MM-DD-YY.md` file
2. Parse: extract title, hook, body, closing, image prompt, image anchor, thematic arc, headlines used, voice
3. Generate slug from English title (lowercase, hyphenated, max 60 chars)
4. Insert into `blog_posts` table with status = 'draft'
5. Confirm: "Post '{title_en}' saved as draft. Review at /hqbackstage/blog/{id}"

### What It Does NOT Do
- Does not set status to 'published' (that's manual)
- Does not upload images (that's the user's workflow)
- Does not trigger revalidation (that happens on publish)

---

## Admin Approval Flow

### From Desktop (hqbackstage)
1. Navigate to `/hqbackstage/blog`
2. See list of drafts with "Missing image" badges where applicable
3. Click into a draft → preview EN + ES side by side
4. Optionally: edit title, hook, body (light edits only)
5. Optionally: upload image via drag-and-drop
6. Click "Publish" → sets status = 'published', `published_at = NOW()`
7. Triggers Vercel on-demand revalidation for the blog index + post page

### From Phone (hqbackstage mobile)
Same flow, responsive. The admin panel already works on mobile.

### From Dispatch (Claude Code mobile)
"Publish my latest blog draft" → skill reads latest draft → confirms title → sets published.

---

## SEO Considerations

- **Unique content:** Every post is original, researched, fact-checked — not syndicated or scraped
- **Bilingual:** EN + ES with proper hreflang = 2x indexable surface
- **Daily cadence:** Fresh content signal, especially valuable for news-adjacent topics
- **Long-form:** 1500-2500 words per post, well above thin content thresholds
- **Named sources:** Outbound links to Bloomberg, TechCrunch, CNBC, arXiv = authority signals
- **OG images:** Unique, art-directed images per post (when available)
- **Structured data:** Article schema markup with `datePublished`, `dateModified`, `author` (site name, not individual), `image`
- **Internal linking:** Blog index → posts, posts → related posts (by theme/voice), blog → main site

---

## Implementation Phases

### Phase 1: Database + Skill (build first)
- [ ] Create `blog_posts` table + RLS + indexes
- [ ] Create Supabase Storage bucket `blog-images`
- [ ] Build `/news-publish` skill (parse local file → insert draft)
- [ ] Test full pipeline: `/news-run` → pick → `/news-writer` → `/news-publish` → draft in DB

### Phase 2: Admin Panel (review + publish)
- [ ] `/hqbackstage/blog` — list all posts (status badge, date, voice, image status)
- [ ] `/hqbackstage/blog/[id]` — preview EN + ES, edit metadata, upload image
- [ ] Publish/unpublish toggle with confirmation
- [ ] On-demand revalidation trigger on publish

### Phase 3: Public Blog Pages
- [ ] `/en/blog` and `/es/blog` index pages (paginated, ISR)
- [ ] `/en/blog/[slug]` and `/es/blog/[slug]` post pages
- [ ] Bilingual toggle or language selector
- [ ] SEO: hreflang, OG tags, article schema, sitemap entry
- [ ] Responsive design matching diktame.me aesthetic

### Phase 4: Polish
- [ ] "Related posts" section (by thematic arc similarity or shared headlines)
- [ ] RSS feed (EN + ES)
- [ ] Newsletter integration (optional: weekly digest email of published posts)
- [ ] Analytics: track which voices/topics get most reads
- [ ] Voice rotation system (ensure variety across the week)

---

## Getting Started (Next Session)

**Read this first.** This section tells the next session exactly what to do.

### What Already Exists
- **`/news-run` skill** — `.claude/skills/news-run/SKILL.md` — fetches Gmail newsletters, deduplicates, presents numbered pick-list
- **`/news-writer` skill** — `.claude/skills/news-writer/SKILL.md` — researches + writes literary posts (Spanish first for ES voices, English first for EN voices), generates image prompts
- **`processed-runs.json`** — `.claude/skills/news-writer/processed-runs.json` — ledger tracking used email IDs
- **`plans/mkt/content/`** — gitignored folder where local NewsRun files land
- **`plans/mkt/Carlos Fuentes Series.md`** — legacy curated series file (3 posts already written)
- **Website** — Next.js + Vercel + Supabase at `diktame.me`, admin panel at `/hqbackstage`
- **Supabase project:** `volwljbiyzvvcqqdojyf`

### Phase 1 Tasks (do these in order)

1. **Create `blog_posts` table** — Use Supabase MCP `apply_migration`. SQL is in the "Database Schema" section above. Includes RLS policies for public read (published only) + admin full access.

2. **Create `blog-images` storage bucket** — Public bucket in Supabase Storage. No auth needed for reads.

3. **Build `/news-publish` skill** — New skill at `.claude/skills/news-publish/SKILL.md`. It:
   - Reads the local `NewsRun_MM-DD-YY.md` file from `plans/mkt/content/`
   - Parses: title (EN/ES), hook (EN/ES), body (EN/ES), closing (EN/ES), voice_id, image_prompt, image_anchor, thematic_arc, headlines_used, newsletter_sources
   - Generates slug from English title
   - Inserts into `blog_posts` with status='draft'
   - Uses Supabase MCP `execute_sql` to insert
   - Confirms with post title and admin URL

4. **Test the full pipeline** — `/news-run` → pick headlines → `/news-writer` writes post → `/news-publish` pushes to DB → verify row exists in Supabase

### What NOT to Build Yet
- No public blog pages (Phase 3)
- No admin panel blog UI (Phase 2 — but can verify via Supabase dashboard directly)
- No image upload workflow (manual for now, bucket just needs to exist)
- No revalidation hooks (no public pages to revalidate yet)

### Key Files to Read Before Starting
- This file (`plans/mkt/BLOG_ROADMAP.md`) — full architecture
- `.claude/skills/news-run/SKILL.md` — understand the fetch+dedup pipeline
- `.claude/skills/news-writer/SKILL.md` — understand the writing pipeline + output format
- `plans/mkt/content/` — look at any existing NewsRun files for the parse format
- Website source — check existing Supabase schema patterns, admin panel patterns, RLS patterns

---

## Open Questions

1. **Blog URL structure:** `/blog/[slug]` or `/insights/[slug]` or `/dispatch/[slug]`?
2. **Bilingual display:** Toggle (one language at a time) or side-by-side?
3. **Post frequency:** Daily? Or accumulate 2-3 days of newsletters for a richer post?
4. **Voice disclosure:** When/if to explain the voice system publicly?
5. **Comments:** Allow them? Or keep the blog as a pure publishing surface?
6. **Image generation automation:** Worth exploring API-based generation later to close the manual gap?
