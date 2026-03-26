# Developer Handoff

## Next Session — Video UX Polish + SPEC_002 Remaining

**Priority 1:** Video UX polish:
- Selection border overlay during recording (dotted rectangle like Windows Snipping Tool — user requested)
- Recording toolbar → CP bar migration (decision made: use existing CP bar auto-collapse + snap instead of standalone window)
- Error feedback: close recording bar if capture fails
- Clean up 0-byte MP4 files on failure

**Priority 2:** V7 — Filler word removal (post-STT cleanup pass on narration audio). MEDIUM priority.

**Priority 3:** Phase 3: Markup & Annotation (M1-M3). Arrows, rectangles, freehand pen, blur, flatten export.

**Also pending:**
- Audit #3: Cloud provider retry (Polly). 2-3 hrs.
- VG-4: Scrolling capture research (effort TBD — complex Win32 scroll-and-stitch)
- Freeform transparency masking (currently crops to bounding box, Windows does shape mask)

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
| V3 Gemini video understanding | ✅ Shipped | Describe (7.7s), Document (9.1s), Bug Report (15.8s). Cloud-only. |
| V4 System audio (WASAPI loopback) | ✅ Shipped | `IsOutputDeviceEnabled` flag |
| V6 Camera bubble (PIP webcam) | ✅ Shipped | 16:9 aspect, USB-preferred, bottom-right overlay in MP4 |
| ~~V5 Share link~~ | ❌ Deferred | → SPEC_013 Connectors |
| V7 Filler word removal | Pending | Post-STT cleanup. MEDIUM priority. |

### Video Recording Stats (from testing)

| Recording | Duration | File Size | Bitrate |
|-----------|----------|-----------|---------|
| Fullscreen 1080p | 15s | 3.25 MB | ~1.7 Mbps |
| Fullscreen 1080p | 50s | 6.5 MB | ~1.0 Mbps |
| Region capture | 15s | 702 KB | ~0.4 Mbps |

- H.264 encoding via ScreenRecorderLib (Media Foundation)
- 30 FPS, 5 Mbps target bitrate (actual varies with content complexity)
- Webcam overlay: 200×112px (16:9), bottom-right with 20px offset
- Auto-selects USB cameras over virtual (Snap Camera, OBS Virtual, etc.)
- System audio (WASAPI loopback) enabled by default alongside mic

### Latency Baselines (from 2026-03-26 logs)

| Operation | Latency |
|-----------|---------|
| Monitor capture (1920x1080 GDI) | 60-100ms |
| All monitors pre-capture (3 screens) | 200-350ms |
| PNG decode for color picker | <10ms |
| Image crop (CropRegion) | 5-20ms |
| PrepareForApi (resize+compress) | 3-45ms |
| Whisper STT (16-24s audio) | 470-611ms (GPU, 0.03x ratio) |
| Video recording startup | ~400ms |

### Key Architecture Decisions

- **ScreenRecorderLib** over raw D3D11/Media Foundation — D3D11 P/Invoke crashed with COM interop issues (`D3D11_CREATE_DEVICE_VIDEO_SUPPORT` flag not universally supported). ScreenRecorderLib wraps all this cleanly.
- **Recording toolbar → CP bar** — Instead of a separate floating window, future work will use the existing Control Panel bar (auto-collapse + snap-to-position) as recording controls. See SPEC_002 §15.8.
- **Multi-pick palette** — Click accumulates, no close-on-pick. Enter=done, Backspace=undo last, Esc=finish if picks exist/cancel if empty. Palette strip shows swatches at bottom of overlay.

---

## Session Log (2026-03-26, afternoon session)

**What shipped:**

### V6 Camera Bubble (HIGH — verified)
- `VideoCaptureOverlay` with 16:9 aspect ratio (200×112px)
- Auto-prefers USB cameras over virtual cameras (checks device path for `usb` prefix)
- Webcam feed composited directly into MP4 stream (no floating window — Loom-style)
- Tested with Elgato Facecam — visible in recorded video

### V4 System Audio (MEDIUM — built, needs runtime test)
- Single flag: `IsOutputDeviceEnabled = options.EnableSystemAudio`
- `EnableSystemAudio` defaults to `true` on `VideoRecordingOptions`
- WASAPI loopback captures desktop audio (YouTube, meetings, music)
- Mic + system audio mix into MP4 automatically

### C3 Multi-Pick Palette (LOW — verified)
- `ColorPickerOverlayWindow` now accumulates picks in `List<ColorPickResult>`
- Palette strip UI at bottom: colored swatches + count + keyboard hints
- Enter = copy, Tab = analyze with AI, Backspace = undo last pick, Esc = finish/cancel
- Single pick = same UX as before (one hex copied)
- Multi-pick = formatted clipboard output (one `#HEX  rgb(R, G, B)` per line)
- Tested: 7-color palette extracted from color grid screenshot

### V3 Gemini Video Understanding (HIGH — verified)
- `VideoActionWindow` modal: Describe / Document / Bug Report / Save Only
- Uses existing `VisionPipeline` + `GeminiProvider` with video/mp4 inline base64
- Tested all 3 AI actions with production-quality output:
  - **Describe**: YouTube video + stop button interaction (7.7s, 214 chars)
  - **Document**: CP bar interactions → numbered steps (9.1s, 377 chars)
  - **Bug Report**: Chrome DevTools bug → structured report with repro steps (15.8s, 1,642 chars)
- Local video AI (Ollama MiniCPM-V) crashes — model runner can't decode MP4 containers. Cloud-only.

### C4 AI Palette Analysis (LOW — verified)
- Tab key during multi-pick → sends hex list to Gemini for AI analysis
- Uses vision API path (4096 `maxOutputTokens`) — text API (1024) was too short
- Output: color names, style identification, WCAG AA contrast pairs, CSS custom properties, complementary colors
- Tested: 5-color palette → 2,241 chars, 28.7s; 3-color palette → 877 chars, 25.5s
- Uses `gemini-3.1-pro-preview` via cloud vision provider

---

## Previous Sessions (2026-03-26, morning + early afternoon)

### Vision Telemetry
- 5 new columns in history.db: `capture_mode`, `action_type`, `image_width`, `image_height`, `capture_ms`
- Color picks logged as `mode = "color_pick"`
- 2 new tests (1134 total)

### Video Capture V1+V2
- `VideoCapture.cs` — ScreenRecorderLib (Media Foundation + Windows.Graphics.Capture)
- Region, fullscreen, and window capture → H.264 MP4 with mic audio
- `VideoRecordingBarWindow.xaml/.cs` — Floating always-on-top bar: blinking red REC dot, mm:ss timer, pause/stop buttons
- `HandleVideoRecordAsync()` in LoadingViewModel — full pipeline wiring

### Vision Quick Wins (9 fixes)
- Crosshair cursor via WinUI ProtectedCursor
- Phantom warmup fix (uses configured model, not hardcoded gemma3:1b)
- VG-1: clipboard copies both AI text AND screenshot image
- App quit stalling fix, Note UX, ESC responsiveness, toast icon

### Audit Critical Fixes
- HistoryManager: SemaphoreSlim(1,1) on all 4 DB methods
- Gemini API key: migrated from ?key= URL param to x-goog-api-key header

---

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 1134+ passing locally (479+ on CI) |
| **Build** | **PASSES** (0 warnings, 0 errors) |
| **CI** | **PASSING** — lint, build, tests, gitleaks, vulnerability audit, publish all green |
| **Branch** | main — all pushed to origin |
| **Website** | Deployed on Vercel (dikta.me), Root Directory = `website` |

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
| **SPEC_002** | Vision — 5 capture modes, color picker (C1+C3+C4), video capture (V1+V2+V3+V4+V6), 9 quick wins, telemetry, audits |
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
