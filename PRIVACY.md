# Privacy Policy for dIKta.me V2

**Your Voice. Your Machine. Your Data.**

> **Last Updated:** 2026-02-15
> **Version:** 2.0.0

## Summary

dIKta.me V2 is designed as a **Native Windows Application** with rigorous privacy controls.

| Data Type | Storage | Control |
|-----------|---------|---------|
| **Audio** | Memory only (streamed) | Never saved to disk* |
| **API Keys** | Encrypted (Windows DPAPI) | Stored in `%APPDATA%` |
| **Transcripts** | SQLite Database | Pruned after 90 days (configurable) |
| **Telemetry** | None | We do not track you. |

*\*Except when using Feature: Save Recording to File, which is explicit.*

## Cloud vs. Local

### ☁️ Cloud Mode (Deepgram, Gemini, OpenAI)
- **Audio**: Audio data is sent to the provider for transcription.
- **Privacy**: Subject to the provider's API privacy policy (usually "no training on API data").
- **Security**: Transmitted via HTTPS (TLS 1.3).

### 🏠 Local Mode (Whisper.net, Ollama)
- **Audio**: Never leaves your computer.
- **Processing**: 100% offline.
- **Privacy**: Absolute.

## Data Retention

1. **History Database**: Stored at `%APPDATA%\DiktaMe\history.db`.
   - Contains: Timestamps, Transcribed Text, Performance Metrics.
   - **YOU** own this file. You can delete it anytime.

2.  **Logs**: Stored at `%APPDATA%\DiktaMe\logs\`.
    - Auto-deleted after 7 days.
    - **PII Scrubbing**: Automatically attempts to redact emails and phone numbers from logs.

## Your Rights

- **Right to Delete**: Use the "Wipe Data" button in Settings to delete all history and logs instantly.
- **Right to Export**: Your history is a standard SQLite database. You can open it with any SQL tool.
