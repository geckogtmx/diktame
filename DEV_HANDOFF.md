# Developer Handoff

## SINGLE_PROVIDER_WALLET — IN PROGRESS (2026-04-05)

**Status:** Implementation complete, tested, **uncommitted**. Decision pending on whether to ship.

**What was done:** Replaced Deepgram + Gemini 2.0 Flash (2 vendors, 2 API calls) with a single Gemini 2.5 Flash call for the wallet pipeline. C#: new `WalletPipelineContext`, `WalletSTTProxy` (replaces `WalletDeepgramProxy`), modified `WalletGeminiProxy` (passthrough), `LoadingViewModel`, `App.xaml.cs`. Edge function: `handleGeminiAudio` handler deployed (v16), `handleDeepgram` kept for rollback. Tests: 1139 passed.

**Finding: ~10x cheaper but ~2x slower.** Old pipeline ~1.6s / ~$0.0006/call. New pipeline ~3.5s / ~$0.00006/call. The Gemini `generateContent` audio endpoint is inherently slower than Deepgram's purpose-built STT. Competitors (Wispr, Aqua) achieve sub-500ms via streaming — our wallet uses batch.

**Phase 2 (Session 2, 2026-04-05): Streaming wallet — NOT DONE. 11 edge function deploys, zero successful Gemini Live API connections.** All C# code **uncommitted** — `git checkout .` reverts everything cleanly. Owner may choose to revert and start fresh.

**Two unresolved problems:**

1. **Gemini Live API won't connect.** 11 edge function deploys tested. The root cause of the `code=1011 Internal error` was identified late in the session (via Gemini): `responseModalities: ["TEXT"]` crashes the Live API — it requires `["AUDIO"]`. v11 has this fix deployed but was **never tested with a rebuilt C# app**. `gemini_setup_complete` has never appeared in any log.

2. **Start/stop UX broken (C# problem, independent of model).** The streaming attempt blocks the hotkey for ~1.5-10s. When streaming fails, batch fallback auto-starts a second recording. User experiences: press hotkey → recording starts → stops on its own → starts again on its own. First words lost. Root cause: recording starts before streaming connection is confirmed, and streaming failure triggers a visible second recording start.

**11 edge function deploys:**

| v | Model | responseModalities | Error |
|---|-------|--------------------|-------|
| 1-3 | `gemini-2.5-flash-live-preview` | TEXT | 1008: model not found |
| 5 | `gemini-2.5-flash` | TEXT | 1008: not supported for bidiGenerateContent |
| 6 | `gemini-2.5-flash-native-audio-latest` | TEXT | 1007: Cannot extract voices from non-audio request |
| 7-10 | `gemini-3.1-flash-live-preview` | TEXT | 1011: Internal error (various setup variations) |
| 11 | `gemini-3.1-flash-live-preview` | **AUDIO** | **Not tested — app not rebuilt** |

**Critical finding:** `responseModalities: ["TEXT"]` causes 1011 on ALL Live API models. Must use `["AUDIO"]` and extract text from `serverContent.modelTurn.parts[].text`, ignoring audio bytes.

**What was built (all uncommitted):**
- C#: `WalletStreamingSTTProxy`, `WalletStreamingDictationPipeline`, 10 tests (1149 total), fail-fast fallback, diagnostics logging, `WalletStreamUrl` in AccountSettings
- Edge function: `wallet-stream` v11 with diagnostics, cost killswitch via `wallet_pipeline_mode` config
- Modified: `LoadingViewModel` (streaming routing), `App.xaml.cs` (DI), `wallet-proxy` (killswitch)
- Migration: `015_wallet_streaming_config.sql`

**Next session — two options:**
- **Option A: Revert and start fresh** — `git checkout .` to clean slate. Rebuild streaming with the `["AUDIO"]` knowledge from the start. Fix the start/stop UX architecture before testing.
- **Option B: Continue from current state** — Rebuild app, test v11, fix start/stop UX. Risk: accumulated complexity from 11 iterations.

**Full spec + data:** [`plans/SINGLE_PROVIDER_WALLET.md`](plans/SINGLE_PROVIDER_WALLET.md)

---

## Audio Feeder V2 Port — ABANDONED (2026-04-03)

**Status:** 12 attempts across 2 sessions. Zero successful end-to-end runs. All code reverted. No code committed.

**Problem:** Python cannot do bidirectional Named Pipe IPC on Windows. Every approach failed — pywin32 synchronous (deadlock), pywin32 overlapped (corrupt data), PowerShell subprocess bridge (FlushFileBuffers deadlock). See [`test-helpers/POTATO_COUCH.md`](test-helpers/POTATO_COUCH.md) for full failure log.

**What exists (uncommitted, in working tree):** `audio_feeder.py`, `fetch_test_data.py`, `Invoke-AudioFeeder.ps1`, `pipe_bridge.ps1`, `fixtures/`. All broken. Do not commit as-is.

**C# server (`LocalApiServer`) is unchanged** — `Start()` remains commented out in `App.xaml.cs`. No server-side changes were committed.

---

## SPEC_002 Vision — COMPLETE ✅

**Completed this session (2026-03-27 evening):**
- ✅ Default-to-Region: Hotkey enables region selection immediately (`be15f43`)
- ✅ B6: CP shows "WORKING" + locks dictation hotkeys during video recording (`be15f43`)
- ✅ B7: Video region recording border overlay with dim + marching ants (`1316a31`) — Pure Win32 layered window, excluded from capture. See [`plans/RECORD_AREA_LAYER.md`](plans/RECORD_AREA_LAYER.md)
- ✅ UI: Removed Table button, reordered PostCapture (Save/OCR/Edit → Clip/Chat/Note), query step redesigned with square LOC/CLD/NON/Go buttons (`44e00cc`)
- ❌ V7 (filler word removal): CANNED — too complex, too low value

**Next session priorities:**
- Gemini Live API streaming wallet — see [SINGLE_PROVIDER_WALLET.md](plans/SINGLE_PROVIDER_WALLET.md) Phase 2, section 3.
- `shop.dikta.me` — link store once LemonSqueezy identity verification completes

**Also pending:**
- Audit #3: Cloud provider retry (Polly). 2-3 hrs.
- VG-4: Scrolling capture research (effort TBD — complex Win32 scroll-and-stitch)
- Freeform transparency masking (currently crops to bounding box, Windows does shape mask)
- Vision clipboard UX: focus/cursor issues when injecting AI text — user must position cursor before AI finishes. Needs UI polish pass.

### SPEC_002 Feature Status

| Feature | Status | Notes |
|---------|--------|-------|
| 5 capture modes (rect/window/full/all/freeform) | ✅ Shipped | All working |
| C1 Color picker | ✅ Shipped | Single pick + live hex/rgb preview |
| C3 Multi-pick palette | ✅ Shipped | Click accumulates, Enter=copy, Tab=analyze, Backspace=undo |
| C4 AI palette analysis | ✅ Shipped | Gemini: color names, style ID, WCAG AA, CSS vars, complements |
| ~~C2 Magnifier~~ | ❌ Cut | Not worth the effort |
| V1 Screen recording → MP4 | ✅ Shipped | ScreenRecorderLib, region/fullscreen/window |
| V2 Mic audio mux | ✅ Shipped | Built into V1 via ScreenRecorderLib |
| V3 Gemini video understanding | ✅ Shipped | Describe, Document, Bug Report — cloud-only |
| V4 System audio (WASAPI loopback) | ✅ Shipped | `IsOutputDeviceEnabled` flag |
| V6 Camera bubble (PIP webcam) | ✅ Shipped | 16:9 aspect, USB-preferred, bottom-right overlay in MP4 |
| ~~V5 Share link~~ | ❌ Deferred | → SPEC_013 Connectors |
| M1 Annotation editor | ✅ Shipped | Arrow, rect, ellipse, freehand, text, step counter, color picker, flatten export |
| Phase 5: AI-aware annotations | ✅ Shipped | `_annotationContext` injected into Gemini system prompt. Verified runtime. |
| M1 flatten quality | ✅ Fixed | Bilinear interpolation via BitmapTransform |
| Annotated image saving | ✅ Shipped | `*_annotated.png` saved alongside originals |
| Vision clipboard UX | ✅ Shipped | AI text injected → image on clipboard |
| **Vision Wizard (CP bar)** | ✅ Shipped | Single-row wizard in CP bar — see §21 |
| **Screen freeze/dim** | ✅ Shipped | Active monitor frozen on hotkey, CP on top |
| **Video post-capture in wizard** | ✅ Shipped | Describe/Document/BugReport/Save in CP (retired modal) |
| **FileSavePicker for Save** | ✅ Shipped | Save-as dialog with default location |
| **Thinking state** | ✅ Shipped | ProgressRing + "Thinking..." during AI, buttons disabled |
| **Edit action wired** | ✅ Shipped | Opens annotation editor from wizard, returns to PostCapture |
| **ESC during full-screen recording** | ✅ Shipped | Polls VK_ESCAPE, stops recording, restores CP |
| **Default-to-Region** | ✅ Shipped | Hotkey enables selection immediately, CP buttons override |
| **B6: WORKING status** | ✅ Shipped | StatusText + dictation lock during video recording |
| **B7: Recording border** | ✅ Shipped | Win32 layered window, marching ants, dim, WDA_EXCLUDEFROMCAPTURE |
| **PostCapture button reorder** | ✅ Shipped | Save/OCR/Edit (immediate) → Clip/Chat/Note (AI query). Table removed. |
| **Query step redesign** | ✅ Shipped | Square LOC/CLD/NON/Go buttons, NON default, orange Go icon |
| ~~V7 Filler word removal~~ | ❌ Canned | Too complex, too low value |

### Session Log (2026-03-27, evening session)

**Vision Wizard — Major UI Refactor**

Replaced the old 3-sub-row vision layout + VisionActionWindow modal with a single-row wizard integrated into the CP bar. See SPEC_002 §21 for canonical flow.

#### Wizard Steps Implemented
1. **CaptureType**: Image / Video / Color (3 buttons)
2. **CaptureMode**: Region / Full Screen (adapts for image vs video)
3. **Recording**: REC dot + timer + pause + stop
4. **PostCapture**: Image actions (Save/Clip/Chat/Note/OCR/Table/Edit) or Video actions (Describe/Document/BugReport/Save) — with thumbnail preview
5. **Query**: TextBox + Local/Cloud/None toggle + Go

#### Bug Fixes (FIX-1 through FIX-8)
- FIX-1: Audio ducking no longer triggers for Save action
- FIX-2: Save shows FileSavePicker dialog (not silent save)
- FIX-3: Screen freeze/dim on hotkey press (active monitor)
- FIX-4: Thumbnail preview in PostCapture panel
- FIX-5: CP auto-collapses during wizard (only Header + Vision row visible)
- FIX-6: Video post-capture uses wizard (retired VideoActionWindow modal)
- FIX-7: Full-screen recording keeps CP visible for region, hides for full-screen
- FIX-8: "Thinking..." spinner during AI processing

#### Critical Bug Fixes (CRIT-1 through CRIT-4)
- CRIT-1: Black images regression — `_dimOverlayScreenshot` was nulled before use in crop
- CRIT-2: "None" (no AI) is now default on Query step
- CRIT-3: ESC stops full-screen video recording (via `GetAsyncKeyState` polling)
- CRIT-4: Clipboard/Note with "None" skip AI entirely (just copy image)
- Toast suppressed during full-screen recording

#### New WinUI Gotcha
- `IsHitTestVisible = false` on a canvas disables ALL input including keyboard (ESC). For dim-only mode, guard pointer handlers individually instead.

### Key Architecture Decisions
- **VisionWizardStep enum**: 6 states (None, CaptureType, CaptureMode, Recording, PostCapture, Query) replace old `VisionRowPhase`
- **Dim overlay reuse**: `SnippingOverlayWindow` with `SetDimOnlyMode()` / `EnableSelection()` for lazy transition
- **Monitor bounds snapshot**: `_dimOverlayMonitorBounds` stored at Step 1, used for all capture paths (prevents wrong-monitor bug)
- **ESC via polling**: `GetAsyncKeyState(VK_ESCAPE)` on background thread during full-screen recording (no WndProc/RegisterHotKey needed)
- **Video actions in wizard**: `IsVideoPostCapture` flag switches PostCapture panel between image buttons and video buttons (Describe/Document/BugReport/Save)

---

## Session: 2026-03-30 (evening) — CP Position Memory + No-License UX

### CP Position Memory (`a624af7`)
- ✅ Save/restore CP window position across restarts (freeform drag + snap presets)
- `WindowX`/`WindowY` added to `ControlPanelSettings` (`int.MinValue` = use snap)
- **WinUI 3 bug**: `AppWindow.Changed` doesn't fire when WndProc is subclassed (our double-click hook). Fix: intercept `WM_WINDOWPOSCHANGED` in the existing WndProc instead.
- **Timing bug**: `App.Current.MainWindow` is null during `Page.Loaded` (constructor not finished). Fix: defer restore via `DispatcherQueue.TryEnqueue`.
- **Override bug**: `LoadFromSettings` re-triggers `BarPosition` change on every settings save, overriding restored position. Fix: `SnapToPosition` checks for saved coords first.
- Debounced 500ms save prevents thrashing during drag
- Snap buttons + double-click clear saved coords and snap to preset
- `GeneralSettingsViewModel.Save()` preserves `WindowX`/`WindowY`

### No-License UX Toast (`a624af7`)
- ✅ All 9 pipeline catch blocks show "License Required" toast instead of raw exception
- `HandleLicenseError()` helper in LoadingViewModel

### Bug Fix: CP snap on every toggle click (`a624af7`)
- ✅ Removed unconditional `OnPropertyChanged(nameof(BarPosition))` from `LoadFromSettings`
- Was causing CP to snap back to preset on any settings change (STT toggle, LLM toggle, etc.)
- Double-click snap restored via explicit `ForceResnap()` call in WndProc hook

### WinUI 3 Gotcha: AppWindow.Changed + WndProc Subclassing
- `AppWindow.Changed` (including `DidPositionChange`, `DidSizeChange`) stops firing entirely when the window is subclassed via `SetWindowLongPtr(GWLP_WNDPROC, ...)`. Known issue: github.com/microsoft/microsoft-ui-xaml/issues/6466
- Workaround: intercept Win32 messages (`WM_WINDOWPOSCHANGED`, `WM_SIZE`, etc.) directly in the WndProc

---

## Session: 2026-03-30 — Vision Settings Polish + LemonSqueezy License System

### Vision Settings Polish (`c99e3b0`)
- ✅ AI system prompts exposed in Workflows > Vision settings (Cloud/Local system prompts in tabs + "Action Prompts" Expander with OCR, Table, Video Describe/Document/BugReport, Video System Prompt)
- ✅ Audio device pickers — mic and output device ComboBoxes appear when corresponding toggle is on. NAudio `MMDeviceEnumerator` for output devices. Device names passed to ScreenRecorderLib `AudioOptions`.
- ✅ Configurable save folder with Browse button (FolderPicker). Default `%APPDATA%\DiktaMe\vision\` when empty.
- All 8 hardcoded prompt strings in LoadingViewModel replaced with `_settings.Current.Vision.*` reads.

### LemonSqueezy License System (`fe1646e`)
- ✅ **REPLACED** RSA offline license validation with LemonSqueezy License API
- Flow: buy on LemonSqueezy → receive GUID key → paste in app → app calls `POST /v1/licenses/activate` → done
- No sign-in required. Machine-bound via `instance_name` (3 activations per key).
- Anti-piracy: hard-coded `store_id=277708` + `product_id=910127` verified in API response `meta`
- 30-day offline grace: cached license expires if not re-validated online
- Startup: `ValidateAsync()` re-checks with LemonSqueezy (offline grace on network failure)
- `DeactivateAsync()` releases instance on LemonSqueezy (frees activation slot)
- Webhook simplified: `provisionLicense()` just updates `profiles.license_tier` for dashboard display
- **LemonSqueezy product ID**: `910127` ("dIKtame App Full")
- **LemonSqueezy store ID**: `277708`
- **Test mode**: active. Webhook URL: `https://volwljbiyzvvcqqdojyf.supabase.co/functions/v1/wallet-webhook`

### Security Hardening (`39d32ea`)
- ✅ Profiles RLS: `WITH CHECK` prevents users from self-updating `license_tier`, `license_status`, `is_admin`
- ✅ HttpClient 10-second timeout
- ✅ 30-day offline grace period on cached licenses
- ✅ Fixed double-prefix `ls_ls_` in licenses table key

### Bug Found: `Environment.MachineName` < 3 chars
- LemonSqueezy requires `instance_name` ≥ 3 characters
- Fix: pad short machine names with `-PC` suffix

### Gotcha: SecureStorage ValidProviders Whitelist
- Adding new DPAPI storage keys requires adding them to the `ValidProviders` HashSet in `SecureStorage.cs` (line 43)
- Missed `license_instance_id` and `license_last_validated` initially — caused activation to fail silently

### Gemini 3.1 Flash Live Research (for future wallet optimization)
- Model: `gemini-3.1-flash-live-preview` — collapses STT+LLM into single native audio model
- Audio in → text out in one hop (replaces Deepgram + Gemini separately)
- **WebSocket streaming ONLY** (no REST/batch) — requires architectural change
- Pricing: $0.005/min audio input, $0.018/min audio output
- Not a drop-in replacement — would need streaming wallet proxy rewrite
- Current wallet (Deepgram + Gemini Flash) works fine — this is a future optimization

---

---

## Session: 2026-04-01 (evening) — Blog System Phase 1

### Blog Database + Storage (`f899f16`)
- ✅ `blog_posts` table created via Supabase MCP `apply_migration` — 25 columns, bilingual content (EN/ES), voice system, dual image URLs (`image_url_en`/`image_url_es`), JSONB metadata
- ✅ `CHECK` constraint on `status` field (`draft`/`published`/`archived`)
- ✅ RLS: public reads published only, admin full access via `is_admin` check
- ✅ Indexes on `status`, `published_at DESC`, `slug`
- ✅ Migration saved locally: `website/supabase/migrations/010_blog_posts_table.sql`
- ✅ `blog-images` Supabase Storage bucket (public read, admin write)

### Admin Panel — `/hqbackstage/blog`
- ✅ Blog list page: table with title, voice, status badge, run date, image status (EN/ES checkmarks or "Missing"), created date
- ✅ Post edit page: side-by-side EN/ES markdown preview (via `react-markdown`), collapsible metadata section
- ✅ Drag-and-drop image upload zones (EN + ES separately), calls `/api/hqbackstage/blog/[id]/image`
- ✅ Publish/Unpublish toggle with confirmation dialog
- ✅ Blog nav item added to AdminSidebar (pencil icon, after Overview)
- ✅ Verified live on dikta.me — admin panel renders correctly, empty state shown

### API Routes
- ✅ `GET /api/hqbackstage/blog/[id]` — fetch full post data
- ✅ `PATCH /api/hqbackstage/blog/[id]` — partial update with field whitelist + status validation
- ✅ `POST /api/hqbackstage/blog/[id]/image` — upload to `blog-images/{slug}-{lang}.webp`, updates `image_url_en`/`image_url_es`

### `/news-publish` Skill
- ✅ New skill at `.claude/skills/news-publish/SKILL.md` (gitignored, local only)
- Parses structured `NewsRun_MM-DD-YY.md` files from `/news-writer` output
- Extracts: EN/ES title/hook/body/closing, voice_id, image_prompt, image_anchor, thematic_arc, headlines, sources
- Generates URL-safe slug from EN title (max 60 chars, uniqueness check)
- Dollar-quoting per field for SQL safety
- Inserts as `status='draft'` via Supabase MCP `execute_sql`

### Security Audit Fixes
- ✅ **Mass assignment fix**: PATCH endpoint now uses explicit field whitelist (was spreading `...body` directly)
- ✅ **Status validation**: rejects values outside `draft`/`published`/`archived`
- ✅ **Slug validation in image upload**: regex check prevents path traversal in storage filenames
- ℹ️ Service role overprivilege in `requireAdmin()` — existing pattern across all admin routes, not blog-specific
- ℹ️ MIME-only file validation — acceptable for admin-only endpoint (no public upload)

### Schema Change vs BLOG_ROADMAP.md
- `image_url` split into `image_url_en` + `image_url_es` (two images per post, one per language)
- `image_id` dropped (redundant with URL-based storage flow)
- Added `CHECK` constraint on `status` (not in original spec)

### Architecture Notes
- **Auth flow**: Supabase auth callback targets production URL → cannot test admin pages on localhost without reconfiguring
- **Image naming**: `{slug}-{en|es}.webp` with `upsert: true` — re-uploading replaces the file
- **NewsRun multi-run**: Same-day runs append letter suffix (`NewsRun_04-01-26b.md`)
- **Publish flow**: `/news-publish` → draft in DB → admin panel → upload images → click Publish

### Full Pipeline Run (E2E verified)
- ✅ `/news-run` → fetched 6 newsletters, deduped to 10 headlines, filtered 5 already-used emails
- ✅ `/news-writer` → wrote 2 posts: Aldous Huxley (EN voice) + José Emilio Pacheco (ES voice, new voice file created)
- ✅ Created `VOICE_JOSE_EMILIO_PACHECO.md` — elegiac, precise, understated, Mexico City as palimpsest
- ✅ Both posts translated to the other language (Huxley→ES adaptation, Pacheco→EN adaptation) with "Originally written in" note
- ✅ `/news-publish` → parsed `NewsRun_04-01-26b.md`, inserted 2 drafts into Supabase
- ✅ Images generated on Nanobanana (2 per post = 4 total), uploaded via admin panel
- ✅ Both posts published via admin panel — live on `dikta.me/blog`
- ✅ Same-day headline recycle: `allHeadlines` field added to ledger, Phase 0 checks before Gmail fetch

### Client-Side Image Compression (`648f110`)
- ✅ Images resized to max 1920px width, compressed to WebP (0.82 quality) in browser before upload
- Fixes Vercel `FUNCTION_PAYLOAD_TOO_LARGE` error on 8MB+ images from Nanobanana

### Public Blog Pages (`770c80f`)
- ✅ `/[locale]/blog` — index page with published posts as full-width cards, locale-aware images/content
- ✅ `/[locale]/blog/[slug]` — post detail with hero image, markdown body, closing pull-quote, BlogPosting JSON-LD
- ✅ "Originally written in [English/Spanish]" note based on voice_id language mapping
- ✅ Language toggle + social links at bottom
- ✅ Navbar: added "Blog" link (desktop + mobile)
- ✅ Sitemap: `/blog` static route + dynamic published post URLs with hreflang
- ✅ `BlogPage` translation namespace in `messages/{en,es}.json`
- ✅ Supabase Storage domain added to `next.config.ts` `remotePatterns`
- ✅ Verified live — both posts rendering with images on `dikta.me/blog`

### Admin Panel Enhancements
- ✅ **View button** (`28b4551`) — opens public post in new tab, only shows when published
- ✅ **Inline editing** (`33543ef`) — pencil icon on Title/Hook/Body/Closing, per-section save via PATCH API, monospace textarea for markdown body
- ✅ **LinkedIn URL field** (`6629e59`) — per-post field in metadata section, "Also on LinkedIn" link on public blog (migration 011)
- ✅ **X/Twitter URL field** (`2753a6f`) — same pattern, refactored `LinkedInField` → generic `SocialUrlField` (migration 012)
- ✅ **Hook for X card** (`87bfa3a` → `5089ae3`) — smart auto-generation from first sentence (not dumb truncate), editable `twitter_hook_en`/`twitter_hook_es` columns (migration 013), EN/ES toggle, copy button, char counter, save/reset
- ✅ **Social links row** (`275585a`) — public blog post footer shows language toggle + LinkedIn + X as horizontal pill buttons with icons

### Migrations This Session
| # | Name | Purpose |
|---|------|---------|
| 010 | `blog_posts_table` | Main table + RLS + indexes |
| 011 | `blog_posts_linkedin_url` | LinkedIn URL column |
| 012 | `blog_posts_twitter_url` | X/Twitter URL column |
| 013 | `blog_posts_twitter_hooks` | `twitter_hook_en` + `twitter_hook_es` columns |

### Skills Updated
- `/news-run` — Phase 0 same-day headline recycle, `allHeadlines` in ledger
- `/news-writer` — Phase 6b X hooks (purpose-built social teasers, not truncated blog hooks)
- `/news-publish` — parses X hooks from NewsRun files, inserts `twitter_hook_en`/`twitter_hook_es`

### Voice Files
- `VOICE_ALDOUS_HUXLEY.md` — already existed, used for Post 1
- `VOICE_JOSE_EMILIO_PACHECO.md` — **created this session**, elegiac/precise/understated, Mexico City flaneur

### Next Session Priorities
- Run the pipeline again with new newsletters to test the full updated flow (X hooks in output)
- Phase 4 candidates: RSS feed, related posts, voice rotation, analytics
- Consider: Supabase image hostname in `next.config.ts` may need updating if bucket URL changes

---

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 1134 passing locally |
| **Build** | **PASSES** (0 warnings, 0 errors) |
| **CI** | Deployed on Vercel (`275585a`) |
| **Branch** | main — pushed |
| **Website** | Deployed on Vercel (dikta.me), Root Directory = `website` |
| **License** | LemonSqueezy License API (test mode active, E2E verified) |

## Completed Streams

| Stream | Summary |
|--------|---------|
| **A-E** | Git repo, solution scaffold, publish config, CancellationToken, Config, Data, Security |
| **F** | WinUI 3 UI Layer — all 12 tasks |
| **G** | 689 unit tests + CI/CD pipeline |
| **I** | SnippetManager, AudioDucker, ChatPipeline, OllamaManager |
| **J** | CRUD Dictation Modes — all 7 tasks |
| **K** | OAuth & Trial Credits — K.1-K.7 |
| **L** | Deepgram Streaming — L.1-L.5 committed |
| **SPEC_002** | Vision — 5 capture modes, color picker (C1+C3+C4), video (V1-V6), M1 annotations + Phase 5, wizard UI, telemetry, audits |
| **SPEC_007** | Chat Feature Upgrade — 14/14 tasks |
| **SPEC_009** | Local Mode E2E + Wizard Fixes — Phases A-G, FIX-1 through FIX-16 |
| **SPEC_011** | Ollama Management Hub |
| **DOCS_V2** | Exhaustive user documentation |
| **SPEC_003 A–G** | TTS: All 40 tasks. E2E verified. |
| **SPEC_KOKORO_GPU** | **BLOCKED** — DirectML ConvTranspose incompatibility |
| **Settings Rework** | 8 features in one session |
| **UI Revamp** | Glassmorphic theme, CP auto-collapse/waveform/snap, nav polish |
| **ACCOUNTS_SIGNIN** | Website auth, dashboard, admin, JWT refresh, Ko-fi webhooks |
| **AVATAR** | Profile pic upload, Supabase Storage, branded deeplinks |
| **UI_REVAMP_SCROLL_CP** | 3-layer cylinder roll idle animation, WeatherService |
| **Chat Theming** | QuickChatWindow themed, MarkdownTextBlock themed |
| **Vision Settings Polish** | AI prompts, audio device pickers, save folder config |
| **LemonSqueezy License** | RSA replaced with LemonSqueezy License API, E2E verified, security hardened |
| **Blog System** | Full pipeline: DB + admin panel + skills + public pages + SEO + social cross-posting. 13 commits, 4 migrations, 3 skills updated, 2 posts live |
