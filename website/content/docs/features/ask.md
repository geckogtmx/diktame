# Ask

The **Ask** pipeline in dIKta.me is designed for one-way voice queries where you need an answer from an AI, but you do *not* want it to type over whatever you are currently working on.

It is particularly useful for quick questions, fact-checking, or generating code snippets in the background.

## How to use Ask

1. Press and hold the **Ask** hotkey (Default: `Ctrl + Alt + A`).
2. Ask your question (e.g., *"What is the capital of Australia?"*, or *"Give me a regular expression that matches email addresses."*).
3. Release the hotkey.

dIKta.me will transcribe your voice, pass the question to the LLM (using your configured Ask profile), and retrieve the answer.

## Output Modes

Because Ask is designed to be non-intrusive, the answer is handled based on your **Ask Output Mode**. 

You can configure what happens to the answer by navigating to the **General** tab in the Settings window. The options are:

1. **Clipboard & Toast (Default)**: The AI's answer is silently copied to your clipboard so you can paste it (`Ctrl + V`) wherever you like. A small Windows Toast notification will also pop up showing you the answer.
2. **Clipboard Only**: The answer is silently copied to your clipboard. No notification is shown.
3. **Toast Only**: The answer is shown in a Windows summary notification. Your clipboard is untouched.
4. **Inject Only**: The answer behaves exactly like Dictation and is injected at your cursor position immediately.

## Customizing the Ask Prompt

By default, the Ask pipeline has a system prompt optimized for answering questions concisely. 

If you want the Ask pipeline to behave differently (for example, if you want it to *always* output JSON, or *always* answer in the format of a haiku), you can modify its system prompt:

1. Open the Control Panel and click the **Settings** gear.
2. Navigate to the **Modes** tab.
3. Select the **Ask** pipeline.
4. Modify the System Prompt for either your Cloud or Local profile.
