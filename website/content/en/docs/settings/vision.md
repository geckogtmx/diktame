# Vision Settings

The **Vision** settings page controls how dIKta.me captures your screen, which AI models process images and video, and what happens to the results.

---

## AI Models

### Cloud Vision Provider / Model

The cloud provider and model used when the **Cloud** toggle is selected in the Vision Action panel.

*   **Default provider**: Gemini
*   **Default model**: `gemini-2.5-flash`

Any vision-capable model on your configured cloud provider works here:

| Provider | Recommended model |
|---|---|
| Gemini | `gemini-2.5-flash` (default), `gemini-2.0-flash` |
| Anthropic | `claude-opus-4-5`, `claude-sonnet-4-5` |
| OpenAI | `gpt-4o`, `gpt-4o-mini` |

### Local Vision Model

The Ollama model used when the **Local** toggle is selected.

*   **Default**: `minicpm-v`
*   Must be a vision-capable model pulled into Ollama before use.

| Model | Pull command | VRAM |
|---|---|---|
| `minicpm-v` (default) | `ollama pull minicpm-v` | ~2 GB |
| `moondream` | `ollama pull moondream` | ~1.2 GB |
| `llava-phi3` | `ollama pull llava-phi3` | ~2.5 GB |

---

## Capture Behaviour

### Default Query

The text sent to the AI when you submit without typing or recording a question.

**Default:** `Describe what you see and extract any visible text.`

### Auto-Record Voice Query

When enabled, the microphone starts recording automatically after a screenshot is taken so you can speak your question straight away.

*   **Default**: On
*   Stops after the **Query Timeout** elapses with no speech.

### Query Timeout (seconds)

How long dIKta.me waits for voice input before proceeding with the default query.

*   **Default**: 10 seconds

### Max Image Dimension (px)

The longest side an image is allowed before dIKta.me resizes it before sending to the AI.

*   **Default**: 2 048 px
*   If the image still exceeds 1 MB after resizing, it is re-encoded as JPEG at 85 % quality.

---

## Save Folder

Where screenshots and screen recordings are saved on disk.

*   **Default**: `%APPDATA%\DiktaMe\vision\`
*   Enter a custom path or click **Browse** to pick a folder.
*   Leave empty to use the default location.

---

## Output Behaviour

Each Vision action has its own **Inject at cursor** toggle. When on, the AI response is typed into the active window at your cursor. When off, the response goes to the clipboard only.

| Action | Inject at cursor default |
|---|---|
| Clipboard action | On |
| OCR action | On |
| Color Picker | On |
| Video AI (Describe / Document / Bug Report) | On |

---

## Video Recording

### Video Quality

| Setting | Bitrate | Best for |
|---|---|---|
| Low | ~2 500 kbps | Long recordings, limited disk space |
| **Medium** (default) | ~5 000 kbps | General use |
| High | ~10 000 kbps | Detailed screen content, fine text |

Frame rate is fixed at 30 fps.

### Microphone Audio

Captures your microphone during recording.

*   **Default**: On

When enabled, a **Microphone Device** dropdown appears so you can choose which mic to use for the recording.

### System Audio

Captures audio playing on your computer (apps, browser tabs, etc.) via WASAPI loopback.

*   **Default**: On

When enabled, an **Output Device** dropdown appears so you can choose which playback device to capture.

### Webcam Bubble

Overlays a picture-in-picture webcam feed in the bottom-right corner of the recording.

*   **Default**: On
*   **Size**: Bubble width in pixels (default: 200 px, always 16:9 aspect ratio).
*   dIKta.me automatically prefers a USB camera over the built-in webcam.

> [!NOTE]
> If no camera is connected, the webcam bubble is silently skipped.

### Max Recording Duration (seconds)

Recording stops automatically after this many seconds even if you haven't clicked Stop.

*   **Default**: 120 seconds

---

## Action Prompts

The **Action Prompts** section (collapsed by default — click to expand) lets you customise the instructions sent to the AI for each Vision action. Changes apply to both Cloud and Local providers.

| Prompt | Default purpose |
|--------|----------------|
| **OCR** | Extract all text exactly as written, preserving formatting |
| **Video: Describe** | Describe what happens in the recording concisely |
| **Video: Document** | Generate numbered step-by-step instructions |
| **Video: Bug Report** | Generate a structured bug report (Summary / Steps / Environment) |
| **Video: System Prompt** | Base instructions sent with every video analysis request |

The **Cloud** and **Local** tabs each also have their own **System Prompt** and **Default Query** for screenshot/image requests.

> [!TIP]
> Keep OCR prompts short and directive — long creative instructions can interfere with structured extraction.

---

## Advanced

### Ollama Keep-Alive (seconds)

How long Ollama keeps the local vision model loaded in VRAM after the last inference call.

*   **Default**: 300 seconds (5 minutes)
*   Increase this if you take multiple screenshots in quick succession and want to avoid re-loading the model each time.

### Max Response Tokens

Upper limit on the tokens the AI may return for a vision query.

*   **Default**: 4 096

### Temperature

Controls how literal vs. creative the AI's response is.

*   **Default**: 0.3
*   Keep low for OCR and structured extraction. Raise slightly for descriptive tasks.
