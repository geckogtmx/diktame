# Note

The **Note** pipeline acts as your personal, hands-free scratchpad. 

Instead of injecting text into your current window or placing it on your clipboard, the Note pipeline takes what you say and appends it to a running Markdown/Text file on your hard drive, complete with a timestamp.

This is incredibly useful during meetings, while researching, or when a random thought strikes you while doing focused work—you can capture the thought instantly without switching tabs or losing context.

## How to use Note

1. Press the **Note** hotkey (Default: `Ctrl + Alt + N`).
2. Dictate your note (e.g., *"Note to self: remind John to update the server certificates on Friday."*).
3. Release the hotkey.

dIKta.me will transcribe the note in the background and silently append it to your configured Notes file. By default, this file is located in your Documents folder as `diktame-notes.md`.

## Note Settings

You can fully customize how notes are stored and formatted. Open the **Settings** window and navigate to the **General** tab to find the Note settings:

### File Path
You can change where your notes are saved. You can point this to a generic `.txt` file on your Desktop, an `.md` file in an Obsidian vault, or a synced DropBox folder. dIKta.me will create the file if it does not exist.

### Timestamp Format
Every time you dictate a note, dIKta.me prepends it with a timestamp heading. By default, it uses the format `yyyy-MM-dd HH:mm:ss` (e.g., `## 2026-10-14 09:30:15`), but you can customize this to match standard C# Date/Time formatting guidelines.

### LLM Processing
By default, Notes are passed through your LLM before being saved so they are properly formatted and punctuated. However, you can toggle **LLM Processing** entirely off for Notes. When turned off, dIKta.me will grab the raw transcription from the STT provider and bypass the LLM entirely, making the note-taking process significantly faster.
