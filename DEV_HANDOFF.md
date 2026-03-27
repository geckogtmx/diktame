# Developer Handoff

## Next Session — Video UX Polish + SPEC_002 Remaining

**Priority 1:** Video UX polish:
- Selection border overlay during recording (dotted rectangle like Windows Snipping Tool — user requested)
- Recording toolbar → CP bar migration (decision made: use existing CP bar auto-collapse + snap instead of standalone window)
- Error feedback: close recording bar if capture fails
- Clean up 0-byte MP4 files on failure

**Priority 2:** V7 — Filler word removal (post-STT cleanup pass on narration audio). MEDIUM priority.

**Also pending:**
- Audit #3: Cloud provider retry (Polly). 2-3 hrs.
- VG-4: Scrolling capture research (effort TBD — complex Win32 scroll-and-stitch)
- Freeform transparency masking (currently crops to bounding box, Windows does shape mask)
- Vision clipboard UX: focus/cursor issues when injecting AI text — user must position cursor before AI finishes. Needs UI polish pass (auto-focus target window, or queue injection).

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
| M1 Annotation editor | ✅ Shipped | Arrow, rect, ellipse, freehand, text, step counter, color picker, flatten export |
| Phase 5: AI-aware annotations | ✅ Shipped | `_annotationContext` injected into Gemini system prompt. Verified runtime. |
| M1 flatten quality | ✅ Fixed | Bilinear interpolation via BitmapTransform (was nearest-neighbor → squished circles) |
| Annotated image saving | ✅ Shipped | `*_annotated.png` saved alongside originals in `%APPDATA%\DiktaMe\vision\` |
| Vision clipboard UX | ✅ Shipped | AI text injected into active window → image placed on clipboard for Ctrl+V |
| V7 Filler word removal | Pending | Post-STT cleanup. MEDIUM priority. |

### Session Log (2026-03-26, evening session)

**What shipped:**

#### Phase 5: AI-Aware Annotations — VERIFIED + FIXED
- **Bug found**: `HandleVisionClipboardAsync` was passed `null` UserQuery → hit early return (no-AI path), silently copied image only
- **Fix** (`a0526d6`): Default annotation prompt + user query from re-shown modal. Annotation context injected into Gemini system prompt.
- **Runtime verified**: Gemini correctly interprets annotations (arrows, steps, text labels) — 7-10s latency
- Annotated images now saved as `*_annotated.png` alongside originals

#### Flatten Quality Fix
- **Bug**: `RenderTargetBitmap` captures at layout-pixel size (DPI-dependent), old nearest-neighbor pixel loop caused squished circles
- **Fix** (`536fc44`): `BitmapTransform` with `BitmapInterpolationMode.Linear` for proper bilinear resampling

#### Vision Clipboard UX Improvement
- **Before**: Both text + image on clipboard → double-paste confusion
- **After** (`b2b4387`): AI text injected via `TextInjector` into active window → annotated image placed on clipboard
- User sees text appear, then can Ctrl+V to paste the screenshot
- **Known issue**: Focus/cursor positioning friction — user must position cursor before AI finishes. Deferred to UI polish pass.

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

### WinUI Gotchas Discovered
- **Unused C# labels crash XAML compiler (exit code 1, silent):** An unused `processAction:` label
  (CS0164 warning) caused the WinUI 3 XAML compiler to fail during XBF generation. No error in
  output.json. Fix: remove the unused label. C# warnings from source generators can trigger XAML compiler crashes.

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
| Post-annotation AI (Gemini) | 7-10s |

### Key Architecture Decisions

- **ScreenRecorderLib** over raw D3D11/Media Foundation — D3D11 P/Invoke crashed with COM interop issues (`D3D11_CREATE_DEVICE_VIDEO_SUPPORT` flag not universally supported). ScreenRecorderLib wraps all this cleanly.
- **Recording toolbar → CP bar** — Instead of a separate floating window, future work will use the existing Control Panel bar (auto-collapse + snap-to-position) as recording controls. See SPEC_002 §15.8.
- **Multi-pick palette** — Click accumulates, no close-on-pick. Enter=done, Backspace=undo last, Esc=finish if picks exist/cancel if empty. Palette strip shows swatches at bottom of overlay.

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
| **SPEC_002** | Vision — 5 capture modes, color picker (C1+C3+C4), video (V1-V6), M1 annotations + Phase 5 AI-aware, telemetry, audits |
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
