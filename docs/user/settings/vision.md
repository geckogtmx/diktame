# Vision Settings

The **Vision** settings page controls how dIKta.me captures your screen, which AI models process images and video, and what happens to the results.

---

## AI Models

### Cloud Vision Provider / Model

The cloud provider and model used when the **Cloud** toggle is selected in the Vision Action panel.

- **Provider**: `gemini`, `anthropic`, or `openai`
- **Default model**: `gemini-2.5-flash`

Any vision-capable model available on your configured cloud provider can be entered here. Common choices:

| Provider | Recommended model |
|---|---|
| Gemini | `gemini-2.5-flash` (default), `gemini-2.0-flash` |
| Anthropic | `claude-opus-4-5`, `claude-sonnet-4-5` |
| OpenAI | `gpt-4o`, `gpt-4o-mini` |

### Local Vision Model

The Ollama model used when the **Local** toggle is selected in the Vision Action panel.

- **Default**: `minicpm-v`
- Must be a vision-capable model pulled into Ollama before use.

Recommended local models and their approximate VRAM requirements:

| Model | Pull command | VRAM | Best for |
|---|---|---|---|
| `minicpm-v` (default) | `ollama pull minicpm-v` | ~2 GB | General use, OCR, description |
| `moondream` | `ollama pull moondream` | ~1.2 GB | Fast descriptions, low-VRAM hardware |
| `llava-phi3` | `ollama pull llava-phi3` | ~2.5 GB | Stronger reasoning |

> [!NOTE]
> If the selected local model is not installed, dIKta.me shows a toast and routes to cloud automatically.

---

## Capture Behaviour

### Default Query

The text sent to the AI when you submit a capture without typing or recording a question.

**Default:** `Describe what you see and extract any visible text.`

You can change this to suit your most common use case — for example, `"Summarise the key points"` or `"What is the error and how do I fix it?"`.

### Auto-Record Voice Query

When enabled, the microphone starts recording automatically after a screenshot is taken, so you can speak your question immediately.

- **Default**: On
- **Timeout**: 10 seconds of silence before the recording stops and the Action panel opens.

Disable this if you prefer to always type your query manually.

### Query Timeout (seconds)

How long dIKta.me waits for voice input (silence) before proceeding with the default query.

- **Default**: 10 seconds
- Only relevant when **Auto-Record Voice Query** is enabled.

### Max Image Dimension (px)

The longest side (width or height) an image is allowed before dIKta.me resizes it.

- **Default**: 2048 px
- Resizing keeps the aspect ratio intact.
- After resizing, if the image still exceeds 1 MB it is re-encoded as JPEG at 85% quality.
- Reducing this value speeds up uploads on slow connections.

---

## Output Behaviour

Each Vision action has its own **Inject at cursor** toggle. When enabled, the AI response is typed into the active window at the cursor position. When disabled, the response goes to the clipboard only.

| Action | Default |
|---|---|
| **Clipboard** action | Inject at cursor: On |
| **OCR** action | Inject at cursor: On |
| **Color Picker** | Inject at cursor: On |
| **Video AI** actions (Describe / Document / Bug Report) | Inject at cursor: On |

> [!TIP]
> If you are using Vision inside a browser or an app where clipboard injection can be unreliable, turn off **Inject at cursor** and paste manually with `Ctrl+V`.

---

## Video Recording

These settings apply to all screen recordings started from the Vision panel.

### Video Quality

Controls the encoding bitrate.

| Setting | Bitrate | Best for |
|---|---|---|
| Low | ~2 500 kbps | Long recordings, limited disk space |
| **Medium** (default) | ~5 000 kbps | General use |
| High | ~10 000 kbps | Detailed screen content, fine text |

Frame rate is fixed at **30 fps**.

### Microphone Audio

Captures your microphone during recording.

- **Default**: On
- Uses the same input device configured in **Settings → Audio**.

### System Audio

Captures audio playing on your computer (applications, browser tabs, etc.) via WASAPI loopback.

- **Default**: On
- Useful for capturing meeting replays, tutorials, or browser video.

### Webcam Bubble

Overlays a picture-in-picture webcam feed in the bottom-right corner of the recording.

- **Default**: On
- **Size**: Width in pixels (default: 200 px). The bubble is always 16:9 aspect ratio.
- dIKta.me automatically prefers a USB camera over the built-in webcam.

> [!NOTE]
> If no camera is connected, the webcam bubble is silently skipped.

### Max Recording Duration (seconds)

The recording stops automatically after this many seconds even if you have not clicked Stop.

- **Default**: 120 seconds (2 minutes)
- Configurable via `VideoRecordingOptions.MaxDurationSeconds`.

---

## Advanced

### Ollama Keep-Alive (seconds)

How long Ollama keeps the local vision model loaded in VRAM after the last inference call.

- **Default**: 300 seconds (5 minutes)
- Increase this if you take multiple screenshots in rapid succession and want to avoid re-loading the model.
- Decrease it to free VRAM sooner for other applications.

### Max Response Tokens

Upper limit on the number of tokens the AI may return for a vision query.

- **Default**: 4 096 tokens
- Reduce for shorter, more focused answers.
- Increase if the AI is truncating long OCR or documentation outputs.

### Temperature

Controls how creative (vs. literal) the AI's response is.

- **Default**: 0.3
- Range: 0.0 (deterministic) to 1.0 (very creative)
- Keep this low for OCR and table extraction. Raise it slightly for descriptive or creative tasks.
