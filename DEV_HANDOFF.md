# Developer Handoff

## Next Session: Stream K — OAuth & Trial Credits

### K.1 Core Models & AppSettings (0.5 day)

1. Add trial-related fields to `AppSettings`:
   - `TrialSessionToken` (encrypted via `SecureStorage`)
   - `TrialEmail`, `TrialWordsUsed`, `TrialWordsQuota`
   - `TrialDaysRemaining`, `TrialExpiresAt`, `TrialActive`, `TrialLastSynced`
2. Add `AuthMode` enum: `None`, `Trial`, `ApiKey`
3. Add `TrialStatus` model class
4. Settings migration for new fields

**Port from:** `E:\git\diktate\src\types\settings.ts` (lines 124–132)

### K.2 TrialAccountService (1 day)

1. `LoginAsync()` — opens browser to `https://dikta.me/login?mode=app`
2. `HandleAuthCallbackAsync(token)` — stores JWT, extracts email, triggers status sync
3. `RefreshStatusAsync()` — GET `/api/trial/status` with Bearer token
4. `RecordUsageAsync(provider, model, wordsUsed)` — POST `/api/trial/usage`
5. `LogoutAsync()` — clears token + trial fields
6. JWT decode helper (extract email, expiry from payload)

**Port from:** `E:\git\diktate\src\ipc\trialHandlers.ts`

### K.3 Protocol Handler (0.5–1 day)

1. Register `diktame://` URL scheme (MSIX manifest or registry fallback)
2. Handle protocol activation in `App.xaml.cs` → route `diktame://auth?token=...`
3. Single-instance check — forward deeplink to existing instance
4. Update V1 website callback to use `diktame://` scheme

**Port from:** `E:\git\diktate\src\main.ts` (lines 57–61, 681–707)

### K.4 Managed Gemini Integration (1 day)

1. `TrialGeminiProvider` — routes through Supabase Edge Function, Bearer JWT auth
2. Wire into `LLMRouter` — `AuthMode == Trial` → managed provider
3. Post-process: `TrialAccountService.RecordUsageAsync()` after each LLM call
4. Handle 403 quota-exceeded, 401 token expiry

**Port from:** `E:\git\diktate\supabase\functions\gemini-proxy\index.ts`

### K.5 Trial Account UI (1 day)

1. Settings "Account" section: sign-in button, usage progress bar, days remaining
2. Control Panel badge: `AuthMode.Trial` → "Trial" badge
3. Configuration Wizard: "Try free" option alongside "Enter API key"
4. Quota exceeded notification → "Add your own API key" prompt

### K.6 Tests (0.5 day)

1. `TrialAccountServiceTests` — login, status sync, usage recording, JWT parsing
2. `TrialGeminiProviderTests` — routing, auth, quota handling
3. `LLMRouter` integration — delegates to trial provider when `AuthMode == Trial`

### K Dependencies

- **Website repo** (`E:\git\diktate\website`): Update OAuth callback `diktate://` → `diktame://`
- **Supabase**: No changes needed (existing Edge Function + DB schema)
- **H.1 (Installer)**: Protocol handler registration may depend on MSIX vs Inno Setup

### V1 Reference Files

| V1 File | What to port |
|---------|-------------|
| `src/types/settings.ts:124-132` | Trial fields in AppSettings |
| `src/ipc/trialHandlers.ts` | TrialAccountService logic |
| `src/main.ts:57-61,681-707` | Protocol handler + deeplink |
| `src/settings/trialAccount.ts` | Trial Account UI |
| `supabase/functions/gemini-proxy/index.ts` | Managed Gemini calling pattern |
| `src/services/configSync.ts` | Status sync pattern |

---

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 521 passing (CI filter: 376) |
| **Build** | 0 errors, 0 warnings |
| **CI** | Passing on main |
| **Branch** | main |

### Remaining Work (after Stream K)

| Task | Effort |
|------|--------|
| **H.1** | 1 day — Installer (MSIX or Inno Setup) |
| **I.6** | 0.5 day — Website rebrand for V2 launch |
| Control Panel wiring | RAW toggle, REFINE toggle, pipeline states |
| Latency tuning | Cloud inference profiling |

### Reference Docs

- `DEVELOPMENT_ROADMAP.md` — Full task breakdown (Stream K at Section 12)
- `ARCHITECTURE.md` — Technical architecture
- `plans/SPEC_001_MEETINGS.md` / `SPEC_002_VISION.md` / `SPEC_003_TTS.md` — Post-launch feature specs
