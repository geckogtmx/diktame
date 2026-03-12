# Privacy Settings

The **Privacy** tab controls exactly how much information dIKta.me logs and retains about your dictation sessions. 

Because you dictate private workflows, passwords, and sensitive client data, you need robust options to control exactly how, what, and where things are logged locally on your computer.

## Logging Levels

You can adjust the logging level to dictate exactly how much diagnostic information is saved to your local `logs` directory inside `%APPDATA%\DiktaMe`. 

1.  **Full Logging**: Captures everything. The full contents of your Speech-to-Text transcriptions and the full, formatted output from the Language Model. Best for creating new Prompt Modes or debugging formatting issues.
2.  **Balanced Logging**: Strips out sensitive information before logging the text to your computer's hard drive. It runs a localized scrubbing module built specifically for Privacy preservation. 
3.  **Stats Logging**: Your output text is never saved to the error logs. It only logs the timestamps, error messages (like `API key rejected`), and total LLM token counts generated during the session. 
4.  **Ghost Mode**: dIKta.me logs absolutely nothing locally. Not even token counts or STT provider latency errors are written to disk. The application operates completely namelessly.

> [!CAUTION]
> If you encounter a bug or repeatable error while in **Ghost Mode**, we cannot help you debug it. The error stack traces will not exist!

## PII Scrubbing

If you are using **Balanced Logging**, dIKta.me will locally execute regex-based PII (Personally Identifiable Information) Redaction before anything is written to the diagnostic log files.

The scrubber automatically detects and overwrites:
*   Email addresses (e.g. `user@example.com` becomes `[EMAIL REDACTED]`)
*   US Social Security Numbers
*   Credit card numbers or complex standardized banking patterns
*   Phone numbers

*Note: This scrubbing only applies to your **local log files**. The actual full text is always sent to the active LLM provider (like Anthropic or Ollama) to be formatted correctly before injection.*
