# Vision Pipeline

The Vision system lets dIKta.me capture screen content (screenshots, regions, video clips) and route it through a multimodal LLM pipeline. This document covers the internal architecture, the key extension points, and how to add multimodal support to a new LLM provider.

---

## Architecture Overview

```
Hotkey (Ctrl+Alt+S)
      │
      ▼
SnippingOverlayWindow          ← fullscreen WinUI 3 overlay, returns SnippingResult
      │
      ▼
VisionActionWindow             ← modal: query input, action selection, Local/Cloud toggle
      │
      ▼
LoadingViewModel               ← dispatches to the right handler based on VisionAction
      │
      ├── VisionPipeline       ← screenshot + optional voice query → LLM → output
      ├── ColorPickerOverlay   ← pixel sampling, palette accumulation
      └── VideoCapture         ← ScreenRecorderLib, floating stop bar, post-capture modal
```

---

## Core Types

### `ScreenCapture` (static, `DiktaMe.Core.Vision`)

Pure Win32 GDI screen capture — no WinRT, no async. All methods return `byte[]` PNG data.

```csharp
// Capture the foreground application window (skips dIKta.me's own windows)
byte[] png = ScreenCapture.CaptureActiveWindow();

// Capture a specific rectangle on the virtual screen
byte[] png = ScreenCapture.CaptureRegion(x, y, width, height);

// Capture the entire virtual screen (all monitors)
byte[] png = ScreenCapture.CaptureFullScreen();

// Helper: bounding rect of the monitor that owns the foreground window
RECT bounds = ScreenCapture.GetActiveMonitorBounds();
```

Capture uses `PrintWindow(PW_RENDERFULLCONTENT)` first (captures DWM-composited content including hardware-accelerated surfaces). If that fails, it falls back to `BitBlt(SRCCOPY)`. The PNG encoder is a custom synchronous implementation — using WinRT `BitmapEncoder` would require `await` which can deadlock when called from the UI thread before the window closes.

---

### `ImageProcessor` (static, `DiktaMe.Core.Vision`)

Pre-processes PNG data before sending to a cloud or local API.

```csharp
// Resize longest side to maxDimension, preserving aspect ratio
byte[] resized = await ImageProcessor.ResizeIfNeeded(png, maxDimension: 2048);

// Re-encode as JPEG at 85% quality if the PNG exceeds maxBytes (default 1 MB)
(byte[] data, string mimeType) = await ImageProcessor.CompressToJpegIfNeeded(png, maxBytes: 1_048_576);

// Convenience: resize then compress in one call
(byte[] data, string mimeType) = await ImageProcessor.PrepareForApi(png, maxDimension: 2048);

// Extract a sub-region from an existing PNG
byte[] cropped = await ImageProcessor.CropRegion(png, x, y, width, height);
```

`PrepareForApi` returns `"image/png"` if compression was not needed, or `"image/jpeg"` after JPEG conversion — pass the returned MIME type directly to the LLM provider.

---

### `VisionOptions` record (`DiktaMe.Core.Vision`)

Configuration bag passed to `VisionPipeline.RunAsync`.

```csharp
public sealed record VisionOptions
{
    public string SystemPrompt { get; init; }
    public string DefaultQuery { get; init; } = "Describe what you see and extract any visible text.";
    public int MaxImageDimensionPx { get; init; } = 2048;
    public int MaxResponseTokens { get; init; } = 4096;
    public double Temperature { get; init; } = 0.3;
    public string? ModelName { get; init; }           // null = use provider's global default
    public VisionOutputMode OutputMode { get; init; } = VisionOutputMode.Inject;
}

public enum VisionOutputMode { Inject, Clipboard, ToastOnly, ToastInject, ToastClipboard }
```

---

### `VisionPipeline` (`DiktaMe.Core.Pipeline`)

Three-stage orchestrator. Raises `StateChanged` as it moves through stages.

```csharp
var result = await _visionPipeline.RunAsync(
    screenshotData: png,            // byte[] — output of ImageProcessor.PrepareForApi
    mimeType: "image/png",          // or "image/jpeg"
    audioFilePath: tempWavPath,     // null to skip STT and use options.DefaultQuery
    options: new VisionOptions
    {
        SystemPrompt  = "You are a helpful vision assistant.",
        OutputMode    = VisionOutputMode.Clipboard,
        MaxResponseTokens = 2048,
    },
    cancellationToken: ct);

if (result.IsSuccess)
    Console.WriteLine(result.Text);
```

**Stage 1 — Optional STT**: If `audioFilePath` is provided, the audio is sent to the configured `ISTTProvider`. On failure the pipeline falls back to `options.DefaultQuery` rather than aborting.

**Stage 2 — Multimodal LLM**: Calls `ILLMProvider.ProcessWithImageAsync(imageData, mimeType, query, systemPrompt, mode: "vision", ct)`. If the provider throws `NotSupportedException`, the pipeline surfaces a `PipelineResult.Failure` with a user-readable message.

**Stage 3 — Output**: Depending on `VisionOutputMode`, the text is injected via `TextInjector`, copied to the clipboard, or shown as a toast — or a combination of both (e.g. `ToastInject`).

**Events:**
```csharp
_visionPipeline.StateChanged += (s, state) => { /* PipelineState enum */ };
_visionPipeline.Completed    += (s, result) => { /* PipelineResult */ };
```

---

## Multimodal LLM Interface

`ILLMProvider` exposes a default-throw multimodal method. Providers opt in by overriding it.

```csharp
// Default implementation (throws — provider does not support images)
public virtual Task<LlmResult> ProcessWithImageAsync(
    byte[] imageData,
    string mimeType,
    string text,
    string systemPrompt,
    string mode = "vision",
    CancellationToken cancellationToken = default)
    => throw new NotSupportedException($"{Name} does not support image input.");
```

### Implemented providers

| Provider class | Cloud/Local | Notes |
|---|---|---|
| `GeminiProvider` | Cloud | Inline base64 in `inlineData` part |
| `AnthropicProvider` | Cloud | `image` content block with base64 source |
| `OpenAICompatibleProvider` | Cloud + Local | `image_url` with `data:` URI; covers GPT-4o and Ollama vision models |
| `OllamaProvider` | Local | Delegates to `OpenAICompatibleProvider` with Ollama endpoint |

### Adding multimodal support to a new provider

Override `ProcessWithImageAsync` in your provider class:

```csharp
public override async Task<LlmResult> ProcessWithImageAsync(
    byte[] imageData, string mimeType,
    string text, string systemPrompt,
    string mode = "vision",
    CancellationToken cancellationToken = default)
{
    var base64 = Convert.ToBase64String(imageData);

    // Build your provider's multimodal request body here
    var requestBody = new { /* ... */ };

    var json = await _httpClient.PostJsonAsync("/your/endpoint", requestBody, cancellationToken);
    return new LlmResult(Text: json["text"]!.GetString()!, Provider: Name, LatencyMs: /* */);
}
```

`LlmResult` properties: `Text`, `Provider`, `LatencyMs`, `InputTokens?`, `OutputTokens?`, `TokensPerSec?`. The `IsSuccess` property returns `true` when `Text` is non-null and non-empty.

---

## Video Recording

Video is handled by `VideoCapture` (in `DiktaMe.App`) which wraps the `ScreenRecorderLib` NuGet package.

```csharp
var options = new VideoRecordingOptions
{
    MaxDurationSeconds = 120,
    FrameRateHz        = 30,
    BitrateKbps        = 5000,     // "medium" quality
    EnableMicAudio     = true,
    EnableSystemAudio  = true,     // WASAPI loopback
    EnableWebcam       = true,
    WebcamBubbleSize   = 200,      // px width, 16:9 aspect
    WebcamPosition     = "bottom-right",
};

await _videoCapture.RecordAsync(left, top, width, height, outputPath, options, ct);
```

`VideoCapture` enforces even dimensions on the capture region (H.264 requirement) and clamps the output path to `%APPDATA%\DiktaMe\vision\`. The `VideoRecordingBarWindow` floating bar fires `Stop` and `Pause` commands back to `LoadingViewModel` via `IMessenger`.

Post-capture AI actions (Describe / Document / Bug Report) upload the MP4 to the **Gemini File API** and issue a multimodal prompt against the uploaded file URI. Local Ollama models cannot decode MP4 containers and are not routed for video actions.

---

## Color Picker

`ColorPickerOverlayWindow` takes a frozen PNG screenshot, decodes it to raw BGRA pixel data on construction, and samples the pixel under the cursor on each `PointerMoved` event. No GDI or WinRT calls happen during interaction — all sampling is pure array indexing into the decoded buffer.

The window returns a `ColorPickerResult` containing a `List<Color>` (accumulated picks) and a `bool RequestAiAnalysis` flag. `LoadingViewModel` formats the hex values as a comma-separated string, optionally sending them through the LLM pipeline for palette analysis.

---

## `VisionSettings` reference

All vision preferences live in `AppSettings.Vision` (type `VisionSettings`):

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Master toggle; unregisters hotkey when false |
| `DefaultQuery` | `string` | `"Describe what you see..."` | Fallback query when no voice/text input is given |
| `MaxImageDimensionPx` | `int` | `2048` | Resize threshold before API upload |
| `AutoRecordQuery` | `bool` | `true` | Start mic immediately after capture |
| `QueryTimeoutSeconds` | `int` | `10` | Silence timeout for auto-record |
| `MaxResponseTokens` | `int` | `4096` | Token cap for vision LLM response |
| `Temperature` | `double` | `0.3` | LLM temperature for vision calls |
| `OllamaKeepAliveSeconds` | `int` | `300` | How long to keep local vision model loaded |
| `CloudVisionProvider` | `string` | `"gemini"` | Cloud provider key |
| `CloudVisionModelId` | `string` | `"gemini-2.5-flash"` | Cloud model ID |
| `LocalVisionModelId` | `string` | `"minicpm-v"` | Ollama model tag |
| `ClipInjectAtCursor` | `bool` | `true` | Inject Clipboard action result at cursor |
| `OcrInjectAtCursor` | `bool` | `true` | Inject OCR result at cursor |
| `ColorPickerInjectAtCursor` | `bool` | `true` | Inject color picker output at cursor |
| `VideoAiInjectAtCursor` | `bool` | `true` | Inject video AI result at cursor |
| `VideoQuality` | `string` | `"medium"` | `"low"` / `"medium"` / `"high"` |
| `EnableMicAudio` | `bool` | `true` | Record microphone during video |
| `EnableSystemAudio` | `bool` | `true` | WASAPI loopback system audio |
| `EnableWebcam` | `bool` | `true` | PIP webcam bubble |
| `WebcamSize` | `int` | `200` | Webcam bubble width (px) |
| `WebcamPosition` | `string` | `"bottom-right"` | Bubble anchor position |
