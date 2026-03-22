# SPEC_002: Vision Module ("See")

> **Status:** DRAFT → **Absorbed into** [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) (Module 3, Phases L–N)
> **Date:** 2026-03-01
> **Supersedes:** V1 `SPEC_004_VISIONARY_MODULE.md` (researched, never implemented)
> **Hotkey:** `Ctrl+Alt+S` ("See")
> **Related Specs:**
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — Meetings module uses shared `ScreenCapture` for session-bound captures (Phase N)
> - [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](SPEC_013_CONNECTORS_IMPLEMENTATION.md) — Vision outputs route through Connectors (cross-module bridge, Phase J)
> - [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md) — Vision results stored as memories for future context recall
> - [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) — **Implementation sprint** (this spec is the design reference; SPEC_015 is the build plan)

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

```
System/Display:     ~1.0 GB
Whisper (if active): ~1.5 GB  (not needed for vision-only)
Ollama LLM (text):  ~1.5-3.5 GB
Ollama Vision:      ~2.0-5.5 GB  (model dependent)
────────────────────────────────
Worst case:         ~8.0-11.5 GB (exceeds 8GB VRAM)
```

**Mitigation:** Vision tasks don't require concurrent STT. Ollama handles model swapping automatically — when a vision model is loaded, the text model may be evicted from VRAM. This is acceptable because vision is a discrete action, not a continuous pipeline.

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

### 4.4 VisionPipeline

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
- Crosshair cursor via `CoreCursor`

### 5.3 Image Handling

- Capture as `byte[]` PNG (via `System.Drawing` or WinRT `SoftwareBitmap`)
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

- **No new NuGet packages required for MVP** — `System.Drawing.Common` is already a transitive dependency for icon handling
- **Optional:** `SixLabors.ImageSharp` if we need cross-platform image processing (not needed for Windows-only)
- **Ollama vision models** require user to `ollama pull llava` or similar — not bundled
- **LFM2.5-VL-1.6B** (Liquid AI) is a recommended local vision model: 1.6B params (~1.2–1.5GB VRAM quantized), strong OCR + real-world QA benchmarks, multi-image support, multilingual. Fits comfortably within the VRAM budget (§3) — potentially allows concurrent STT + Vision on 8GB without model swapping. Same OpenAI-compatible format as LLaVA/Moondream; zero extra integration work once available via `ollama pull lfm2.5-vl`. Tag in `ModelListService` as a known vision-capable model alongside `llava` and `moondream`.

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

## 15. References

- V1 Spec: `E:\git\diktate\docs\internal\specs\deferred\SPEC_004_VISIONARY_MODULE.md`
- Gemini Vision API: `generateContent` with `inlineData` parts
- Claude Vision: Messages API with `image` content blocks
- OpenAI Vision: Chat Completions with `image_url` content
- Ollama Vision: LLaVA / Moondream via `/api/generate` with `images` parameter
- Flameshot UX: https://github.com/flameshot-org/flameshot (snipping overlay reference)
