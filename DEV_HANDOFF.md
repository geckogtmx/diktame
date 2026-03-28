# Developer Handoff

## SPEC_002 Vision — COMPLETE ✅

**Completed this session (2026-03-27 evening):**
- ✅ Default-to-Region: Hotkey enables region selection immediately (`be15f43`)
- ✅ B6: CP shows "WORKING" + locks dictation hotkeys during video recording (`be15f43`)
- ✅ B7: Video region recording border overlay with dim + marching ants (`1316a31`) — Pure Win32 layered window, excluded from capture. See [`plans/RECORD_AREA_LAYER.md`](plans/RECORD_AREA_LAYER.md)
- ✅ UI: Removed Table button, reordered PostCapture (Save/OCR/Edit → Clip/Chat/Note), query step redesigned with square LOC/CLD/NON/Go buttons (`44e00cc`)
- ❌ V7 (filler word removal): CANNED — too complex, too low value

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

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 1134+ passing locally (479+ on CI) |
| **Build** | **PASSES** (0 warnings, 0 errors) |
| **CI** | Green (last push) |
| **Branch** | main — needs commit + push |
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
