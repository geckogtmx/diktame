# SPEC_002: Vision Module ("See")

> **Status:** READY FOR IMPLEMENTATION
> **Date:** 2026-03-01 (revised 2026-03-24)
> **Supersedes:** V1 `SPEC_004_VISIONARY_MODULE.md` (researched, never implemented)
> **Hotkey:** `Ctrl+Alt+S` ("See")
> **Role:** Design reference for [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) Phase 0C. This spec defines *what* and *why*; SPEC_015 defines *when* and *how* within the sprint.
> **Phase 0B dependency:** Vision lives in Core (not a plugin) but should be built after Phase 0B so it can publish to `PipelineEventBus` from day one. This avoids retrofitting event wiring later.
> **Related Specs:**
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — Meetings module uses shared `ScreenCapture` for session-bound captures (Phase N)
> - [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](SPEC_013_CONNECTORS_IMPLEMENTATION.md) — Vision outputs route through Connectors (cross-module bridge, Phase J)
> - [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md) — Vision results stored as memories for future context recall
> - [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) — **Implementation sprint** (build plan; this spec is the design reference)

---

## 1. Executive Summary

**"You talk, dIKta.me looks."**

dIKta.me is currently blind — it hears you but can't see what you're looking at. The Vision module adds multimodal capability: capture a screenshot, optionally speak a query, and get an AI-analyzed response injected into your active window.

Both cloud (Gemini, Claude, OpenAI) and local (Ollama vision models) are supported from day one, following the existing Cloud/Local profile pattern already used across all other modes.

---

## 2. Core Workflow

```
[1] User presses Ctrl+Alt+S
    → Screen capture overlay appears (dim + crosshair)

[2] User selects target:
    → Click: capture active window
    → Drag: capture selected region
    → Esc: cancel

[3] Optional voice query (auto-records after selection):
    → "What does this error mean?"
    → "Extract the table data"
    → "Turn this mockup into HTML"
    → Silence/skip: defaults to "Describe what you see and extract any visible text."

[4] Processing:
    → Screenshot (PNG) + voice query → STT → LLM (multimodal)
    → Cloud profile: Gemini/Claude/OpenAI vision API
    → Local profile: Ollama vision model (LLaVA, Moondream, etc.)

[5] Output:
    → Response injected into active window via TextInjector
    → Also shown as toast notification (truncated preview)
    → Full result stored in history (if privacy level allows)
```

---

## 3. V1 Research Carried Forward

V1 SPEC_004 went through extensive research. Key decisions preserved:

| V1 Decision | Rationale | V2 Adaptation |
|-------------|-----------|---------------|
| Cloud-first (Gemini) for MVP | Zero VRAM impact, natively multimodal, already integrated | Both cloud + local from day one — V2 has the Cloud/Local profile toggle pattern |
| GLM-OCR as future local option | Smallest footprint (1.6GB), SOTA OCR | Replaced by Ollama ecosystem — LLaVA, Moondream, and newer models available via `ollama pull` |
| Defer MiniCPM-V (5.5GB) | Too large for 8GB GPU | Still valid — leave to user choice via Ollama model selection |
| `mss`/`pyautogui` for capture | Python utilities | Replaced by Win32 GDI / `Windows.Graphics.Capture` (native C#) |
| Flameshot-style snipping overlay | Best UX reference | Adopted — dim + crosshair + region selection |

### VRAM Budget (Local Mode)

**Target: coexistence on 8GB GPUs.** By scoping to small vision models (~1-2GB), both the text LLM and vision model can remain loaded simultaneously — no model swapping needed.

```
Recommended (coexistence):
System/Display:     ~1.0 GB
Ollama text LLM:    ~1.5-3.5 GB  (e.g., Phi-3 3.8B Q4 ≈ 2.5GB)
Ollama Vision:      ~1.2-1.5 GB  (Moondream 2 or LFM2.5-VL)
────────────────────────────────
Total:              ~3.7-6.0 GB  ✓ fits on 8GB GPU

Advanced (requires swapping):
Ollama Vision:      ~2.5-5.5 GB  (LLaVA-Phi3 or LLaVA 7B)
────────────────────────────────
Total:              ~5.0-10.0 GB (may exceed 8GB → Ollama auto-evicts)
```

**Recommended local vision models (small footprint):**

| Model | Params | VRAM (Q4) | Strengths | Ollama |
|-------|--------|-----------|-----------|--------|
| **Moondream 2** | 1.8B | ~1.2GB | Edge-optimized, fast, good OCR | `ollama pull moondream` |
| **LFM2.5-VL** | 1.6B | ~1.2-1.5GB | Best-in-class for size, multilingual, multi-image, document understanding | GGUF on HuggingFace; Ollama community model pending official support |
| **LLaVA-Phi3** | 3.8B | ~2.5GB | Phi-3 backbone, stronger reasoning | `ollama pull llava-phi3` (stretch — may not coexist with large text LLM) |

**Mitigation for larger models:** Ollama auto-evicts the least-recently-used model when VRAM is exhausted. VisionPipeline should cancel/await any in-flight LLM request before calling a vision model to avoid concurrent VRAM pressure. See §4.6 for details.

---

## 4. Architecture

### 4.1 New Components

```
DiktaMe.Core/
├── Vision/
│   ├── ScreenCapture.cs          // Win32 screenshot capture (full screen, window, region)
│   ├── VisionOptions.cs          // Options record (capture mode, system prompt, etc.)
│   └── SnippingOverlay.cs        // Logic for region selection coordinates
├── LLM/
│   ├── ILLMProvider.cs           // Extended with ProcessWithImageAsync()
│   └── (existing providers)      // GeminiProvider, AnthropicProvider, OpenAICompatible — add image support
├── Pipeline/
│   └── VisionPipeline.cs         // Screenshot → optional STT → multimodal LLM → inject result

DiktaMe.App/
├── Views/
│   └── SnippingOverlayWindow.xaml // Transparent fullscreen overlay for region selection
├── ViewModels/
│   └── LoadingViewModel.cs       // Add Vision hotkey dispatch + RunVisionPipelineAsync()
```

### 4.2 ILLMProvider Extension

Current interface (text-only):
```csharp
Task<LlmResult> ProcessAsync(string text, string systemPrompt,
    string mode = "dictate", CancellationToken cancellationToken = default);
```

New overload for multimodal:
```csharp
Task<LlmResult> ProcessWithImageAsync(byte[] imageData, string mimeType,
    string text, string systemPrompt, string mode = "vision",
    CancellationToken cancellationToken = default);
```

Default implementation throws `NotSupportedException` — providers opt in by overriding.

### 4.3 Provider Multimodal API Formats

**GeminiProvider** — native multimodal:
```json
{
  "contents": [{
    "parts": [
      { "inlineData": { "mimeType": "image/png", "data": "{{base64}}" } },
      { "text": "{{userQuery}}" }
    ]
  }],
  "systemInstruction": { "parts": [{ "text": "{{systemPrompt}}" }] }
}
```

**AnthropicProvider** — Claude vision:
```json
{
  "messages": [{
    "role": "user",
    "content": [
      { "type": "image", "source": { "type": "base64", "media_type": "image/png", "data": "{{base64}}" } },
      { "type": "text", "text": "{{userQuery}}" }
    ]
  }],
  "system": "{{systemPrompt}}"
}
```

**OpenAICompatibleProvider** — GPT-4o vision:
```json
{
  "messages": [{
    "role": "user",
    "content": [
      { "type": "image_url", "image_url": { "url": "data:image/png;base64,{{base64}}" } },
      { "type": "text", "text": "{{userQuery}}" }
    ]
  }]
}
```

**Ollama** (via OpenAICompatibleProvider or native API) — same OpenAI format for LLaVA/Moondream.

### 4.5 LLMRouter Integration

`LLMRouter` wraps `ILLMProvider` with primary/fallback routing. It needs a passthrough for the multimodal overload:

```csharp
// LLMRouter — add multimodal passthrough (same pattern as ProcessAsync)
public async Task<LlmResult> ProcessWithImageAsync(byte[] imageData, string mimeType,
    string text, string systemPrompt, string mode = "vision",
    CancellationToken cancellationToken = default)
{
    try { return await _primary.ProcessWithImageAsync(imageData, mimeType, text, systemPrompt, mode, cancellationToken); }
    catch when (_fallback != null) { return await _fallback.ProcessWithImageAsync(imageData, mimeType, text, systemPrompt, mode, cancellationToken); }
}
```

### 4.6 Model Switching (Local Mode)

**Happy path (recommended models):** Moondream/LFM2.5-VL (~1.2GB) + text LLM (~2-3.5GB) coexist on 8GB GPU. No swapping needed. Both models stay loaded.

**Edge case (larger vision models):**

1. **Cancel in-flight:** When Vision hotkey fires, cancel any active text LLM pipeline via `CancellationToken` before calling the vision model. Prevents concurrent VRAM pressure.
2. **Ollama auto-eviction:** Ollama evicts LRU model when VRAM is full. No app-side coordination needed.
3. **`keep_alive` tuning:** Vision model uses Ollama's `keep_alive` parameter to control VRAM residency. Default `5m` is reasonable. Expose `OllamaKeepAliveSeconds` in VisionSettings for users who want immediate unload (`keep_alive: 0`).

**Loading UX:** Show toast "Loading vision model..." during first vision call (cold load = 3-10s). Subsequent calls with warm model are fast (~1s).

**Cloud mode:** No model switching concerns. Skip all VRAM management.

### 4.7 VisionPipeline

```
VisionPipeline.RunAsync(screenshotData, audioFilePath?, options, cancellationToken)
│
├── [1] If audioFilePath provided: STT transcription → user query text
│       Else: use default query ("Describe what you see...")
│
├── [2] LLM.ProcessWithImageAsync(screenshotData, "image/png", queryText, systemPrompt)
│       Cloud profile → Gemini/Claude/OpenAI
│       Local profile → Ollama vision model
│
├── [3] TextInjector.InjectText(response)
│
└── [4] Return PipelineResult (text, timing, provider info)
```

---

## 5. Screen Capture

### 5.1 Capture Modes

| Mode | Trigger | Implementation |
|------|---------|----------------|
| **Active Window** | Single click after overlay appears | `GetForegroundWindow()` + `PrintWindow()` or BitBlt |
| **Region Select** | Click + drag rectangle | Overlay tracks mouse, captures region from virtual screen |
| **Full Screen** | Future option (settings) | `BitBlt` on entire virtual display |

### 5.2 Snipping Overlay Window

A transparent, fullscreen, always-on-top WinUI 3 window:

```
┌─────────────────────────────────────────┐
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│  ← Semi-transparent dark overlay
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│░░░░░░░┌─────────────────┐░░░░░░░░░░░░░│
│░░░░░░░│                 │░░░░░░░░░░░░░│  ← Selected region (clear)
│░░░░░░░│   (user drags)  │░░░░░░░░░░░░░│
│░░░░░░░│                 │░░░░░░░░░░░░░│
│░░░░░░░└─────────────────┘░░░░░░░░░░░░░│
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│  Press Esc to cancel  │  Click = Window │  ← Bottom hint text
└─────────────────────────────────────────┘
```

**Implementation:**
- Transparent `Window` covering all monitors
- Canvas with semi-transparent black fill
- Mouse down → start rect; mouse move → update rect (clear cutout); mouse up → capture region
- Single click (no drag) → capture active window bounds
- Esc → cancel and close overlay
- Crosshair cursor via `InputSystemCursor` (WinUI 3) or `SetCursor` (Win32 fallback)

> **WinUI 3 Note:** Transparent fullscreen overlays have known limitations in WinUI 3 (DPI scaling, multi-monitor edge cases). Primary approach: WinUI 3 `Window` with `SystemBackdrop = null` and transparent composition. Fallback: raw Win32 layered window via P/Invoke if WinUI 3 approach has issues. Evaluate during implementation.

### 5.3 Image Handling

- Capture via `Windows.Graphics.Capture` API (preferred, available on Windows 10 2004+ which matches our TFM `net8.0-windows10.0.19041.0`). Fallback: GDI `BitBlt` via P/Invoke.
- Output as `byte[]` PNG (via WinRT `SoftwareBitmap` → `BitmapEncoder`)
- Resize if larger than 2048px on longest side (reduce API payload / token cost)
- Compress to JPEG (quality 85) if PNG > 1MB
- Base64 encode for API transmission
- Temporary file for local Ollama models that require file path

---

## 6. Hotkey & Mode Integration

### 6.1 Hotkey Registration

```csharp
// HotkeyId enum — add new slot:
Vision = 8    // Ctrl+Alt+S ("See")

// HotkeySettings — add default:
public string Vision { get; init; } = "Ctrl+Alt+S";
```

### 6.2 Pipeline Dispatch (LoadingViewModel)

```csharp
case HotkeyId.Vision:
    _ = RunVisionPipelineAsync();
    break;
```

**RunVisionPipelineAsync flow:**
1. Show snipping overlay → await user selection → get `byte[] screenshot`
2. Optionally record short voice query (reuse existing `RecordAudioAsync`)
3. Get active profile: `_pipelines.GetActiveProfile("vision")`
4. Create pipeline: `_pipelineFactory.CreateVisionPipeline()`
5. Run pipeline: `pipeline.RunAsync(screenshot, audioPath, options, ct)`
6. Show toast notification with truncated result preview

### 6.3 Voice Query (Optional)

After the user selects a region, a short recording window opens (same as Note mode):
- User speaks query → STT → text query
- Or user presses hotkey again / Enter to skip → default query used
- Timeout: 5 seconds of silence → auto-submit with default query

---

## 7. Settings

### 7.1 VisionSettings (AppSettings)

```csharp
public sealed record VisionSettings
{
    public bool Enabled { get; init; } = true;
    public string DefaultQuery { get; init; } = "Describe what you see and extract any visible text.";
    public int MaxImageDimensionPx { get; init; } = 2048;
    public bool AutoRecordQuery { get; init; } = true;  // Auto-start voice recording after capture
    public int QueryTimeoutSeconds { get; init; } = 10;
    public int MaxResponseTokens { get; init; } = 4096;  // Vision responses need more tokens than dictation (1024)
    public double Temperature { get; init; } = 0.3;      // Slightly higher than dictation (0.1) for creative vision tasks
    public int OllamaKeepAliveSeconds { get; init; } = 300; // How long vision model stays in VRAM after use (0 = unload immediately)
}
```

### 7.2 Per-Profile Model Selection

Follows the existing CRUD Dictation Modes pattern (Stream J):
- Cloud profile: select vision-capable model (e.g., `gemini-2.0-flash`, `claude-sonnet-4-5-20250929`, `gpt-4o`)
- Local profile: select Ollama vision model (e.g., `llava`, `moondream`, `llava-llama3`)
- `ModelListService` already discovers available models — extend to tag vision-capable models

### 7.3 Settings UI

Add "Vision" section to Settings (new tab or subsection):
- Enable/disable toggle
- Default query text
- Auto-record voice query toggle
- Cloud model selector (filtered to vision-capable)
- Local model selector (filtered to Ollama vision models)

---

## 8. Output Modes

| Mode | Behavior | Use Case |
|------|----------|----------|
| **Inject** (default) | Paste response into active window via `TextInjector` | "What does this error mean?" → answer typed at cursor |
| **Clipboard** | Copy to clipboard, show toast | User wants to review before pasting |
| **Toast only** | Show full response in notification | Quick visual answer, no text output needed |

Configurable in VisionSettings. Default: Inject (matches all other modes).

---

## 9. Error Handling

| Scenario | Response |
|----------|----------|
| No vision-capable model selected | Toast: "Configure a vision model in Settings > Vision" |
| API error (rate limit, auth) | Toast with error message + "Check API key in Settings" |
| Ollama model doesn't support vision | Toast: "Model X doesn't support images. Try llava or moondream." |
| Screenshot capture fails (permissions) | Toast: "Screen capture failed. Check display permissions." |
| Image too large after resize | Compress to JPEG, retry. If still >4MB, crop to center region. |
| Voice query STT fails | Fall back to default query text, proceed with vision |
| User cancels (Esc) | Silent cancel, no action |

---

## 10. Implementation Phases

### Phase 1: Core Vision Pipeline (MVP)
**Effort:** ~3-4 days

1. `ScreenCapture` class — Win32 active window + region capture
2. `SnippingOverlayWindow` — transparent fullscreen overlay with region selection
3. Extend `ILLMProvider` with `ProcessWithImageAsync()` + default `NotSupportedException`
4. Implement multimodal in `GeminiProvider` (inline image data)
5. Implement multimodal in `AnthropicProvider` (Claude vision)
6. Implement multimodal in `OpenAICompatibleProvider` (GPT-4o / Ollama vision)
7. `VisionPipeline` — capture → optional STT → multimodal LLM → inject
8. Hotkey registration (`Vision = 8`, `Ctrl+Alt+S`)
9. `LoadingViewModel` dispatch + `RunVisionPipelineAsync()`
10. Basic `VisionSettings` in AppSettings

### Phase 2: Voice Query + Polish
**Effort:** ~1-2 days

1. Auto-record voice query after screenshot selection
2. Image preprocessing (resize, compress, format detection)
3. Output mode selector (inject / clipboard / toast)
4. Vision settings UI tab
5. Per-profile model selection for vision (cloud + local)
6. History integration (store vision results in SQLite if privacy allows)

### Phase 3: Advanced UX
**Effort:** ~1-2 days

1. Multi-monitor support (overlay spans all displays)
2. Keyboard shortcuts within overlay (Enter = active window, arrows = fine-tune region)
3. Selection dimensions overlay (show WxH while dragging)
4. Recent captures gallery (re-analyze previous screenshots with new queries)
5. "Follow-up" mode — keep screenshot in context, ask multiple questions

### Phase 4: Extended Capabilities (Future)
**Effort:** TBD

1. OCR-only mode (extract text without LLM reasoning — faster, cheaper)
2. Ollama vision model auto-detection in `ModelListService`
3. MCP tool (expose vision to external AI agents)
4. Batch capture (capture multiple regions, analyze together)
5. Integration with Scribe sessions — capture whiteboard during meeting

---

## 11. Existing Code to Reuse

| Component | File | Reuse |
|-----------|------|-------|
| `TextInjector` | `Core/Input/TextInjector.cs` | Inject vision response into active window |
| `ISTTProvider` + providers | `Core/STT/` | Transcribe voice query |
| `LLMRouter` | `Core/LLM/LLMRouter.cs` | Route to correct provider based on profile |
| `PipelineFactory` | `Core/Config/PipelineFactory.cs` | Add `CreateVisionPipeline()` |
| `AudioRecorder` | `Core/Audio/AudioRecorder.cs` | Record voice query |
| `NotificationService` | `App/Services/NotificationService.cs` | Toast result preview |
| `HistoryManager` | `Core/Data/HistoryManager.cs` | Store vision results |
| `ModelListService` | `Core/LLM/ModelListService.cs` | Discover vision-capable models |
| `HotkeyManager` | `Core/Input/HotkeyManager.cs` | Register `Ctrl+Alt+S` |
| `DictationModeManager` | `Core/Config/DictationModeManager.cs` | Per-mode model selection pattern |
| `PipelineResult` | `Core/Pipeline/PipelineResult.cs` | Unified result model |

---

## 12. Dependencies

- **No new NuGet packages required for MVP** — image capture uses `Windows.Graphics.Capture` (WinRT) and GDI P/Invoke (both already available in the target TFM)
- **Optional:** `SixLabors.ImageSharp` if we need advanced image processing (not needed for MVP — resize/compress via `BitmapEncoder` is sufficient)
- **Ollama vision models** — user installs via `ollama pull`. Not bundled with the app.

### Recommended Local Vision Models (Small Footprint)

Scoped to ~1-2GB VRAM so vision + text LLM coexist on 8GB GPUs without model swapping.

| Model | Params | VRAM (Q4) | Strengths | Install |
|-------|--------|-----------|-----------|---------|
| **Moondream 2** | 1.8B | ~1.2GB | Edge-optimized, fast inference, good OCR | `ollama pull moondream` |
| **LFM2.5-VL** | 1.6B | ~1.2-1.5GB | Best-in-class for size: multilingual, multi-image, document understanding, strong OCR + real-world QA | GGUF on HuggingFace (`LiquidAI/LFM2.5-VL-1.6B`); Ollama community model pending official |
| **LLaVA-Phi3** | 3.8B | ~2.5GB | Phi-3 backbone, stronger reasoning for complex analysis | `ollama pull llava-phi3` (may not coexist with large text LLMs on 8GB) |

**Advanced user options (require model swapping on 8GB):** LLaVA 7B (~4.7GB), MiniCPM-V (~5.5GB). These work but Ollama will evict the text LLM from VRAM during vision calls.

**ModelListService integration:** Tag known vision-capable model IDs (`moondream`, `lfm2.5-vl`, `llava-phi3`, `llava`, `minicpm-v`) so the Settings UI can filter to vision-capable models in the model selector.

---

## 13. Competitive Context

No direct competitors combine dictation + vision in a single desktop app:

- **Granola / Fellow** — meeting-only, no vision
- **Windows Snipping Tool** — capture only, no AI analysis
- **ShareX** — capture + OCR, no LLM reasoning
- **GitHub Copilot Vision** — IDE-only, not system-wide
- **Claude Desktop** — can analyze pasted images, but no hotkey capture workflow

dIKta.me's angle: **system-wide hotkey → instant capture → voice query → AI response injected at cursor.** No app switching, no copy-paste, no uploading images to a web chat.

---

## 14. Success Criteria

- [ ] `Ctrl+Alt+S` triggers snipping overlay on all connected monitors
- [ ] User can click (active window) or drag (region) to capture
- [ ] Captured screenshot is sent to vision-capable LLM with user's voice query
- [ ] Response is injected into the active window via `TextInjector`
- [ ] Works with Gemini, Claude, and OpenAI vision APIs (cloud profile)
- [ ] Works with Ollama vision models — LLaVA, Moondream (local profile)
- [ ] Cloud/Local profile toggle works identically to other modes
- [ ] Esc cancels capture cleanly with no side effects
- [ ] Images are resized/compressed to stay under API payload limits
- [ ] Vision results appear in history (if privacy level allows)

---

## 15. Video Capture — "Capture Moments" (Loom-style)

> **Added 2026-03-26** — Extends Vision from screenshots to short video clips.
> **Philosophy:** Not a screen recorder (OBS). Not a streaming tool. **Loom for AI** — short async clips with voice narration, flowing through dIKta.me's pipeline architecture.

### 15.1 Why

Every time a user alt-tabs to Loom, ShareX, or OBS to record their screen, dIKta.me loses top-of-mind. The goal is **one tool always running** where every screen interaction starts and flows into AI pipelines + memory + connectors.

dIKta.me already has the mic, the pipelines, the connectors, and the memory layer. Video is the missing capture surface.

### 15.2 Core Flow

```
[1] User presses hotkey (e.g., Ctrl+Alt+R — "Record")
    → Snipping overlay appears (same as screenshot)

[2] User selects region, window, or fullscreen
    → Overlay closes, recording starts
    → Floating mini-bar: ● REC  00:12  [⏸ Pause] [⏹ Stop]
    → Mic captures voice narration (already have AudioRecorder)

[3] User clicks Stop (or max duration hit, default 120s)
    → MP4 saved to %APPDATA%\DiktaMe\vision\

[4] Post-capture modal (extended VisionActionWindow):
    → 💾 Save (MP4, no AI)
    → 📋 Describe — Gemini video understanding → clipboard
    → 📝 Document — "Write step-by-step instructions for what I just did"
    → 🐛 Bug Report — structured report with visual evidence + AI description
    → 💬 Chat — attach clip to QuickChat for follow-up questions
    → 🔗 Share — upload to Supabase Storage → short link (VG-3 infra)
```

### 15.3 Technical Requirements

| Component | API / Library | Notes |
|-----------|---------------|-------|
| Frame capture | `Windows.Graphics.Capture` (`GraphicsCaptureItem`, `Direct3D11CaptureFramePool`) | Win10 1903+ for display capture. Per-window capture = Win11 only (`TryCreateFromWindowId`). |
| Video encoding | `MediaStreamSource` + `MediaTranscoder` or `Windows.Media.MediaProperties` | Frames → H.264 MP4. Reference: [SimpleRecorder](https://github.com/MicrosoftDocs/SimpleRecorder) |
| Mic audio | NAudio `WaveInEvent` (already used by `AudioRecorder`) | Mux mic audio into MP4 |
| System audio | NAudio `WasapiLoopbackCapture` (already used by `AudioDucker` for session enumeration) | Optional Phase 2 — capture what the user hears |
| Cloud inference | [Gemini File API](https://ai.google.dev/gemini-api/docs/video-understanding) | Upload MP4 → video understanding. 1 FPS + 1Kbps audio. Up to 1hr at 1M context. Formats: mp4, mpeg, mov, avi, webm, wmv. |
| Floating bar | Small `Window` (always-on-top, compact) | Timer + pause/stop buttons. Similar to CP snap pattern. |

### 15.4 Gemini Video Understanding — Pipeline Integration

```csharp
// New overload on ILLMProvider (or Gemini-specific)
Task<LlmResult> ProcessWithVideoAsync(
    byte[] videoData, string mimeType,
    string text, string systemPrompt,
    CancellationToken cancellationToken = default);
```

**Upload flow:** Gemini File API → upload MP4 → poll until `state: ACTIVE` → reference in `generateContent` request.

**Prompt templates:**
| Action | System Prompt |
|--------|---------------|
| Describe | "Describe what happens in this screen recording. Be concise." |
| Document | "Write step-by-step instructions for the workflow shown. Use numbered steps. Reference UI elements by name." |
| Bug Report | "This is a bug recording. Describe: (1) Expected behavior, (2) Actual behavior, (3) Steps to reproduce, (4) Environment details visible on screen." |

### 15.5 Use Cases

| Persona | Scenario | Pipeline |
|---------|----------|----------|
| Developer | Record a bug → "Write a bug report" → AI structures it → paste into Jira (connector) | Video → Gemini → Bug template → Connector |
| Designer | Record a prototype walkthrough → voice narrate → share link to Slack | Video + mic → MP4 → Supabase → short URL → Connector |
| Manager | Record async standup → "Summarize what I showed" → paste into Notion | Video → Gemini → Summary → Connector |
| Support | Record repro steps for customer → AI generates KB article | Video → Gemini → Documentation → Connector |
| Teacher | Record a how-to → "Turn this into lesson notes" → save to Notes | Video → Gemini → Notes plugin |
| Anyone | "What did I just do wrong?" → AI reviews the clip | Video → Gemini → Clipboard/Chat |

### 15.6 Memory Integration

- Video clips auto-indexed in Visual Memory (SPEC_014 §6.6) — AI-generated description, timestamps, keywords
- Memory knows context: *"Last Tuesday you recorded a bug in the checkout flow"*
- Chaviz can reference: *"Show me the recording where I demoed the new feature"*

### 15.7 Implementation Phases

| Phase | Scope | Effort | Dependency |
|-------|-------|--------|------------|
| **V1** | Record region/window → MP4 (no audio). Floating stop bar. Save action only. | 2-3 sessions | `Windows.Graphics.Capture` |
| **V2** | Add mic audio mux (NAudio). Pause/resume. | 1 session | V1 |
| **V3** | Gemini video upload + understanding prompts. Post-capture AI actions. | 1-2 sessions | V2 + Gemini File API |
| **V4** | System audio capture (WASAPI loopback). Meeting clip use case. | 1 session | V2 |
| **V5** | Share link (Supabase upload), connector routing, memory indexing. | 1-2 sessions | V3 + VG-3 + SPEC_014 |

### 15.8 Constraints

- **Max clip duration:** 120s default (configurable). Not a meeting recorder — that's SPEC_001.
- **File size:** 30s 1080p H.264 ≈ 15-30MB. Gemini accepts up to 2GB.
- **Win10 compatibility:** `GraphicsCaptureItem.CreateFromDisplayId` works on Win10 1903+. Per-window `TryCreateFromWindowId` is Win11 only — use display capture + crop as fallback.
- **Local inference:** No local video understanding models exist at usable quality. Video actions are **cloud-only** (Gemini/OpenAI). Save/Share work offline.
- **VRAM:** Zero GPU impact — video encoding uses CPU/Media Foundation, not CUDA.

---

## 16. Color Picker — AI-Powered Design Capture

> **Added 2026-03-26** — Elevates VN-3 from "developer niche" to pipeline-integrated design tool.
> **Philosophy:** Not a standalone color picker (PowerToys). A **design capture tool** that flows into memory, connectors, and AI — the same way screenshots flow into Chat/Notes/OCR.

### 16.1 Why

Top-of-mind. Every time the user switches to PowerToys Color Picker or a browser extension to grab a color, dIKta.me wasn't there. For a tool that owns the screen (screenshots, snipping, OCR), missing color picking is a gap that breaks the "one tool for everything on screen" promise.

### 16.2 Core Flow

```
[1] User presses hotkey (e.g., Ctrl+Alt+C — "Color")
    → Screen freezes (same CaptureFullScreen bitmap as snipping overlay)
    → Cursor changes to crosshair / eyedropper

[2] Live preview follows cursor:
    ┌──────────────────┐
    │ ┌──────────┐     │
    │ │ ████5x██ │     │  ← Magnified pixel grid (e.g., 11x11 zoomed to ~120px)
    │ │ ████X███ │     │     Center pixel highlighted
    │ │ █████████ │     │
    │ └──────────┘     │
    │ #2A4365          │  ← Hex value (live)
    │ rgb(42, 67, 101) │  ← RGB value
    └──────────────────┘

[3] User clicks → color captured
    → Hex copied to clipboard immediately
    → Toast: "Copied #2A4365"

[4] Optional: hold Shift+Click to multi-pick (collect palette)
    → Each pick adds to a palette strip
    → Enter/Esc to finish → all colors copied

[5] Post-pick actions (optional, via expanded toast or mini-modal):
    → 📋 Copy (hex/rgb/hsl — already done on click)
    → 🎨 Generate Palette — cloud LLM + memory context
    → 📝 Save to Notes — with context ("picked from competitor landing page")
    → 🔗 Send to Figma — connector pushes color/palette
```

### 16.3 Technical Requirements

| Component | Implementation | Effort |
|-----------|----------------|--------|
| Pixel reading | Read from already-captured bitmap (`ScreenCapture.CaptureFullScreen()` PNG bytes). No `GetPixel()` needed — decode the PNG, index into pixel array at cursor coords. | Trivial |
| Magnifier overlay | Small XAML `Canvas` with scaled pixel grid, following cursor position. Render from same bitmap. | Low |
| Color formats | Hex, RGB, HSL, HSV conversions. Pure math, no deps. | Trivial |
| Multi-pick (palette) | `List<Color>` accumulator. Shift+Click adds. Enter finishes. | Low |
| Clipboard | `DataPackage` with text (hex). Already have clipboard infra. | Trivial |
| Eyedropper cursor | Same approach as snipping overlay — `InputSystemCursor` or XAML custom element. | Already needed for crosshair fix |

**Total effort for basic picker:** Half a session. 90% reuses existing `ScreenCapture` + `SnippingOverlayWindow` infrastructure.

### 16.4 AI + Memory + Connector Integration (The dIKta.me Twist)

This is what makes it more than PowerToys:

**Memory-aware palette generation:**
```
User picks #2A4365 from a website
    → Memory knows: "User's brand primary is #1A365D, prefers earthy/navy tones"
    → Cloud LLM generates: 4 complementary colors that match brand + picked color
    → Output: full palette with names + rationale
    → Connector: push to Figma as a color style / Notion as a design note
```

**Voice-enhanced:**
```
User picks a color + says: "I liked this blue from the competitor's landing page,
    give me 4 variants for our dashboard sidebar"
    → STT transcribes intent
    → LLM receives: picked color + voice context + memory (brand colors, preferences)
    → Returns: named palette with usage recommendations
```

**Pipeline integration points:**
| Integration | How |
|-------------|-----|
| **Memory** (SPEC_014) | Store picked colors as observations: `"User picked #2A4365 from competitor site"`. Memory builds preference profile over time. |
| **Connectors** (SPEC_013) | Push to Figma (color styles), Notion (design notes), Slack (share palette). |
| **Notes** | Append color picks to notes.md with context: `"## Colors picked 2026-03-26\n- #2A4365 — competitor header blue\n- #E2E8F0 — their background gray"` |
| **Chat** | Ask QuickChat: `"Based on these colors I've been picking, suggest a dark mode palette"` — Chat has access to memory. |
| **Visual Memory** | Color picks from screenshots are already indexed (dominant_colors in PV schema). Cross-reference: *"Find screenshots with similar blues"* |

### 16.5 Settings

```csharp
public sealed record ColorPickerSettings
{
    public bool Enabled { get; init; } = true;
    public string DefaultFormat { get; init; } = "hex";  // hex, rgb, hsl
    public bool ShowMagnifier { get; init; } = true;
    public int MagnifierZoom { get; init; } = 8;          // pixel zoom factor
    public bool AutoCopyOnPick { get; init; } = true;
    public bool EnableMultiPick { get; init; } = true;     // Shift+Click palette mode
}
```

### 16.6 Implementation Phases

| Phase | Scope | Effort |
|-------|-------|--------|
| **C1** | Basic picker: freeze screen, eyedropper cursor, click → hex to clipboard + toast | 0.5 session |
| **C2** | Magnifier overlay (zoomed pixel grid + live hex/rgb display) | 0.5 session |
| **C3** | Multi-pick palette mode (Shift+Click accumulator) | 0.5 session |
| **C4** | AI palette generation (cloud LLM + memory context) + voice query | 1 session |
| **C5** | Connector routing (Figma, Notion, Notes) | Depends on SPEC_013 |

---

## 17. Markup & Annotation — Post-Capture Editor

> **Added 2026-03-26** — Promotes VG-2 from gap list into full design. Applies to both screenshots and video thumbnails.
> **Philosophy:** Capture → Mark up → AI-enhance → Share. The annotation layer sits between capture and pipeline, adding human intent before AI processing.

### 17.1 Why

Every screenshot tool has markup. It's table stakes. But dIKta.me's markup feeds into AI pipelines — annotations become context. Draw an arrow pointing at a bug → AI knows *where* you're looking. Circle a UI element → AI focuses its description there. Highlight text → AI prioritizes that content.

### 17.2 Core Tools

```
┌─────────────────────────────────────────────────────┐
│ [🔲 Select] [➡️ Arrow] [⬜ Rectangle] [⭕ Ellipse]  │  ← Shape tools
│ [✏️ Freehand] [T Text] [🔢 Step] [💬 Callout]      │  ← Annotation tools
│ [🟡 Highlight] [🔲 Blur] [✂️ Crop]                  │  ← Effect tools
│─────────────────────────────────────────────────────│
│                                                     │
│              [Screenshot Image]                     │
│                                                     │
│              ← Canvas with tool overlays →          │
│                                                     │
│─────────────────────────────────────────────────────│
│ Color: [●●●●●●] Width: [—━] Undo ↩️  Redo ↪️       │  ← Properties bar
│                                                     │
│ [💾 Save] [📋 Copy] [🤖 AI Describe] [🔗 Share]    │  ← Actions
└─────────────────────────────────────────────────────┘
```

### 17.3 Tool Inventory

| Tool | Behavior | AI Context Value |
|------|----------|-----------------|
| **Arrow** | Click-drag draws arrow with head. Color + weight configurable. | AI: "User is pointing at [element near arrow head]" |
| **Rectangle** | Drag to draw outlined or filled rect. | AI: "User highlighted region containing [content]" |
| **Ellipse** | Drag to draw circle/oval. | AI: "User circled [element]" |
| **Freehand** | Free-draw pen/pencil strokes. | General annotation context |
| **Text** | Click to place text label. Font size + color. | Direct text visible to AI |
| **Numbered Steps** | Click to place numbered circles (1, 2, 3...). Auto-increments. | AI: "User marked a sequence of steps: 1→2→3" |
| **Callout** | Text box with pointer/speech bubble shape. | AI: user's comment on a specific region |
| **Highlight** | Semi-transparent overlay (yellow default). | AI: "User highlighted [text/area]" |
| **Blur/Redact** | Pixelate or solid-fill a region. | AI: "User redacted a region (likely PII)" — ties into VD-3 |
| **Crop** | Resize canvas boundaries. | Focuses AI on remaining content |

### 17.4 Technical Approach

**Rendering:** WinUI 3 `Canvas` with shape elements (`Line`, `Rectangle`, `Ellipse`, `Path` for freehand, `TextBlock` for text). Each annotation is a layer object stored in an `ObservableCollection<Annotation>`.

**Architecture:**
```
DiktaMe.App/
├── Views/
│   └── AnnotationWindow.xaml       // Post-capture editor
├── ViewModels/
│   └── AnnotationViewModel.cs      // Tool state, annotation collection, undo/redo
├── Models/
│   └── Annotations/
│       ├── IAnnotation.cs          // Base: bounds, color, weight, type
│       ├── ArrowAnnotation.cs
│       ├── RectAnnotation.cs
│       ├── EllipseAnnotation.cs
│       ├── FreehandAnnotation.cs
│       ├── TextAnnotation.cs
│       ├── StepAnnotation.cs
│       ├── CalloutAnnotation.cs
│       ├── HighlightAnnotation.cs
│       └── BlurAnnotation.cs
```

**Export:** Flatten annotations onto screenshot bitmap using `CanvasRenderTarget` (Win2D) or `RenderTargetBitmap` (WinUI). Output = annotated PNG/JPEG.

**Undo/Redo:** Simple `Stack<IAnnotation>` push/pop. No command pattern overkill for V1.

### 17.5 AI-Aware Annotations

The key differentiator. When the annotated screenshot is sent to an AI pipeline, annotations provide structured context:

```csharp
// Generate annotation context for AI system prompt
string annotationContext = AnnotationSerializer.ToPromptContext(annotations);
// Example output:
// "The user has annotated this screenshot:
//  - Arrow pointing to coordinates (340, 220) — likely indicating an element of interest
//  - Red rectangle around region (100,150)-(400,300) — highlighting an area
//  - Text label at (200, 50): 'This button is broken'
//  - Numbered steps: 1 at (50,100), 2 at (200,200), 3 at (350,300)
//  - Blurred region at (500,400)-(600,450) — likely contains sensitive information"
```

This means: **draw an arrow at a bug → ask "What's wrong here?" → AI focuses on where you pointed.** No other screenshot tool does this.

### 17.6 Integration Points

| From | To | Flow |
|------|-----|------|
| **Screenshot capture** | Annotation editor | Capture → optional markup → then AI/save/share |
| **Video thumbnail** | Annotation editor | Pick a frame from video clip → annotate → share |
| **Color picker palette** | Annotation editor | Overlay color swatches on a screenshot |
| **AI Describe/OCR/Table** | Annotation context | Annotations inform AI focus area |
| **Share link (VG-3)** | Annotated image | Upload flattened image to Supabase |
| **Memory (SPEC_014)** | Annotation metadata | "Screenshot with 3 arrows pointing at nav bugs" |
| **Bug Report (Video §15.4)** | Annotated frame | Annotate a frame → include in AI bug report |

### 17.7 Implementation Phases

| Phase | Scope | Effort |
|-------|-------|--------|
| **M1** | AnnotationWindow shell: canvas + screenshot display + arrow/rect/ellipse tools + color/width picker + undo/redo | 2 sessions |
| **M2** | Freehand, text, numbered steps, callout tools | 1 session |
| **M3** | Highlight + blur/redact tools. Export to flattened PNG. | 1 session |
| **M4** | AI-aware annotations: serialize annotation context → inject into AI system prompt | 1 session |
| **M5** | Integrate into VisionActionWindow flow: Capture → [optional: Edit] → AI/Save/Share | 0.5 session |
| **M6** | Crop tool, keyboard shortcuts (Ctrl+Z undo, number keys for tools) | 0.5 session |

---

## 18. Vision Module Roadmap — Unified Build Order

> **Updated 2026-03-26** — Integrates video capture (§15), color picker (§16), and markup (§17) into the existing gap/differentiator framework.

### Phase 1: Quick Wins (Current Sprint)
| ID | Feature | Effort | Status |
|----|---------|--------|--------|
| VG-1 | Copy screenshot image to clipboard | LOW | Pending |
| Crosshair | Fix snipping overlay cursor | LOW | Pending |
| Note UX | Better state transitions for vision→voice→save | LOW | Pending |
| Warmup | Fix gemma3:1b phantom warmup on settings reload | MEDIUM | Pending |

### Phase 2: Color Picker
| ID | Feature | Effort |
|----|---------|--------|
| C1 | Basic picker: freeze → eyedropper → hex to clipboard | 0.5 session |
| C2 | Magnifier overlay + live color display | 0.5 session |
| C3 | Multi-pick palette mode | 0.5 session |

### Phase 3: Markup & Annotation
| ID | Feature | Effort |
|----|---------|--------|
| M1 | AnnotationWindow: arrow/rect/ellipse + undo/redo | 2 sessions |
| M2 | Freehand, text, steps, callout | 1 session |
| M3 | Highlight, blur, flatten export | 1 session |
| M5 | Wire into VisionActionWindow flow | 0.5 session |

### Phase 4: Video Capture
| ID | Feature | Effort |
|----|---------|--------|
| V1 | Record region/window → MP4 (no audio) | 2-3 sessions |
| V2 | Mic audio mux + pause/resume | 1 session |
| V3 | Gemini video understanding + AI actions | 1-2 sessions |

### Phase 5: AI Integration Across All Surfaces
| ID | Feature | Effort |
|----|---------|--------|
| M4 | AI-aware annotations (context in system prompt) | 1 session |
| C4 | AI palette generation + memory-aware color suggestions | 1 session |
| V4 | System audio capture | 1 session |
| V5 | Share links, connector routing, memory indexing | 1-2 sessions |

### Phase 6: Polish & Advanced
| ID | Feature | Effort |
|----|---------|--------|
| VG-3 | Cloud upload + share link (shared infra for screenshots + video) | 1 session |
| VG-4 | Scrolling/full-page capture | 2 sessions |
| VD-3 | AI auto-redaction (needs blur from M3) | 1 session |
| VD-4 | AI smart crop | 0.5 session |
| M6 | Crop tool, keyboard shortcuts | 0.5 session |
| C5 | Connector routing (Figma, Notion) | Depends on SPEC_013 |

---

## 19. Competitive Context — Updated

| Feature | Snipping Tool | ShareX | CleanShot X | Loom | Greenshot | **dIKta.me** |
|---------|:---:|:---:|:---:|:---:|:---:|:---:|
| Screenshot (region/window) | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ |
| Annotation/Markup | ✅ Basic | ✅ | ✅ Best-in-class | ❌ | ✅ | 🔜 Phase 3 |
| Color Picker | ❌ | ✅ | ✅ | ❌ | ❌ | 🔜 Phase 2 |
| Screen Recording | ✅ Basic | ✅ | ✅ | ✅ Best-in-class | ❌ | 🔜 Phase 4 |
| OCR | ❌ | ✅ | ✅ | ❌ | ❌ | ✅ |
| AI Image Understanding | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ **Unique** |
| AI Video Understanding | ❌ | ❌ | ❌ | ❌ | ❌ | 🔜 **Unique** |
| Voice Query on Capture | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ **Unique** |
| AI-Aware Annotations | ❌ | ❌ | ❌ | ❌ | ❌ | 🔜 **Unique** |
| AI Palette from Pick | ❌ | ❌ | ❌ | ❌ | ❌ | 🔜 **Unique** |
| Memory Integration | ❌ | ❌ | ❌ | ❌ | ❌ | 🔜 **Unique** |
| Connector Routing | ❌ | ❌ | ❌ | ✅ Slack/Email | ❌ | 🔜 **Unique** |
| Local AI (Privacy) | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ **Unique** |
| Voice Narration on Record | ❌ | ❌ | ❌ | ✅ | ❌ | 🔜 (mic already built) |
| Scrolling Capture | ❌ | ✅ | ✅ | ❌ | ❌ | 🔜 Phase 6 |
| Share Link | ❌ | ✅ | ✅ | ✅ | ❌ | 🔜 Phase 6 |

**Moat summary:** No competitor has AI understanding of captures, voice-first interaction, memory context, or connector routing. dIKta.me's capture tools are input surfaces for an AI pipeline — everyone else's are endpoints.

---

## 20. References

- V1 Spec: `E:\git\diktate\docs\internal\specs\deferred\SPEC_004_VISIONARY_MODULE.md`
- Gemini Vision API: `generateContent` with `inlineData` parts
- Gemini Video Understanding: https://ai.google.dev/gemini-api/docs/video-understanding
- Claude Vision: Messages API with `image` content blocks
- OpenAI Vision: Chat Completions with `image_url` content
- Ollama Vision: LLaVA / Moondream via `/api/generate` with `images` parameter
- Flameshot UX: https://github.com/flameshot-org/flameshot (snipping overlay reference)
- **Windows 11 Snipping Tool** (PRIMARY UX reference for annotation toolbar): minimal floating toolbar, self-explanatory icons, mode picker bar for capture type selection. Study this before designing AnnotationWindow. Cleaner than Flameshot.
- Windows.Graphics.Capture: https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture
- SimpleRecorder (MS reference app): https://github.com/MicrosoftDocs/SimpleRecorder
- Win32CaptureSample: https://github.com/robmikh/Win32CaptureSample
- Loom (product reference): https://www.loom.com
