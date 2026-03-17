# Translate

The **Translate** pipeline allows you to speak in one language and instantly type in another. 

Whether you are communicating in international chat rooms, writing emails to overseas clients, or learning a new language, Translate bridges the gap automatically without needing to open a separate browser tab.

## How to use Translate

1. Place your cursor where you want the translated text to appear.
2. Press the **Translate** hotkey (Default: `Ctrl + Alt + T`).
3. Speak your sentence in your source language.
4. Release the hotkey.

dIKta.me will automatically detect the language you are speaking, transcribe it, translate it into your target language, and inject the final result directly into your active window.

> [!TIP]
> **Fallback**: If the translation engine fails for any reason, dIKta.me will seamlessly inject the raw transcription in your original language so you don't lose what you said.

## Configuring your Target Language

To change the language that dIKta.me translates *into*, you simply update your Translate profile's system prompt:

1. Open the Control Panel and click the **Settings** gear.
2. Navigate to the **Modes** tab.
3. Select the **Translate** pipeline.
4. Modify the System Prompt. 

For example, you can set the prompt to:
*   `Translate this text to Spanish.`
*   `Translate this to formal Japanese.`
*   `Translate the following to English, but keep any programming terms or variable names in English.`

The AI will follow these instructions perfectly every time you use the `Ctrl + Alt + T` hotkey.

## A Note on Streaming

Like all pipelines that rely on an LLM to process and format the text, **Translate operates exclusively in Batch mode**. 

Even if you have Streaming Dictation enabled in your General settings, the Translate pipeline will wait until you finish speaking to ensure the LLM has the full context of your sentence before attempting to translate it.
