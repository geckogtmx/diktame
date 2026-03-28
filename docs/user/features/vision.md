# Vision (See)

The **Vision** feature lets you capture anything on your screen — a screenshot, a selected region, or a window — and ask an AI about it. The response can be injected at your cursor, copied to the clipboard, or sent to Quick Chat, without leaving your workflow.

> [!TIP]
> Vision pairs naturally with your voice. After capturing, you can record a spoken question (e.g. *"What does this error mean?"* or *"Summarise the data in this table"*) before the AI processes the image.

## Starting a Vision Capture

Press the **Vision** hotkey from any application:

`Ctrl + Alt + S` (Default)

The screen dims and a transparent snipping overlay appears with a hint bar at the bottom.

## Capture Modes

From the overlay you have two ways to define what to capture:

| Input | What happens |
|---|---|
| **Click** on a window | Captures that specific application window |
| **Click and drag** | Captures a custom rectangular region |
| **Press `F`** | Captures the full active monitor |
| **Press `A`** | Captures all monitors as one wide image |
| **Press `Esc`** | Cancels and returns to your work |

After making a selection the overlay closes and the **Vision Action panel** appears.

## The Vision Action Panel

This step lets you type (or record) an optional question and choose what the AI should do with the screenshot.

### Optional Query

The text field at the top accepts your question. You can type it, or click the **microphone button** to record a voice query (up to 30 seconds). dIKta.me transcribes your question and fills the field automatically.

If you leave the field empty, the default query is used: *"Describe what you see and extract any visible text."*

### Provider Toggle

Switch between **Local** (your configured Ollama vision model, runs on-device) and **Cloud** (Gemini, Claude, or OpenAI, depending on your active profile) for each capture.

### Action Buttons

| Button | What it does |
|---|---|
| **Save** | Saves the screenshot to a file and copies it to the clipboard. No AI involved. |
| **Clipboard** | Sends the image + query to the AI and copies the response to your clipboard. |
| **Chat** | Attaches the screenshot to Quick Chat so you can have a multi-turn conversation about it. |
| **Note** | Runs the vision pipeline and records a voice note appended to your notes file. |
| **OCR** | Extracts all visible text from the screenshot exactly as it appears, and copies it to your clipboard. |
| **Table** | Extracts tabular data from the screenshot as tab-separated values (TSV), ready to paste into Excel or Sheets. |
| **Color** | Opens the [Color Picker](#color-picker) directly on the captured screenshot. |
| **Record** | Starts a [video recording](#video-recording-capture-moments) of the selected region. |

> [!NOTE]
> **Table** always uses the cloud provider regardless of your Local/Cloud toggle, because local models produce unreliable structured output for this task.

## Color Picker

`Ctrl + Alt + C` also opens the Color Picker directly (without going through the Vision overlay).

Once the overlay is open:

- **Move the mouse** to see a live magnifier showing the exact pixel colour under the cursor, with the hex and RGB values.
- **Click** to pick a colour. Picked colours accumulate in a palette strip at the bottom.
- **Backspace** to undo the last pick.
- **Enter** to finish and copy all picked colours to the clipboard.
- **Tab** to finish and send the palette to the AI for a branded colour analysis.
- **Esc** to cancel (if you haven't picked any colours) or finish with the current palette.

## Video Recording (Capture Moments)

`Ctrl + Alt + S` → **Record** (or use your configured video hotkey) starts a screen recording.

The snipping overlay appears so you can select a region or full screen. Once you confirm, the overlay closes and a compact floating bar appears at the top of the screen showing:

- A blinking red dot and elapsed timer
- A **Pause / Resume** button
- A **Stop** button

Recording captures your screen, microphone audio, and system audio simultaneously. An optional webcam bubble (picture-in-picture, bottom-right corner) can be enabled in Settings.

The default maximum recording duration is **120 seconds**.

### After Recording

When you click **Stop** (or the max duration is reached), a post-capture panel appears with these options:

| Button | What it does |
|---|---|
| **Save** | Saves the MP4 file to `%APPDATA%\DiktaMe\vision\`. No AI processing. |
| **Describe** | Uploads the clip to Gemini and returns a description of what happened. |
| **Document** | Asks Gemini to write step-by-step instructions for the actions shown in the recording. |
| **Bug Report** | Asks Gemini to produce a structured bug report based on what it sees. |
| **Chat** | Attaches the clip to Quick Chat for a multi-turn conversation. |

> [!NOTE]
> Video AI actions require a cloud connection. Local Ollama models cannot process MP4 video files. The **Save** action always works offline.

## Output Modes

By default, Vision responses are **injected at your cursor**, exactly like Dictation. You can change the default in **Settings → Vision**:

| Mode | Behaviour |
|---|---|
| **Inject** (default) | Response is typed into the active window at the cursor position. |
| **Clipboard** | Response is copied to the clipboard. A toast notification confirms. |
| **Toast Only** | Response is shown in a Windows notification. Nothing is written or copied. |

## Supported AI Providers

**Cloud:** Gemini (recommended), Claude (Anthropic), OpenAI (GPT-4o and above).

**Local (via Ollama):** Any vision-capable model installed in Ollama. Recommended models:

| Model | Ollama command | Best for |
|---|---|---|
| `minicpm-v` | `ollama pull minicpm-v` | General use, OCR, description (~2 GB VRAM) |
| `moondream` | `ollama pull moondream` | Fast descriptions on low-VRAM hardware (~1.2 GB) |
| `llava-phi3` | `ollama pull llava-phi3` | Stronger reasoning (~2.5 GB VRAM) |

Configure your local vision model in **Settings → Vision → Local Vision Model**.
