# Getting Started

Welcome! Let's get you set up with **dIKta.me**. No coding or complex installations required.

## 1. Installation

1. **Download**: Grab the latest `DiktaMe_Setup.exe` from the [Releases page](https://github.com/geckogtmx/diktame/releases).
2. **Run**: Double-click the installer.
   > *First-time Windows SmartScreen warning: click "More info" → "Run anyway" to proceed.*
3. **Done**: dIKta.me launches and settles into your system tray.

---

## 2. First Run Wizard

The first time you open dIKta.me, a short setup wizard walks you through configuration.

### Step 1 — Language

Choose English or Spanish for the app interface.

### Step 2 — Choose your mode

| Mode | What it does | Requires |
|------|-------------|----------|
| **Wallet** | Cloud dictation powered by dIKta.me credits. Just sign in — no API keys needed. | Free account |
| **API Key (BYOK)** | Use your own Deepgram, Gemini, Anthropic, and/or OpenAI keys. Requests go directly to each provider. | Power License |
| **Local** | Fully offline with Whisper.net + Ollama on your machine. | Power License |

> **Wallet is the fastest way to start.** The wizard will open a browser tab for sign-in, then you're done.
>
> **BYOK and Local** require a [Power License](https://www.dikta.me/pricing). If you select one of these without an active license, the wizard will show an activation prompt before continuing.

### Steps 3–5 — STT, LLM, TTS *(BYOK path only)*

If you chose API Key mode, the wizard walks you through:

- **Speech to Text (STT)**: Cloud (Deepgram or OpenAI Whisper) or Local (Whisper.net — downloaded on first use).
- **Language Model (LLM)**: Cloud (Gemini, Claude, GPT, OpenRouter…) or Local (Ollama).
- **Text to Speech (TTS)**: Off, Cloud (Deepgram, OpenAI, or Gemini TTS), or Local (Kokoro).

### Step 6 — API Keys *(if cloud providers selected)*

Enter keys for the cloud providers you chose. Keys are encrypted immediately with Windows DPAPI. This step is skipped automatically if you selected only local providers.

### Step 7 — Test

Make sure your microphone is detected and the AI responds correctly.

### Step 8 — Ready!

You're all set. The Control Panel appears on screen and dIKta.me is ready to use.

---

## 3. How to Use It

dIKta.me lives quietly in your system tray.

- **To Dictate**: Click anywhere you can type and press **`Ctrl+Alt+D`**. Hold it down while you talk, then let go — dIKta.me types what you said.
- **To Tweak**: Right-click the tray icon or click the gear icon in the Control Panel to open Settings.
- **To Switch Modes**: Click the Cloud/Local toggle in the Control Panel HUD.
