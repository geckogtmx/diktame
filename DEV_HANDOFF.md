# Developer Handoff

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 620 passing locally (479 on CI — DPAPI/Clipboard/Audio tests skipped on runners) |
| **Build** | 0 errors, 0 warnings |
| **CI** | Passing on main |
| **Branch** | main |
| **Website** | Deployed on Vercel (dikta.me), Root Directory = `website` |

## Stream L: Deepgram Streaming — IN PROGRESS

### Completed (committed)
- **L.1** (S1): DeepgramSettings, BuildListenUrl, settings UI, Raw toggle fix
- **L.2** (S2): IStreamingSTTProvider interface + AudioRecorder.AudioDataAvailable event (570 tests)
- **L.3** (S3): DeepgramStreamingProvider WebSocket client (29 tests, 599 total)
- **L.4** (S4): StreamingDictationPipeline + factory wiring (21 tests, 620 total)

### Uncommitted (pending manual testing)
- **L.5** (S5): LoadingViewModel integration — streaming/batch dispatch

**What S5 does:** When the user enables "Real-Time Streaming" in Settings > AI Engine (Deepgram section) AND the STT provider supports streaming, dictation uses `StreamingDictationPipeline` instead of the batch record→WAV→transcribe flow. Toggle-stop (pressing hotkey again) still works — it calls `StopRecordingAsync()` which fires `RecordingStopped`, completing the pipeline's internal TCS.

**Key design decisions:**
- Streaming toggle is in **Settings > AI Engine** (Deepgram section), NOT in the Control Panel
- Streaming is always raw mode (no LLM) — text injected as each final arrives
- Streaming toggle (`GeneralSettings.StreamingEnabled`) is independent from the Raw toggle
- If streaming is OFF, Deepgram still works as a batch STT provider (raw or with LLM)

**Files changed (uncommitted):**
- `src/DiktaMe.Core/Config/AppSettings.cs` — `GeneralSettings.StreamingEnabled`
- `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` — `RunDictationPipelineAsync` dispatcher + `RunStreamingDictationAsync` + renamed `RunBatchDictationAsync`
- `src/DiktaMe.App/Views/Settings/AIEngineSettingsPage.xaml` — streaming toggle
- `src/DiktaMe.App/ViewModels/Settings/AIEngineSettingsViewModel.cs` — `DeepgramStreaming` property

**Manual test plan:**
1. Streaming ON + Deepgram → real-time injection, Serilog shows `Starting Streaming Dictate pipeline...`
2. Streaming OFF + Raw ON → batch raw, Serilog shows `Starting Dictate pipeline...`
3. Streaming OFF + Raw OFF → batch with LLM
4. Streaming ON + Whisper → falls back to batch silently

### Remaining (Stream L)
- **S6–S7**: ⏸️ DEFERRED — Flux conversational model. Revisit when Chat gets voice input.

---

## Stream K: OAuth & Trial Credits — COMPLETED (with open bugs)

All K.1–K.7 tasks are implemented and committed. The core flow works:
App → browser (`/login?mode=app`) → OAuth → `diktame://auth?token=JWT` deeplink → app receives token.

### Open Bugs (fix in next session)

#### Bug 1: App UI doesn't update after sign-in
**Root cause:** `HandleAuthCallbackAsync` stores the token and email, then calls `RefreshStatusAsync()` which hits `https://dikta.me/api/trial/status`. That endpoint likely returns an error (not yet provisioning trial records for new users). When it fails, `StatusChanged` event never fires → UI stays on "Sign in".

**Partial fix applied (uncommitted):** Added `StatusChanged?.Invoke(null)` in `HandleAuthCallbackAsync` right after storing token/email, before `RefreshStatusAsync`. This ensures the UI updates even if status sync fails. File: `src/DiktaMe.Core/Account/TrialAccountService.cs` line ~81.

**To verify:** Build app, sign in, check if UserPaneFooter shows email + avatar. If not, check logs at `%LOCALAPPDATA%/diktame/logs/diktame_YYYYMMDD.log` for errors.

#### Bug 2: Website "Sign Up" button shows Coming Soon page
**Root cause:** Vercel env var `NEXT_PUBLIC_COMING_SOON=true` is still set from pre-launch. The middleware at `website/middleware.ts` blocks `/login` and `/auth/*` unless `mode=app`.

**Fix:** In Vercel dashboard → Settings → Environment Variables → delete `NEXT_PUBLIC_COMING_SOON` (or set to `false`). No code change needed. The user said: "No one can actually download the app, so lets just leave it all open as if it was live and working."

#### Bug 3: Trial counter page not showing
**Related to Bug 1.** The Account settings page (`AccountSettingsViewModel`) listens to `StatusChanged` to refresh. Once Bug 1 is fixed, navigating to Settings → Account should show the signed-in state. The trial usage counter won't have real data until the Supabase Edge Function (`/api/trial/status`) returns proper trial records.

### Key Files (Stream K)

| File | Purpose |
|------|---------|
| `src/DiktaMe.Core/Account/TrialAccountService.cs` | OAuth login, JWT storage, status sync, usage reporting |
| `src/DiktaMe.Core/Account/TrialGeminiProvider.cs` | Managed Gemini proxy for trial users |
| `src/DiktaMe.Core/Account/JwtDecoder.cs` | Extract email/expiry from JWT without library |
| `src/DiktaMe.Core/Config/AuthMode.cs` | Enum: None, Trial, ApiKey |
| `src/DiktaMe.Core/Config/TrialSettings.cs` | Trial metadata in AppSettings |
| `src/DiktaMe.App/Services/SingleInstanceManager.cs` | Named mutex + pipe for deeplink forwarding |
| `src/DiktaMe.App/Services/ProtocolRegistrar.cs` | `diktame://` HKCU registry registration |
| `src/DiktaMe.App/Views/Settings/AccountSettingsPage.xaml` | Account tab with usage progress bar |
| `src/DiktaMe.App/Views/Settings/UserPaneFooter.xaml` | Settings nav footer: avatar + email or "Sign in" |
| `website/app/login/page.tsx` | Login page — passes `mode=app` through OAuth |
| `website/app/auth/callback/route.ts` | OAuth callback — issues `diktame://auth?token=JWT` when `mode=app` |
| `website/app/api/auth/app-token/route.ts` | Already-signed-in shortcut — redirects to deeplink |
| `website/app/auth/signout/route.ts` | Sign-out (POST + GET) |

### CI/CD Notes

- **Gitleaks:** `.gitleaks.toml` allowlists `website/QUICKSTART.md` (historical fake JWTs in git history)
- **Test threshold:** `ci/test-threshold.json` set to 470 (local runs 539, CI runs ~479 due to skipped tests)
- **Vercel:** Connected to `geckogtmx/diktame`, Root Directory = `website`

## Remaining Work

| Task | Effort |
|------|--------|
| **H.1** | 1 day — Installer (MSIX or Inno Setup) |
| **LemonSqueezy** | License integration, device binding, trial abuse prevention |
| Latency tuning | Cloud inference profiling |
| ~~L.6–L.7~~ | ⏸️ Deferred — Flux (revisit when Chat gets voice input) |

## Reference Docs

- `DEVELOPMENT_ROADMAP.md` — Full task breakdown
- `ARCHITECTURE.md` — Technical architecture
- `plans/STREAM_K_IMPLEMENTATION.md` — Stream K implementation plan
- `SECURITY.md` — GitHub security policy
- `plans/SPEC_001_MEETINGS.md` / `SPEC_002_VISION.md` / `SPEC_003_TTS.md` — Post-launch feature specs
