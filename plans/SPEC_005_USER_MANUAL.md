# dIKta.me Stream Deck Plugin — User Manual

> **Version:** 1.0.0
> **Requires:** dIKta.me V2, Stream Deck software v6.5+, Windows 10+

---

## What This Plugin Does

The dIKta.me Stream Deck plugin gives you physical buttons for:

1. **Triggering pipelines** — Dictate, Ask, Refine, Translate, Note, Oops, Read Selection — each with its own button, no hotkey memorization.
2. **Mode-specific dictation** — Dedicate a button to a specific dictation mode (e.g., "Developer", "Email") without changing the app's active mode.
3. **Toggling settings** — Flip Raw Mode, Streaming, Audio Ducking, or Cloud/Local engine with one press. The button shows current state.
4. **Visual feedback** — Buttons reflect what the app is doing: idle, recording/processing, or offline.

Works with Stream Deck Plus, Stream Deck XL, Stream Deck MK.2, Stream Deck Mini, Stream Deck Neo, and the iPad Stream Deck app.

---

## Installation

### Prerequisites

- dIKta.me V2 installed and working
- Elgato Stream Deck software v6.5 or later

### Install the Plugin

1. **Close Stream Deck** (right-click tray icon → Quit)

2. **Copy the plugin folder** to Stream Deck's plugin directory:
   ```
   From: src\DiktaMe.StreamDeck\bin\Release\me.dikta.streamdeck.sdPlugin\
   To:   %APPDATA%\Elgato\StreamDeck\Plugins\me.dikta.streamdeck.sdPlugin\
   ```

   Or use the install script (from the `src\DiktaMe.StreamDeck` folder):
   ```cmd
   install-plugin.cmd
   ```
   This automatically stops Stream Deck, copies files, and restarts it.

3. **Start Stream Deck** — the "dIKta.me" category appears in the action list.

### Updating the Plugin

Same steps as installation — the install script removes the old version first.

### Uninstalling

1. Close Stream Deck
2. Delete the folder: `%APPDATA%\Elgato\StreamDeck\Plugins\me.dikta.streamdeck.sdPlugin\`
3. Restart Stream Deck

---

## Actions

### Pipeline Trigger

Triggers a dIKta.me pipeline when you press the button.

**Setup:**
1. Drag "Pipeline Trigger" from the dIKta.me category onto a Stream Deck button
2. In the Property Inspector (right panel), choose the **Pipeline Type**:
   - **Dictate** — start/stop dictation
   - **Ask** — ask a question about selected text
   - **Refine Auto** — automatically refine selected text
   - **Refine Voice** — refine selected text with voice instructions
   - **Translate** — translate selected text
   - **Note** — take a voice note
   - **Oops** — undo the last text injection
   - **Read Selection** — read selected text aloud (TTS)
3. If you selected **Dictate**, a **Mode** dropdown appears — pick a specific dictation mode or leave it on "Use App Default"

**Button states:**
- **Dark** = idle, ready to use
- **Red** = active (recording, processing, or speaking)
- **Grey** = dIKta.me app is not running

**Usage:** Press the button to trigger the pipeline. If the app is already recording, pressing the button stops the recording (same as pressing the hotkey again). If the app is busy with another pipeline, you'll see a brief alert flash.

### Settings Toggle

Toggles a dIKta.me setting on/off with one press.

**Setup:**
1. Drag "Settings Toggle" from the dIKta.me category onto a Stream Deck button
2. In the Property Inspector, choose the **Setting**:
   - **Raw Mode** — bypass LLM processing (dictate raw audio transcription)
   - **Streaming** — use WebSocket streaming instead of batch processing
   - **Audio Ducking** — lower other audio while speaking/recording
   - **Engine** — switch between Cloud and Local processing

**Button states:**
- **Teal** = setting is ON (or Engine = Local)
- **Dark** = setting is OFF (or Engine = Cloud)
- **Grey** = dIKta.me app is not running

**Button labels:**
| Setting | ON label | OFF label |
|---------|----------|-----------|
| Raw Mode | RAW ON | RAW |
| Streaming | STREAM | BATCH |
| Audio Ducking | DUCK ON | DUCK |
| Engine | LOCAL | CLOUD |

**Bidirectional sync:** Changing a setting in the dIKta.me app updates the button, and pressing the button updates the app. They always stay in sync.

---

## Example Setup (Stream Deck Plus — 8 Buttons)

| Button | Action | Config | What It Does |
|--------|--------|--------|-------------|
| 1 | Pipeline Trigger | Dictate, Mode: Standard | General-purpose dictation |
| 2 | Pipeline Trigger | Dictate, Mode: Developer | Code-focused dictation |
| 3 | Pipeline Trigger | Ask | Ask about selected text |
| 4 | Pipeline Trigger | Refine Auto | Polish selected text |
| 5 | Pipeline Trigger | Translate | Translate selected text |
| 6 | Settings Toggle | Raw Mode | Toggle LLM bypass |
| 7 | Settings Toggle | Engine | Switch Cloud/Local |
| 8 | Pipeline Trigger | Oops | Undo last injection |

---

## Troubleshooting

### All buttons show grey (offline)

The plugin can't connect to the dIKta.me app.

- Make sure dIKta.me is running
- Buttons should reconnect automatically within 3 seconds after you launch the app
- If they stay grey, restart Stream Deck

### Button press does nothing

- Check that the dIKta.me app is running (button should not be grey)
- If the button flashes briefly, the app is busy with another pipeline — wait for it to finish
- For Refine, Translate, Ask, and Read Selection: make sure you have text selected in your target app before pressing

### Settings toggle doesn't update the app

- The toggle sends a command over a local pipe — both apps must be running
- Check the dIKta.me app's Settings page to confirm the setting value
- Restart Stream Deck if the issue persists

### Mode dropdown is empty

- The mode list is populated from the dIKta.me app when the plugin connects
- Make sure the app is running, then re-open the Property Inspector (click on the button in Stream Deck)
- If you just created a new dictation mode, the list updates on the next connection

### Plugin doesn't appear in Stream Deck

- Verify the folder exists: `%APPDATA%\Elgato\StreamDeck\Plugins\me.dikta.streamdeck.sdPlugin\`
- The folder must contain `DiktaMe.StreamDeck.exe` and `manifest.json`
- Restart Stream Deck after copying files
- Check that you're running Stream Deck software v6.5 or later

### iPad Stream Deck app

The iPad app works the same way — it communicates with Stream Deck software on your PC, which talks to the plugin. No special setup needed. Just make sure both your PC and iPad are on the same network.

