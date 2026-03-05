# Stream K: OAuth, Trial Credits & Website Migration — Implementation Plan

**Date:** 2026-03-02
**Predecessor:** All streams complete (A–J). Build: 0 errors, 0 warnings. Tests: 521 passing.
**Website:** Live at [www.dikta.me](https://www.dikta.me) — Vercel + Supabase (already deployed)

---

## Context

V2 is currently BYOK-only — users must provide their own API keys. This blocks the free trial onboarding flow that V1 had. Stream K adds:

1. **OAuth login** via browser → `diktame://auth?token=JWT` deeplink callback
2. **15,000 free words** through dIKta.me's managed Gemini proxy (Supabase Edge Function)
3. **Managed Gemini STT** — trial users get both speech-to-text AND LLM through the same proxy (no API keys, no local model downloads)
4. **Zero-friction wizard** — "Try for free" as the first wizard step, skips all configuration
5. **Trial account UI** in Settings (sidebar user section + Account page)
6. **Website migration** from old repo (`E:\git\diktate\website`) into this repo

The backend (Supabase Auth, Edge Function `gemini-proxy`, database tables `profiles`/`api_usage`) is already live at dikta.me. The Edge Function needs a small update to also accept audio transcription requests (Gemini handles both natively).

---

## User Journey: Website → First Dictation

```
1. dikta.me          → User registers (Google/GitHub OAuth), gets Supabase account
2. dikta.me/download → Downloads installer (gated behind auth)
3. Install + Launch  → Loading screen → settings.json created (WizardCompleted = false)
4. Wizard Step 1     → "How do you want to get started?"
                        ● Try for free (default) — skips entire wizard
                        ○ I have my own API keys — continues to STT/LLM/keys steps
5. Browser opens     → dikta.me/login?mode=app (may already be logged in from step 1)
6. OAuth callback    → diktame://auth?token=JWT → app receives token via named pipe
7. Token stored      → SecureStorage (DPAPI), AuthMode = Trial, status synced
8. App ready         → Control Panel with "TRIAL" badge, hotkeys registered
9. Ctrl+Alt+D        → Record → STT via managed Gemini → LLM via managed Gemini → text injected
```

---

## Execution Order

| # | Task ID | Description | New Files | Modified Files |
|---|---------|-------------|:---------:|:--------------:|
| 1 | K.1 | Core models & AppSettings | 3 | 2 |
| 2 | K.2 | TrialAccountService | 3 | 0 |
| 3 | K.3 | Protocol handler + single-instance | 2 | 1 |
| 4 | K.4a | TrialGeminiProvider (LLM) + LLMRouter wiring | 1 | 2 |
| 5 | K.4b | TrialGeminiAudioProvider (STT) + STTRouter wiring | 1 | 2 |
| 6 | K.5a | Wizard restructure — early trial fork | 2 | 4 |
| 7 | K.5b | Settings window — PaneFooter user section | 2 | 2 |
| 8 | K.5c | Account settings page | 3 | 2 |
| 9 | K.5d | Control Panel badge | 0 | 1 |
| 10 | K.6 | Tests | 5 | 1 |
| 11 | K.7 | Website migration into V2 repo | ~35 | 2 |
| **Totals** | | | **~57** | **~19** |

---

## Phase 1: Core Infrastructure (K.1–K.4)

### 1. K.1 — Core Models & AppSettings

**New file:** `src/DiktaMe.Core/Config/AuthMode.cs`
```csharp
public enum AuthMode { None = 0, Trial = 1, ApiKey = 2 }
```

**New file:** `src/DiktaMe.Core/Config/TrialSettings.cs`
```csharp
public sealed record TrialSettings
{
    public string TrialEmail { get; init; } = string.Empty;
    public int TrialWordsUsed { get; init; }
    public int TrialWordsQuota { get; init; } = 15_000;
    public int TrialDaysRemaining { get; init; }
    public string TrialExpiresAt { get; init; } = string.Empty;   // ISO 8601
    public bool TrialActive { get; init; }
    public string TrialLastSynced { get; init; } = string.Empty;  // ISO 8601
}
```
Non-sensitive metadata only. The JWT token is stored in `SecureStorage` under key `"trial_token"`.

**New file:** `src/DiktaMe.Core/Account/TrialStatus.cs`
- Record matching the GET `/api/trial/status` JSON response
- Fields: `WordsUsed`, `WordsQuota`, `DaysRemaining`, `ExpiresAt`, `TrialActive`

**Modify:** `src/DiktaMe.Core/Config/AppSettings.cs`
- Add properties:
  ```csharp
  public AuthMode AuthMode { get; init; } = AuthMode.None;
  public TrialSettings Trial { get; init; } = new();
  ```
- Add `[JsonSerializable]` attributes for `AuthMode`, `TrialSettings`, `TrialStatus` to `AppSettingsContext`

**Modify:** `src/DiktaMe.Core/Config/SettingsManager.cs`
- No migration logic needed — `AuthMode.None` and `new TrialSettings()` are safe defaults for existing JSON files that don't have these fields

**Commit:** `feat(config): add AuthMode enum and TrialSettings to AppSettings [K.1]`

---

### 2. K.2 — TrialAccountService

**New file:** `src/DiktaMe.Core/Account/ITrialAccountService.cs`

| Method | Purpose |
|--------|---------|
| `LoginAsync()` | Opens browser to `https://dikta.me/login?mode=app` |
| `HandleAuthCallbackAsync(string token)` | Stores JWT in SecureStorage, extracts email, syncs status |
| `RefreshStatusAsync()` → `TrialStatus?` | GET `/api/trial/status` with Bearer token |
| `RecordUsageAsync(provider, model, wordsUsed)` | POST `/api/trial/usage` with Bearer token |
| `LogoutAsync()` | Clears token + resets AppSettings |
| `bool HasValidToken` | Whether a valid token exists in SecureStorage |
| `string? Email` | Cached email from AppSettings.Trial |
| `event StatusChanged` | Raised on login, logout, status sync |

**New file:** `src/DiktaMe.Core/Account/JwtDecoder.cs`

Internal static helper — no external JWT library needed:
- `ExtractEmail(string jwt)` → `string?` — split on `.`, base64url-decode middle segment, parse `email` claim
- `ExtractExpiry(string jwt)` → `DateTimeOffset?` — parse `exp` claim

**New file:** `src/DiktaMe.Core/Account/TrialAccountService.cs`

Implementation details:
- **Constructor:** `SecureStorage`, `SettingsManager`, `HttpClient` (injectable for testing)
- **`LoginAsync`:** `Process.Start(LoginUrl, UseShellExecute: true)`
- **`HandleAuthCallbackAsync`:** store token → extract email via `JwtDecoder` → set `AuthMode.Trial` → call `RefreshStatusAsync`
- **`RefreshStatusAsync`:** GET with Bearer header → parse JSON → update `AppSettings.Trial` → raise `StatusChanged`. On HTTP 401: auto-logout
- **`RecordUsageAsync`:** POST with Bearer header + JSON body `{provider, model, wordsUsed}`. Also update local `TrialWordsUsed` optimistically
- **`LogoutAsync`:** `SecureStorage.DeleteKey("trial_token")` → reset `AuthMode` to `None` + `Trial` to `new()` → raise `StatusChanged(null)`

**Commit:** `feat(account): add ITrialAccountService, JwtDecoder, TrialAccountService [K.2]`

---

### 3. K.3 — Protocol Handler + Single-Instance

**New file:** `src/DiktaMe.App/Services/SingleInstanceManager.cs`

Named mutex + named pipe for inter-process communication:
- `TryAcquire()` → `bool` — acquires `"DiktaMe.V2.SingleInstance"` named mutex
- `StartListening()` — starts named pipe server `"DiktaMe.V2.DeepLink"` in background
- `static SendDeepLinkAsync(uri)` — pipe client: sends URI string to running instance, then exits
- `event Action<string> DeepLinkReceived`

**New file:** `src/DiktaMe.App/Services/ProtocolRegistrar.cs`

Registry-based `diktame://` URL scheme (HKCU, no admin needed):
```
HKCU\Software\Classes\diktame
    (Default) = "dIKta.me"
    URL Protocol = ""
    \shell\open\command
        (Default) = "\"<exe_path>\" \"%1\""
```
- `Register()` — writes registry keys pointing to current exe
- `IsRegistered()` → `bool`
- `Unregister()` — removes keys (called by future uninstaller)

**Modify:** `src/DiktaMe.App/App.xaml.cs` — `OnLaunched`:
1. Parse `Environment.GetCommandLineArgs()` for `diktame://` argument
2. If another instance running (`!singleInstance.TryAcquire()`): send deeplink via pipe, exit
3. If primary instance: start pipe listener, wire `DeepLinkReceived` → parse URI → extract `token` query param → `ITrialAccountService.HandleAuthCallbackAsync(token)` (dispatched via `DispatcherQueue.TryEnqueue`)
4. Register protocol via `ProtocolRegistrar.Register()` during startup
5. Add DI registrations:
   ```csharp
   services.AddSingleton<ITrialAccountService, TrialAccountService>();
   services.AddSingleton<TrialGeminiProvider>();
   services.AddSingleton<TrialGeminiAudioProvider>();
   ```

**Commit:** `feat(app): register diktame:// protocol handler with single-instance support [K.3]`

---

### 4. K.4a — TrialGeminiProvider (LLM) + LLMRouter Wiring

**New file:** `src/DiktaMe.Core/Account/TrialGeminiProvider.cs`

Implements `ILLMProvider`, `IDisposable`:
- Routes requests through Supabase Edge Function proxy URL with Bearer JWT auth
- Reuses same Gemini API response format (same `candidates[0].content.parts[0].text` parsing as `GeminiProvider`)
- After successful call: `_trialService.RecordUsageAsync()` with word count
- Error handling:
  - 401 → call `LogoutAsync()`, return error result
  - 403 → return `LlmResult` with user-friendly quota-exceeded message
- `ProviderName` → `"Gemini (Trial)"`
- Constructor: `SecureStorage`, `ITrialAccountService`, optional `HttpClient`

**Modify:** `src/DiktaMe.Core/LLM/LLMRouter.cs`

Add AuthMode-aware routing (centralized, no DI container rebuild):
- Add `SettingsManager` + `TrialGeminiProvider?` as optional constructor parameters
- In `ProcessAsync`: check `_settings.Current.AuthMode == AuthMode.Trial && _trialProvider != null` → delegate to trial provider before trying primary/fallback
- Existing behavior unchanged when `AuthMode != Trial`

**Modify:** `src/DiktaMe.App/App.xaml.cs` — Update LLM DI wiring:
```csharp
services.AddSingleton<ILLMProvider>(sp => new LLMRouter(
    primary: sp.GetRequiredService<OllamaProvider>(),
    factory: sp.GetRequiredService<ILLMProviderFactory>(),
    settings: sp.GetRequiredService<SettingsManager>(),
    trialProvider: sp.GetRequiredService<TrialGeminiProvider>()));
```

**Commit:** `feat(account): add TrialGeminiProvider and LLMRouter trial routing [K.4a]`

---

### 5. K.4b — TrialGeminiAudioProvider (STT) + STTRouter Wiring

Trial users need STT without providing a Deepgram key or downloading Whisper models. Gemini's multimodal API handles audio natively — the existing `GeminiAudioProvider` already proves this works. We route trial STT through the same managed proxy.

**New file:** `src/DiktaMe.Core/Account/TrialGeminiAudioProvider.cs`

Implements `ISTTProvider`, `IDisposable`:
- Same pattern as `TrialGeminiProvider` but for audio transcription
- Sends audio as base64 inline data to the Supabase Edge Function (same proxy URL)
- Uses Bearer JWT auth (same token)
- Reuses `GeminiAudioProvider`'s request/response format (inline audio + text prompt asking for transcription)
- Records usage via `_trialService.RecordUsageAsync()` after successful transcription
- Error handling mirrors LLM provider: 401 → logout, 403 → quota exceeded
- `ProviderName` → `"Gemini Audio (Trial)"`

**Modify:** `src/DiktaMe.Core/STT/STTRouter.cs`

Add AuthMode-aware routing (same pattern as LLMRouter):
- Add `SettingsManager` + `TrialGeminiAudioProvider?` as optional constructor parameters
- In `TranscribeAsync`: check `_settings.Current.AuthMode == AuthMode.Trial && _trialStt != null` → delegate to trial STT provider
- Existing behavior unchanged when `AuthMode != Trial`

**Modify:** `src/DiktaMe.App/App.xaml.cs` — Update STT DI wiring:
```csharp
services.AddSingleton<ISTTProvider>(sp => new STTRouter(
    primary: sp.GetRequiredService<WhisperProvider>(),
    settings: sp.GetRequiredService<SettingsManager>(),
    trialStt: sp.GetRequiredService<TrialGeminiAudioProvider>()));
```

**Backend note:** The Supabase Edge Function (`gemini-proxy`) already forwards requests to Gemini's `generateContent` API. Gemini's multimodal API accepts audio inline — the proxy just needs to pass through the request body as-is. No Edge Function changes needed if the proxy is a transparent forwarder. If it currently only handles text requests, add an `audio` content type pass-through.

**Commit:** `feat(account): add TrialGeminiAudioProvider and STTRouter trial routing [K.4b]`

---

## Phase 2: UI (K.5a–K.5d)

### 6. K.5a — Wizard Restructure: Early Trial Fork

The wizard currently has 6 steps: Welcome → STT → LLM → API Keys → Test → Ready. For trial users, all of this is unnecessary friction. We restructure step 1 (Welcome) into a **fork point** that lets trial users skip everything.

**New file:** `src/DiktaMe.App/Views/Wizard/WizardGetStartedPage.xaml` + `.cs`

Replaces the current Welcome page as step 0. Layout:

```
How do you want to get started?

● Try for free (default)
  15,000 words free — no API key or setup needed.
  Sign in with your dikta.me account to start.

○ I have my own API keys
  Configure your preferred STT and LLM providers.
```

Two radio buttons. Selecting "Try for free" enables a **"Get Started"** button that:
1. Sets `WizardCompleted = true`
2. Sets `AuthMode = Trial` (pending — will be confirmed when token arrives)
3. Calls `ITrialAccountService.LoginAsync()` → opens browser
4. Closes the wizard → shows main window immediately
5. When the `diktame://auth?token=...` deeplink arrives (could be seconds later), the token is stored and status synced in the background

Selecting "I have my own API keys" proceeds to the existing wizard flow (STT → LLM → API Keys → Test → Ready) unchanged.

**Modify:** `src/DiktaMe.App/Views/Wizard/WizardWindow.xaml.cs`
- Replace step 0 page type from `WizardWelcomePage` to `WizardGetStartedPage`
- Handle the "skip wizard" path when trial is selected

**Modify:** `src/DiktaMe.App/ViewModels/WizardViewModel.cs`
- Add `OnboardingChoice` property: `"trial"` or `"apikeys"`
- Add `StartTrialAsync()` method: sets WizardCompleted, AuthMode, calls LoginAsync, fires WizardCompleted event
- When `OnboardingChoice == "trial"` and user clicks the primary button → `StartTrialAsync()` instead of `GoNextAsync()`

**Modify:** `src/DiktaMe.App/Views/Wizard/WizardWelcomePage.xaml` (or remove/repurpose)
- The old welcome page is no longer needed as a separate step; its content merges into `WizardGetStartedPage`

**Commit:** `feat(ui): restructure wizard with early trial fork — zero-friction onboarding [K.5a]`

---

### 7. K.5b — Settings Window PaneFooter (User Section)

The user/login UI goes in the **bottom-left corner** of the Settings NavigationView sidebar, using `NavigationView.PaneFooter`.

**New file:** `src/DiktaMe.App/Views/Settings/UserPaneFooter.xaml` + `.cs`

A lightweight UserControl placed inside `NavigationView.PaneFooter`:
- **Signed-out state:** Person icon + "Sign in" link/button
- **Signed-in state:** Person icon + email text (truncated) + small "Sign out" link
- Resolves `ITrialAccountService` from DI, subscribes to `StatusChanged`
- Clicking "Sign in" → `ITrialAccountService.LoginAsync()`
- Clicking the signed-in user area → navigates to "Account" settings page

**Modify:** `src/DiktaMe.App/Views/SettingsWindow.xaml`

Add PaneFooter below the NavigationView MenuItems:
```xml
<muxc:NavigationView.PaneFooter>
    <local:UserPaneFooter x:Name="UserFooter"/>
</muxc:NavigationView.PaneFooter>
```

**Modify:** `src/DiktaMe.App/Views/SettingsWindow.xaml.cs`
- Wire `UserFooter` click to navigate ContentFrame to Account page (set NavView selected item)

**Commit:** `feat(ui): add user login section to Settings NavigationView footer [K.5b]`

---

### 8. K.5c — Account Settings Page

**New file:** `src/DiktaMe.App/ViewModels/Settings/AccountSettingsViewModel.cs`

ObservableObject with:
- Properties: `IsSignedIn`, `Email`, `WordsUsed`, `WordsQuota`, `DaysRemaining`, `TrialActive`, `UsagePercent` (0–100 for ProgressBar), `UsageText` ("1,234 / 15,000 words"), `StatusText`
- Commands: `SignInCommand` → `LoginAsync()`, `SignOutCommand` → `LogoutAsync()`, `RefreshCommand` → `RefreshStatusAsync()`
- Subscribes to `ITrialAccountService.StatusChanged` for live updates
- Opens `https://dikta.me/dashboard` via "Manage account" HyperlinkButton

**New file:** `src/DiktaMe.App/Views/Settings/AccountSettingsPage.xaml` + `.cs`

Layout:
- **Signed-out:** InfoBar with "Sign in to try dIKta.me free — 15,000 words, no API key needed" + Sign In button
- **Signed-in:** Email display, ProgressBar (green ≤80%, red >80%) for word usage, days remaining text, "Manage account" link, Sign Out button

**Modify:** `src/DiktaMe.App/Views/SettingsWindow.xaml`

Add NavigationViewItem (insert as first item, before "General"):
```xml
<muxc:NavigationViewItem Content="Account" Tag="account">
    <muxc:NavigationViewItem.Icon>
        <FontIcon Glyph="&#xE77B;"/>  <!-- Person icon -->
    </muxc:NavigationViewItem.Icon>
</muxc:NavigationViewItem>
```

**Modify:** `src/DiktaMe.App/Views/SettingsWindow.xaml.cs`

Add routing case: `"account" => typeof(Settings.AccountSettingsPage)`

**Commit:** `feat(ui): add Account settings page with trial status display [K.5c]`

---

### 9. K.5d — Control Panel Badge

**Modify:** `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`
- Update `AuthBadgeText` logic (lines 318, 474):
  - `AuthMode.Trial` → `"TRIAL"` badge
  - Otherwise existing `"API"` / `"LOC"` logic unchanged

**Commit:** `feat(ui): show TRIAL badge on control panel when in trial mode [K.5d]`

---

## Phase 3: Tests (K.6)

### 10. K.6 — Unit Tests

**New file:** `tests/DiktaMe.Core.Tests/Account/JwtDecoderTests.cs`
- `ExtractEmail_ValidJwt_ReturnsEmail`
- `ExtractEmail_MalformedJwt_ReturnsNull`
- `ExtractExpiry_ValidJwt_ReturnsDateTimeOffset`
- `ExtractExpiry_MissingClaim_ReturnsNull`

**New file:** `tests/DiktaMe.Core.Tests/Account/TrialAccountServiceTests.cs`

Uses `LlmFakeHandler` pattern (from `tests/DiktaMe.Core.Tests/LLM/LLMProviderTests.cs`) for HTTP mocking:
- `HandleAuthCallback_StoresTokenInSecureStorage`
- `HandleAuthCallback_ExtractsEmail_SetsAuthModeTrial`
- `RefreshStatus_ValidResponse_UpdatesAppSettings`
- `RefreshStatus_401_TriggersLogout`
- `RecordUsage_PostsToEndpoint`
- `Logout_ClearsTokenAndResetsAuthMode`
- `HasValidToken_WithStoredToken_ReturnsTrue`

**New file:** `tests/DiktaMe.Core.Tests/Account/TrialGeminiProviderTests.cs`
- `ProcessAsync_ValidToken_SendsBearerAuth`
- `ProcessAsync_ValidResponse_ReturnsText`
- `ProcessAsync_403_ReturnsQuotaExceededResult`
- `ProcessAsync_401_TriggersAutoLogout`
- `ProcessAsync_NoToken_Throws`

**New file:** `tests/DiktaMe.Core.Tests/Account/TrialGeminiAudioProviderTests.cs`
- `TranscribeAsync_ValidToken_SendsBearerAuth`
- `TranscribeAsync_ValidResponse_ReturnsTranscription`
- `TranscribeAsync_403_ReturnsQuotaExceeded`
- `TranscribeAsync_401_TriggersAutoLogout`
- `TranscribeAsync_NoToken_Throws`

**New file:** `tests/DiktaMe.Core.Tests/LLM/LLMRouterTrialTests.cs`
- `ProcessAsync_AuthModeTrial_RoutesToTrialProvider`
- `ProcessAsync_AuthModeNone_RoutesToPrimaryProvider`

**Modify:** `ci/test-threshold.json` — update minimum test count

**Commit:** `test(account): add trial account, provider, JWT, and router tests [K.6]`

---

## Phase 4: Website Migration (K.7)

### 11. K.7 — Migrate Website into V2 Repo

The website currently lives at `E:\git\diktate\website` (Next.js 16, React 19, Tailwind CSS 4, Supabase Auth). It is live at **www.dikta.me** on Vercel. We bring it into this repo so both desktop app and website are managed together.

#### K.7a — Copy source files

Copy from `E:\git\diktate\website\` into `website/` at the repo root:

```
website/
├── app/                     # Next.js App Router pages + API routes + components
│   ├── api/trial/           #   GET /api/trial/status, POST /api/trial/usage
│   ├── api/profile/         #   GET /api/profile
│   ├── auth/callback/       #   OAuth callback handler (deeplink support)
│   ├── auth/signout/        #   Sign-out endpoint
│   ├── dashboard/           #   User dashboard + profile pages
│   ├── login/               #   Login page (mode=app for desktop)
│   ├── coming-soon/         #   Pre-launch gate page
│   ├── features/            #   Features marketing page
│   ├── pricing/             #   Pricing page
│   ├── docs/                #   Documentation page
│   ├── components/          #   React components (Hero, Features, Pricing, etc.)
│   ├── globals.css          #   Tailwind CSS styles
│   ├── layout.tsx           #   Root layout
│   └── page.tsx             #   Home page
├── lib/                     # Supabase clients + animation hooks
│   ├── supabase/            #   server.ts, client.ts
│   └── animations/          #   Scroll/reveal hooks
├── public/                  # Static assets (SVGs, icons)
├── middleware.ts            # Auth session refresh + coming-soon gate
├── next.config.ts
├── package.json
├── package-lock.json
├── tsconfig.json
├── postcss.config.mjs
├── eslint.config.mjs
├── .env.local.example       # Template only — real .env.local NOT committed
├── API_DOCUMENTATION.md
├── OAUTH_SETUP.md
└── QUICKSTART.md
```

**Do NOT copy:** `.next/` (build artifacts), `node_modules/`, `.env.local` (live secrets)

#### K.7b — Update protocol references

Two files need `diktate://` → `diktame://`:

| File | Line | Change |
|------|------|--------|
| `website/app/auth/callback/route.ts` | 23 | OAuth deeplink redirect URL |
| `website/API_DOCUMENTATION.md` | multiple | Documentation references |

#### K.7c — Update .gitignore

Add to repo root `.gitignore`:
```gitignore
# Website
website/node_modules/
website/.next/
website/.env.local
```

#### K.7d — Vercel deployment

Vercel is already deployed from the old repo. Two options:
1. **Reconnect Vercel** to this repo with "Root Directory" set to `website/` (preferred, do as follow-up)
2. **Keep deploying** from old repo temporarily until reconnection is done

No code changes needed for either option — the site will work identically.

**Commit:** `feat(website): migrate dikta.me website into V2 monorepo [K.7]`

---

## File Summary

### New Files (Core)

| File | Project | Purpose |
|------|---------|---------|
| `src/DiktaMe.Core/Config/AuthMode.cs` | Core | AuthMode enum |
| `src/DiktaMe.Core/Config/TrialSettings.cs` | Core | Trial metadata record |
| `src/DiktaMe.Core/Account/TrialStatus.cs` | Core | Server response model |
| `src/DiktaMe.Core/Account/ITrialAccountService.cs` | Core | Service interface |
| `src/DiktaMe.Core/Account/TrialAccountService.cs` | Core | Service implementation |
| `src/DiktaMe.Core/Account/JwtDecoder.cs` | Core | JWT payload decoder |
| `src/DiktaMe.Core/Account/TrialGeminiProvider.cs` | Core | Managed LLM provider |
| `src/DiktaMe.Core/Account/TrialGeminiAudioProvider.cs` | Core | Managed STT provider |

### New Files (App)

| File | Project | Purpose |
|------|---------|---------|
| `src/DiktaMe.App/Services/SingleInstanceManager.cs` | App | Mutex + named pipe |
| `src/DiktaMe.App/Services/ProtocolRegistrar.cs` | App | Registry URL scheme |
| `src/DiktaMe.App/Views/Wizard/WizardGetStartedPage.xaml` + `.cs` | App | Trial fork (replaces Welcome) |
| `src/DiktaMe.App/Views/Settings/UserPaneFooter.xaml` + `.cs` | App | Sidebar user section (bottom-left) |
| `src/DiktaMe.App/ViewModels/Settings/AccountSettingsViewModel.cs` | App | Account page VM |
| `src/DiktaMe.App/Views/Settings/AccountSettingsPage.xaml` + `.cs` | App | Account page |

### New Files (Tests)

| File | Project | Purpose |
|------|---------|---------|
| `tests/DiktaMe.Core.Tests/Account/JwtDecoderTests.cs` | Tests | JWT unit tests |
| `tests/DiktaMe.Core.Tests/Account/TrialAccountServiceTests.cs` | Tests | Service tests |
| `tests/DiktaMe.Core.Tests/Account/TrialGeminiProviderTests.cs` | Tests | LLM provider tests |
| `tests/DiktaMe.Core.Tests/Account/TrialGeminiAudioProviderTests.cs` | Tests | STT provider tests |
| `tests/DiktaMe.Core.Tests/LLM/LLMRouterTrialTests.cs` | Tests | Router integration |

### New Files (Website — ~35 source files)

| Path | Description |
|------|-------------|
| `website/` | Entire Next.js website migrated from `E:\git\diktate\website` |

### Modified Files

| File | Change |
|------|--------|
| `src/DiktaMe.Core/Config/AppSettings.cs` | Add `AuthMode`, `Trial` properties + JsonSerializable attrs |
| `src/DiktaMe.Core/Config/SettingsManager.cs` | No-op migration comment (defaults are safe) |
| `src/DiktaMe.Core/LLM/LLMRouter.cs` | Add `SettingsManager` + `TrialGeminiProvider?` params, trial routing |
| `src/DiktaMe.Core/STT/STTRouter.cs` | Add `SettingsManager` + `TrialGeminiAudioProvider?` params, trial routing |
| `src/DiktaMe.App/App.xaml.cs` | DI registrations, single-instance check, deeplink routing |
| `src/DiktaMe.App/Views/Wizard/WizardWindow.xaml.cs` | Replace step 0 with GetStartedPage, handle trial skip |
| `src/DiktaMe.App/ViewModels/WizardViewModel.cs` | Add `OnboardingChoice`, `StartTrialAsync()` |
| `src/DiktaMe.App/Views/Wizard/WizardWelcomePage.xaml` | Repurposed or removed (merged into GetStartedPage) |
| `src/DiktaMe.App/Views/SettingsWindow.xaml` | Add Account nav item + PaneFooter with UserPaneFooter |
| `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` | Add "account" routing + footer wiring |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | Dynamic AuthBadgeText for Trial mode |
| `ci/test-threshold.json` | Updated test count minimum |
| `.gitignore` | Website exclusions |

---

## V1 Reference Files

| V1 File | What to port |
|---------|-------------|
| `src/types/settings.ts:124-132` | Trial fields in AppSettings |
| `src/ipc/trialHandlers.ts` | TrialAccountService logic |
| `src/main.ts:57-61,681-707` | Protocol handler + deeplink |
| `src/settings/trialAccount.ts` | Trial Account UI patterns |
| `supabase/functions/gemini-proxy/index.ts` | Managed Gemini calling pattern |
| `website/app/auth/callback/route.ts` | OAuth callback (update `diktate://` → `diktame://`) |

---

## API Endpoints (already live at dikta.me)

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| `GET` | `https://dikta.me/api/trial/status` | Bearer JWT | Trial quota, usage, expiration |
| `POST` | `https://dikta.me/api/trial/usage` | Bearer JWT | Record word usage |
| `POST` | `https://volwljbiyzvvcqqdojyf.supabase.co/functions/v1/gemini-proxy` | Bearer JWT | Managed Gemini proxy (LLM + STT audio) |
| Browser | `https://dikta.me/login?mode=app` | — | Opens OAuth flow, redirects to `diktame://auth?token=...` |

---

## Verification

1. **Build:** `dotnet build DiktaMe.sln -c Release` → 0 errors, 0 warnings
2. **Tests:** `dotnet test DiktaMe.sln` → all existing + new tests pass
3. **Protocol:** Launch app → `regedit` confirms `HKCU\Software\Classes\diktame` exists
4. **Single-instance:** Second instance with `diktame://auth?token=test` argument → primary receives token
5. **Wizard trial path:** New user → wizard step 1 → "Try for free" → browser opens → wizard closes → app ready
6. **Wizard API keys path:** "I have my own API keys" → proceeds through STT → LLM → Keys → Test → Ready (unchanged)
7. **Settings UI:** Account tab visible as first item; PaneFooter shows "Sign in" when logged out, email when signed in
8. **Trial dictation:** Ctrl+Alt+D → audio recorded → STT via managed Gemini → LLM via managed Gemini → text injected. No API keys needed.
9. **Control Panel:** Shows "TRIAL" badge when `AuthMode == Trial`
10. **Website:** `cd website && npm install && npm run dev` → site runs at localhost:3000
11. **OAuth callback:** `website/app/auth/callback/route.ts` redirects to `diktame://` (not `diktate://`)

---

## Risks & Notes

- **Named pipe security:** Use unique pipe name and restrict to same-user connections
- **JWT expiry:** Client-side `JwtDecoder` is display-only; server validates on every request. HTTP 401 → auto-logout
- **Registry vs MSIX:** Current approach is registry-based (HKCU, no admin). Future MSIX installer (H.1) may use package manifest instead — `ProtocolRegistrar` can be swapped out
- **Vercel root directory:** When reconnecting Vercel to this repo, set root directory to `website/`. Can be done as a follow-up
- **DispatcherQueue:** Deeplink handler must marshal to UI thread via `DispatcherQueue.TryEnqueue()` before touching UI-bound state
- **Supabase Edge Function:** May need minor update to pass through audio content in request body. Gemini's `generateContent` API handles both text and audio natively — the proxy just needs to be a transparent forwarder.
- **Wizard timing:** The trial user clicks "Get Started" → browser opens → they may already be logged in from website registration. The deeplink callback may arrive while the main window is still loading. The `HandleAuthCallbackAsync` handler must be resilient to being called at any point during app lifecycle.
