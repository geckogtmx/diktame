# API Keys Settings

The **API Keys** tab allows you to integrate dIKta.me with premium, industry-leading Cloud services, letting you Bring Your Own Key (BYOK).

Instead of relying solely on the built-in dIKta.me balance framework, connecting your developer accounts directly grants you absolute control over your API costs and capabilities. 

## Supported Providers

*   **Deepgram API Key**: Used by our Cloud STT pipeline for both Batch and real-time Streaming processing. Extremely high accuracy and blindingly fast recognition rates. Get yours by signing up at deepgram.com.
*   **Gemini API Key**: Used by our Cloud LLM processing engine. Provides robust, incredibly intelligent text transformations. Get your key from Google AI Studio. 
*   **Anthropic API Key**: An alternative Cloud LLM processor capable of using the Claude 3 and 3.5 model families.
*   **OpenAI API Key**: Connects to Whisper STT for Batch translations (if you do not want to use Deepgram) or GPT-4o LLM processing engines.

## Security

Your keys are never broadcast loosely. They are:
1.  **Stored securely**: They are permanently encrypted via standard **Windows DPAPI** integration (the same way Windows protects passwords). 
2.  **Processed locally**: They are never sent to a main dIKta.me server. Only API requests sent directly over HTTPS from your computer explicitly to Google, OpenAI, Deepgram, or Anthropic are processed.

To update an API key, enter the entire string in the masked textbox and press **Save Keys**. You can always click `Clear` to purge keys from the encrypted vault.
