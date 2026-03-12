# Threat Model for dIKta.me V2

## 1. Assets

1. **User Data**
   - Voice recordings (temporary in memory, possibly saved to file if feature enabled)
   - Transcribed text (stored in history database, logs, notes)
   - User settings (including API keys stored encrypted)
   - Notes (saved to markdown files)
   - Clipboard content (temporary)

2. **Application Integrity**
   - Application binaries and configuration
   - Secure storage of API keys

3. **Privacy**
   - User's voice and transcribed content
   - Personal information in transcripts (PII)

## 2. Threat Actors

1. **Local Malicious User**
   - Has physical or logical access to the machine
   - Can run processes, read files, etc.

2. **Malicious Website**
   - Can trigger deep links (diktame://) via browser
   - Can attempt CSRF attacks

3. **Malware**
   - Can run on the user's machine
   - Can attempt to steal API keys, logs, etc.

4. **Network Attacker**
   - Can intercept network traffic (if not using HTTPS)
   - Can perform man-in-the-middle attacks

5. **Insider Threat**
   - Legitimate user with malicious intent
   - Can abuse their access

## 3. Threat Matrix (STRIDE)

### 3.1 Desktop Application (DiktaMe.App)

| Threat Type | Description | Impact | Mitigation |
|-------------|-------------|--------|------------|
| Spoofing | Attacker attempts to impersonate the application or user via deep links | Medium | Validate JWT format, use state parameter for CSRF, rate limit deep links |
| Tampering | Attacker modifies application binaries or configuration | High | Code signing, secure boot, integrity checks (not currently implemented) |
| Repudiation | User denies performing an action; lack of non-repudiation | Low | Audit logs (history database) but not cryptographically signed |
| Information Disclosure | Attacker accesses sensitive data (API keys, transcripts, notes) | High | DPAPI encryption for API keys, PII scrubbing in logs, access controls on files |
| Denial of Service | Attacker makes application unavailable (e.g., via deep link flooding) | Medium | Rate limiting on deep links, input validation |
| Elevation of Privilege | Attacker gains higher privileges than intended | Low | Application runs with standard user privileges, no admin elevation |

### 3.2 Secure Storage (DiktaMe.Core.Security.SecureStorage)

| Threat Type | Description | Impact | Mitigation |
|-------------|-------------|--------|------------|
| Spoofing | N/A | - | - |
| Tampering | Attacker modifies the encrypted keys file | Medium | DPAPI provides integrity protection; tampering will cause decryption failure |
| Repudiation | N/A | - | - |
| Information Disclosure | Attacker decrypts and steals API keys | High | DPAPI encryption tied to user and machine; keys cannot be decrypted on another machine |
| Denial of Service | Attacker deletes or corrupts keys file | Medium | Application handles missing keys gracefully; user can re-enter keys |
| Elevation of Privilege | N/A | - | - |

### 3.3 Pipeline Modules (RefinePipeline, ChatPipeline, AskPipeline)

| Threat Type | Description | Impact | Mitigation |
|-------------|-------------|--------|------------|
| Spoofing | N/A | - | - |
| Tampering | Attacker modifies pipeline code | High | Code integrity (same as application) |
| Repudiation | N/A | - | - |
| Information Disclosure | PII logged in plaintext (historical issue) | High | PII scrubbing now implemented in LogUserText methods |
| Denial of Service | Attacker causes pipeline to hang or crash | Low | Exception handling, timeouts on external calls |
| Elevation of Privilege | N/A | - | - |

### 3.4 NoteWriter (DiktaMe.Core.Data.NoteWriter)

| Threat Type | Description | Impact | Mitigation |
|-------------|-------------|--------|------------|
| Spoofing | N/A | - | - |
| Tampering | Attacker modifies note files | Medium | User controls file location; no integrity protection on notes |
| Repudiation | N/A | - | - |
| Information Disclosure | Path traversal allows writing/reading outside intended directories | High | Path validation restricts to Documents and AppData\DiktaMe |
| Denial of Service | Attacker fills disk with large notes | Low | User-controlled; application does not limit note size |
| Elevation of Privilege | N/A | - | - |

### 3.5 Deep Link Handler (App.xaml.cs)

| Threat Type | Description | Impact | Mitigation |
|-------------|-------------|--------|------------|
| Spoofing | Attacker forges deep link to impersonate legitimate callback | High | JWT format validation, state parameter for CSRF |
| Tampering | Attacker modifies deep link URI in transit | Medium | HTTPS not applicable for custom scheme; relies on URI validation |
| Repudiation | User denies initiating authentication | Low | State parameter ties request to session |
| Information Disclosure | Attacker steals JWT via logs or interception | Medium | JWT not logged; short-lived; HTTPS for token exchange |
| Denial of Service | Attacker floods application with deep links | Medium | Rate limiting (2 second cooldown) |
| Elevation of Privilege | N/A | - | - |

### 3.6 Logging (Serilog)

| Threat Type | Description | Impact | Mitigation |
|-------------|-------------|--------|------------|
| Spoofing | N/A | - | - |
| Tampering | Attacker modifies log files | Low | Logs are append-only; no integrity protection |
| Repudiation | N/A | - | - |
| Information Disclosure | PII in logs (historical issue) | High | PII scrubbing implemented; logs retained only 7 days |
| Denial of Service | Attacker fills disk with logs | Low | Log rotation (7-day retention) |
| Elevation of Privilege | N/A | - | - |

### 3.7 History Database (SQLite)

| Threat Type | Description | Impact | Mitigation |
|-------------|-------------|--------|------------|
| Spoofing | N/A | - | - |
| Tampering | Attacker modifies history database | Medium | No integrity protection; user owns the file |
| Repudiation | N/A | - | - |
| Information Disclosure | Sensitive data in history database (transcripts) | High | Data stored per privacy level; PII scrubbing optional; user can delete |
| Denial of Service | Attacker corrupts or deletes database | Medium | Application handles missing database gracefully |
| Elevation of Privilege | N/A | - | - |

## 4. Mitigations Summary

- **Secrets Management**: API keys encrypted with DPAPI, not stored in plaintext.
- **Input Validation**: Deep link tokens validated for JWT format; file paths validated for traversal.
- **Output Encoding**: PII scrubbing applied to logs and potentially other outputs based on privacy level.
- **Authentication and Authorization**: Deep link uses JWT with state parameter for CSRF protection.
- **Communication Security**: All HTTP communication uses HTTPS.
- **Privacy Controls**: Four privacy levels allow user to control data retention and PII handling.
- **Audit and Logging**: Security-relevant events logged (deep link failures, validation errors).
- **Secure Defaults**: Application runs with standard user privileges; no admin elevation required.

## 5. Threat Modeling Notes

- The application is a desktop application with a significant attack surface being the local machine.
- The most critical threats involve information disclosure of sensitive data (API keys, transcripts).
- Mitigations are in place for the most critical issues identified in past audits (PII logging, path traversal, deep link validation).
- Continuous monitoring and regular security reviews are recommended.
