# Dictation Modes

The **Dictation Modes** tab is where you configure exactly *how* the AI formats your spoken words before injecting them into your applications. 

You can create unlimited customized Dictation Presets (like "Casual Tone", "Code Comments", or "Medical Transcriber") and quickly switch between them using the dropdown on the main floating Control Panel.

## Managing Presets

The interface allows you to manage a comprehensive list of different prompts:
*   **Add New**: Creates a blank preset.
*   **Duplicate**: Clones your currently selected preset for easy A/B testing or minor tweaking.
*   **Delete**: Removes the preset from your configuration.

Each preset you create requires a highly descriptive **Name** (e.g., "Spanish Email Writer") so you understand exactly what it does when picking it from the Control Panel.

## Configuring Prompts

For every preset, you must define **two** separate System Prompts:

### 1. Cloud System Prompt
This prompt is sent to premium, highly intelligent API models like Google's Gemini, Anthropic's Claude, or OpenAI's GPT-4o. 
Because these models are massive, you can write intricate, complex instructions here.

*Example Cloud Prompt:*
> "You are an expert C# Developer. The user is going to dictate code comments. Format everything they say correctly according to XML standards. Infer variable names by context and capitalize them properly. Output only the finished comment, nothing else."

### 2. Local System Prompt
This prompt is sent to local models running on your machine via Ollama (like `llama3:8b` or `mistral`).
Because these are lightweight models designed to run quickly on a consumer GPU, they perform better with shorter, highly repetitive, and extremely direct commands. 

*Example Local Prompt:*
> "Format strictly as a technical C# comment. Do not output conversational filler. Ensure spelling accuracy."

## Why separate them?
By having two distinct prompts for every preset, dIKta.me ensures that no matter whether you are running in Cloud Mode (for complex formatting) or Local Mode (for absolute privacy without the internet), your preset will seamlessly execute without missing a beat. You simply flip the "Cloud/Local" switch on the Control Panel and the AI knows exactly which of the two instructions to use silently in the background.
