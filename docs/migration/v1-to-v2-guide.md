# V1 to V2 Migration Guide

**dIKta.me V2** is a complete rewrite from the V1 Electron/Python application. This guide outlines the key differences and how to migrate your settings.

## ⚠️ Key Differences

| Feature | V1 (diktate) | V2 (diktame) |
|---------|--------------|--------------|
| **Core** | Electron + Python (Slow startup) | Native C# + WinUI 3 (Instant) |
| **Settings** | `config.json` | `settings.json` (Migrated automatically) |
| **STT** | Whisper (Python) | Deepgram / Gemini / Whisper.net |

## 🔄 Automatic Migration

V2 will attempt to automatically import your:
- API Keys (if possible)
- Custom Prompts
- History Database

*Detailed migration steps coming soon.*
