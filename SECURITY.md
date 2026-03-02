# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 2.x     | Yes                |
| 1.x     | No (legacy Electron app, not maintained) |

## Reporting a Vulnerability

If you discover a security vulnerability in dIKta.me, please report it responsibly:

1. **Do NOT open a public GitHub issue**
2. Email **security@dikta.me** with:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
3. You will receive an acknowledgement within 48 hours
4. We will work with you to understand and address the issue before any public disclosure

## Scope

The following are in scope for security reports:

- **Desktop app** (DiktaMe.App) — credential handling, deeplink injection, local privilege escalation
- **Website** (dikta.me) — authentication bypass, API abuse, XSS, CSRF
- **Trial proxy** (Supabase Edge Functions) — token forgery, quota bypass, unauthorized access

## Security Practices

- API keys and tokens are stored using Windows DPAPI encryption (not plaintext)
- JWT tokens are never persisted in settings files — only in encrypted secure storage
- All HTTP communication uses HTTPS
- No telemetry or analytics are collected without user consent
- The desktop app runs with standard user privileges (no admin elevation)

## Secret Scanning

This repository has GitHub secret scanning enabled. Do not commit API keys, tokens, or credentials — even as examples. Use plain-text placeholders like `your-key-here` instead.
