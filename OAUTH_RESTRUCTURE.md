# OAuth Restructure Plan — OAUTH_RESTRUCTURE.md

## Context

**Problem**: Auth and Trial are fused together in both the website and desktop app. The result:
1. **Website**: Users sign up via Google OAuth but no `profiles` row is created — the dashboard shows zeroed-out trial data, and API calls return 404
2. **Desktop App**: `ITrialAccountService` handles ALL auth (login, token, email, logout) + trial-specific operations (status sync, usage reporting). `AuthMode.Trial` is hardcoded on login — signing in always means "trial mode"
3. **No decoupling**: Trial credits cannot be turned on/off independently — they're baked into the auth flow

**Goal**: Make auth the foundation layer. Trial becomes an optional add-on. A user can sign in (website or app) without any trial involvement. Free tokens can be enabled/disabled independently.

---

## Master Checklist

### Phase 1: Fix Profile Auto-Creation (Website — make sign-in work)
- [x] **L.1a** — Create Supabase `handle_new_user` trigger (run in SQL Editor + save migration file in repo)
- [x] **L.1b** — Add safety-net profile creation in `website/app/auth/callback/route.ts`
- [x] **L.1c** — Commit pending URL fix (`dikta.me` → `www.dikta.me` in `TrialAccountService.cs`)
- [ ] **Phase 1 test**: Delete your Supabase profile row, sign in on website, verify profile auto-created + dashboard loads

### Phase 2: Restructure Website (Separate profile from trial)
- [x] **L.2a** — Create `/api/account/me` endpoint (generic "who am I", Bearer token support)
- [x] **L.2b** — Make `/api/trial/status` tolerate null trial data (return `trialActive: false` instead of error)
- [x] **L.2c** — Restructure dashboard page (generic profile first, trial section conditional)
- [x] **L.2d** — Update login page copy (account-centric, not trial-centric)
- [x] **L.2e** — Enable Bearer token auth on `/api/profile` (change `createClient` → `createApiClient`)
- [ ] **Phase 2 test**: Sign in on website, see generic dashboard + conditional trial section. Sign in from app, verify `/api/account/me` returns user info.

### Phase 3: Restructure Desktop App (Split auth from trial)
- [x] **L.3a** — Create `IAccountService` interface + `AccountSettings` record (new files)
- [x] **L.3b** — Add `AuthMode.Account = 3` to enum
- [x] **L.3c** — Add `Account` property to `AppSettings` + serialization context
- [x] **L.3d** — Add settings Migration 5 (populate `Account.Email` from `Trial.TrialEmail`)
- [x] **L.3e** — Create `ITrialService` interface (new file)
- [x] **L.3f** — Make `ITrialAccountService` extend both interfaces (bridge pattern) + add `AuthStateChanged`/`IsTrialActive` to `TrialAccountService`
- [x] **L.3g** — Change `HandleAuthCallbackAsync` to set `AuthMode.Account`; `RefreshStatusAsync` upgrades to `AuthMode.Trial` when server confirms active trial
- [x] **L.3h** — Register all three interfaces in DI (single `TrialAccountService` instance)
- [x] **L.3i** — Migrate auth-only consumers to `IAccountService` (`App.xaml.cs`, `UserPaneFooter`, `WizardViewModel`)
- [x] **L.3j** — Remove "Account" from Settings nav menu; keep page navigable from UserPaneFooter click; repurpose page (user info always, trial section conditional)
- [x] **L.3k** — Update `ControlPanelViewModel` badge: "TRIAL" / "ACCT" / "LOC" / "API"
- [x] **L.3l** — Fix `https://dikta.me/dashboard` → `https://www.dikta.me/dashboard` in `AccountSettingsViewModel`
- [x] **L.3m** — Update tests (expect `AuthMode.Account` on login, add trial-upgrade test, add `Account.Email` test)
- [x] **Phase 3 test**: Build + all 552 tests pass. 0 warnings, 0 errors.

### Phase 4: Final Cleanup
- [x] **L.4a** — Mark `ITrialAccountService` as `[Obsolete]`
- [x] **L.4b** — Update `TrialGeminiProvider` + `TrialGeminiAudioProvider` to use `IAccountService` + `ITrialService`
- [x] **L.4c** — Update provider tests for split interfaces
- [x] **Phase 4 test**: Build + all 552 tests pass. 0 warnings, 0 errors.

### Phase 5: End-to-End Verification
- [x] Build: `dotnet build DiktaMe.sln -c Release` — 0 errors, 0 warnings
- [x] Tests: `dotnet test DiktaMe.sln` — 552 pass
- [ ] Website sign-up → profile created → dashboard loads
- [ ] Website sign-in (returning user) → dashboard shows profile
- [ ] Desktop app sign-in → footer shows email → user info page works → trial conditional
- [ ] Desktop app with expired/no trial → badge "ACCT" → uses own API keys
- [ ] Settings migration → old settings.json loads without crash
- [ ] Logout → AuthMode=None → footer shows "Sign in"

---

## Detailed Task Descriptions

### Phase 1: Fix Profile Auto-Creation (Website — make sign-in actually work)

### L.1a — Supabase trigger for automatic profile creation

**Where**: Run in Supabase Dashboard SQL Editor. Save reference copy in `website/supabase/migrations/001_profile_auto_create.sql`

**What**: Create a PostgreSQL trigger that fires on `auth.users` INSERT, creating a default `profiles` row. Trial fields get defaults (15000 quota, 15-day expiry) — these can be nulled out later when trial is disabled.

**Commit**: `fix(db): add handle_new_user trigger for profile auto-creation [L.1a]`

### L.1b — Safety-net profile creation in auth callback

**File**: `website/app/auth/callback/route.ts`

**What**: After `exchangeCodeForSession` succeeds, check if a profile exists. If not (trigger lag, edge case), create one inline. This is belt-and-suspenders — the trigger handles 99% of cases.

**Commit**: `fix(web): ensure profile exists after OAuth callback [L.1b]`

### L.1c — Commit the pending URL fix

**File**: `src/DiktaMe.Core/Account/TrialAccountService.cs`

**What**: StatusUrl and UsageUrl already changed from `dikta.me` to `www.dikta.me` (uncommitted). Commit this.

**Commit**: `fix(account): use www.dikta.me URLs to avoid 307 redirect stripping auth header [L.1c]`

---

## Phase 2: Restructure Website (Separate profile dashboard from trial)

### L.2a — Add `/api/account/me` endpoint

**New file**: `website/app/api/account/me/route.ts`

**What**: Generic "who am I" endpoint. Uses `createApiClient` (Bearer token + cookie support). Returns:
```json
{
  "id": "uuid",
  "email": "user@example.com",
  "name": "User Name",
  "createdAt": "2026-...",
  "hasCustomGeminiKey": false,
  "licenseTier": "free"
}
```
No trial-specific fields. The desktop app will call this instead of `/api/trial/status` to verify auth.

**Commit**: `feat(web): add /api/account/me endpoint [L.2a]`

### L.2b — Make `/api/trial/status` tolerate missing trial data

**File**: `website/app/api/trial/status/route.ts`

**What**: If `trial_expires_at` is null, return `trialActive: false` with zeroed values instead of erroring. Safe to call when trial is disabled.

**Commit**: `fix(web): make trial/status tolerant of null trial data [L.2b]`

### L.2c — Restructure dashboard page

**File**: `website/app/dashboard/page.tsx`

**What**: Show generic user profile first (greeting, account info, quick actions). Trial Credits section becomes conditional — only shown when `trial_expires_at` is set and not expired.

**Commit**: `refactor(web): make dashboard generic with conditional trial section [L.2c]`

### L.2d — Update login page copy

**File**: `website/app/login/page.tsx`

**What**: Change "Sign in to access your trial credits" → "Sign in to your dIKta.me account". Update bullet points to be account-centric.

**Commit**: `refactor(web): update login page to be account-centric [L.2d]`

### L.2e — Enable Bearer token auth on profile API

**File**: `website/app/api/profile/route.ts`

**What**: Change `createClient()` to `createApiClient(request)` in GET/PATCH/DELETE handlers so the desktop app can call this endpoint.

**Commit**: `fix(web): enable Bearer token auth on profile API [L.2e]`

---

## Phase 3: Restructure Desktop App (Split auth from trial)

### L.3a — Create `IAccountService` interface + `AccountSettings` record

**New files**:
- `src/DiktaMe.Core/Account/IAccountService.cs`
- `src/DiktaMe.Core/Config/AccountSettings.cs`

`IAccountService` — auth-only concerns:
```csharp
public interface IAccountService
{
    void Login();
    Task HandleAuthCallbackAsync(string token, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    bool HasValidToken { get; }
    string? Email { get; }
    event Action<bool>? AuthStateChanged; // true=logged in, false=logged out
}
```

`AccountSettings` — auth metadata only:
```csharp
public sealed record AccountSettings
{
    public string Email { get; init; } = string.Empty;
    public string LastSynced { get; init; } = string.Empty;
}
```

**Commit**: `feat(core): add IAccountService and AccountSettings [L.3a]`

### L.3b — Add `AuthMode.Account`

**File**: `src/DiktaMe.Core/Config/AuthMode.cs`

**What**: Add `Account = 3` — means "signed in via OAuth, using own API keys (not trial proxy)". Existing `Trial = 1` means "signed in AND routing through managed proxy". Existing settings.json with `"AuthMode": 1` still deserializes correctly.

```csharp
public enum AuthMode
{
    None = 0,       // Not signed in, BYOK
    Trial = 1,      // Signed in + using managed Gemini proxy
    ApiKey = 2,     // Not signed in, using own keys
    Account = 3,    // Signed in + using own keys
}
```

**Commit**: `feat(core): add AuthMode.Account for signed-in non-trial users [L.3b]`

### L.3c — Add `Account` property to `AppSettings`

**File**: `src/DiktaMe.Core/Config/AppSettings.cs`

**What**: Add `public AccountSettings Account { get; init; } = new();` in the Stream K section. Add `AccountSettings` to `AppSettingsContext`.

**Commit**: `feat(core): add AccountSettings to AppSettings [L.3c]`

### L.3d — Settings migration for `Account` field

**File**: `src/DiktaMe.Core/Config/SettingsManager.cs`

**What**: Migration 5 — if `Account` is null, create it. If `Account.Email` is empty but `Trial.TrialEmail` has a value, copy it over (migrate existing signed-in users).

**Commit**: `feat(core): add settings migration for Account field [L.3d]`

### L.3e — Create `ITrialService` interface

**New file**: `src/DiktaMe.Core/Account/ITrialService.cs`

**What**: Trial-specific operations only:
```csharp
public interface ITrialService
{
    Task<TrialStatus?> RefreshStatusAsync(CancellationToken ct = default);
    Task RecordUsageAsync(string provider, string model, int wordsUsed, CancellationToken ct = default);
    bool IsTrialActive { get; }
    event Action<TrialStatus?>? StatusChanged;
}
```

**Commit**: `feat(core): add ITrialService interface [L.3e]`

### L.3f — Make `ITrialAccountService` extend both interfaces (bridge)

**File**: `src/DiktaMe.Core/Account/ITrialAccountService.cs`

**What**: `ITrialAccountService : IAccountService, ITrialService` — all existing members are now inherited. This is a migration bridge so existing consumers keep compiling.

**File**: `src/DiktaMe.Core/Account/TrialAccountService.cs`

**What**: Add `AuthStateChanged` event + `IsTrialActive` property. Also update `Email` to read from `Account.Email` (with fallback to `Trial.TrialEmail`). Keep `StatusChanged` as-is (satisfies `ITrialService.StatusChanged`).

**Commit**: `refactor(core): make ITrialAccountService extend IAccountService + ITrialService [L.3f]`

### L.3g — Change `HandleAuthCallbackAsync` to set `AuthMode.Account`

**File**: `src/DiktaMe.Core/Account/TrialAccountService.cs`

**What**: Line 78 changes from `AuthMode.Trial` to `AuthMode.Account`. Also writes to `Account.Email`. In `RefreshStatusAsync`, after parsing status with `trialActive: true`, upgrade `AuthMode` from `Account` to `Trial`.

```csharp
// HandleAuthCallbackAsync:
AuthMode = AuthMode.Account,
Account = _settings.Current.Account with { Email = email ?? string.Empty },
Trial = _settings.Current.Trial with { TrialEmail = email ?? string.Empty },

// RefreshStatusAsync (after updating Trial settings):
if (status.TrialActive && _settings.Current.AuthMode == AuthMode.Account)
{
    await _settings.UpdateAsync(_settings.Current with { AuthMode = AuthMode.Trial }, ct);
}
```

**Commit**: `refactor(core): login sets AuthMode.Account, trial sync upgrades to Trial [L.3g]`

### L.3h — Register split interfaces in DI

**File**: `src/DiktaMe.App/App.xaml.cs`

**What**: Register one `TrialAccountService` instance behind all three interfaces:
```csharp
services.AddSingleton<TrialAccountService>();
services.AddSingleton<ITrialAccountService>(sp => sp.GetRequiredService<TrialAccountService>());
services.AddSingleton<IAccountService>(sp => sp.GetRequiredService<TrialAccountService>());
services.AddSingleton<ITrialService>(sp => sp.GetRequiredService<TrialAccountService>());
```

**Commit**: `refactor(app): register IAccountService and ITrialService in DI [L.3h]`

### L.3i — Migrate auth-only consumers to `IAccountService`

**Files**:
- `src/DiktaMe.App/App.xaml.cs` — `HandleDeepLink` resolves `IAccountService` instead of `ITrialAccountService`
- `src/DiktaMe.App/Views/Settings/UserPaneFooter.xaml.cs` — inject `IAccountService`, use `AuthStateChanged`
- `src/DiktaMe.App/ViewModels/WizardViewModel.cs` — inject `IAccountService` for `Login()`

**Commit**: `refactor(app): migrate auth-only consumers to IAccountService [L.3i]`

### L.3j — Remove "Account" from nav menu, keep page navigable from footer

**File**: `src/DiktaMe.App/Views/SettingsWindow.xaml`

**What**: Remove the `<NavigationViewItem Content="Account" Tag="account">` entry from `MenuItems`. The Account page is no longer a nav destination — it's accessed only from the UserPaneFooter.

**File**: `src/DiktaMe.App/Views/SettingsWindow.xaml.cs`

**What**:
- Change footer click handler from `NavView.SelectedItem = NavView.MenuItems[0]` to `ContentFrame.Navigate(typeof(Settings.AccountSettingsPage))` + deselect nav items
- Default selection on load changes from `MenuItems[1]` to `MenuItems[0]` (General is now first)
- Remove `"account"` from the tag→page switch (keep the page type, just not reachable from nav)

**File**: `src/DiktaMe.App/ViewModels/Settings/AccountSettingsViewModel.cs`

**What**: Constructor takes `IAccountService` + `ITrialService` + `SettingsManager`. Add `HasTrialData` property (true when `TrialWordsQuota > 0 && TrialExpiresAt` is set). Trial UI section bound to this.

**File**: `src/DiktaMe.App/Views/Settings/AccountSettingsPage.xaml`

**What**: Repurpose as user info panel:
- **Always visible when signed in**: Email, avatar, name (from server profile), "Manage account" link, "Sign out" button
- **Conditional trial section**: Wrap usage bar / days remaining / quota in `Visibility="{x:Bind ViewModel.HasTrialData, Converter={StaticResource BoolToVis}}"`
- **When signed out**: Keep the "Sign in to your dIKta.me account" InfoBar (update copy from trial-specific to generic)

**Commit**: `refactor(app): remove Account from nav menu, navigate from footer only [L.3j]`

### L.3k — Update badge logic in `ControlPanelViewModel`

**File**: `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`

**What**: Badge shows "TRIAL" for `AuthMode.Trial`, "ACCT" for `AuthMode.Account`:
```csharp
AuthBadgeText = settings.AuthMode switch
{
    AuthMode.Trial => "TRIAL",
    AuthMode.Account => "ACCT",
    _ => IsLocalMode ? "LOC" : "API",
};
```

**Commit**: `refactor(app): add ACCT badge for AuthMode.Account [L.3k]`

### L.3l — Update URLs in AccountSettingsViewModel

**File**: `src/DiktaMe.App/ViewModels/Settings/AccountSettingsViewModel.cs`

**What**: Change `https://dikta.me/dashboard` to `https://www.dikta.me/dashboard` for the "Manage account" link.

**Commit**: `fix(app): use www.dikta.me for dashboard URL [L.3l]`

### L.3m — Update tests

**Files**: Test files in `tests/DiktaMe.Core.Tests/Account/`

**What**:
- Update `HandleAuthCallback` tests to expect `AuthMode.Account` instead of `AuthMode.Trial`
- Add test: `RefreshStatus_TrialActive_UpgradesToTrial` — verify AuthMode upgrades
- Add test: `HandleAuthCallback_SetsAccountEmail` — verify `Account.Email` is populated
- Update mock setups for `IAccountService` and `ITrialService` where applicable

**Commit**: `test(core): update tests for auth/trial separation [L.3m]`

---

## Phase 4: Final Cleanup

### L.4a — Mark `ITrialAccountService` as obsolete

**File**: `src/DiktaMe.Core/Account/ITrialAccountService.cs`

**What**: Add `[Obsolete("Use IAccountService and ITrialService. Will be removed in v3.")]`

**Commit**: `refactor(core): mark ITrialAccountService as obsolete [L.4a]`

### L.4b — Update `TrialGeminiProvider` + `TrialGeminiAudioProvider`

**Files**:
- `src/DiktaMe.Core/Account/TrialGeminiProvider.cs`
- `src/DiktaMe.Core/Account/TrialGeminiAudioProvider.cs`

**What**: Change constructor from `ITrialAccountService` to `IAccountService` (for `LogoutAsync`) + `ITrialService` (for `RecordUsageAsync`).

**Commit**: `refactor(core): update trial providers to use split interfaces [L.4b]`

### L.4c — Update provider tests

**Files**: `tests/DiktaMe.Core.Tests/Account/TrialGemini*Tests.cs`

**What**: Update mocks to use `IAccountService` + `ITrialService`.

**Commit**: `test(core): update trial provider tests for split interfaces [L.4c]`

---

## Phase 5: Verification

### Build + Tests
```bash
dotnet build DiktaMe.sln -c Release "-p:Platform=x64"   # 0 errors
dotnet test DiktaMe.sln                                   # All tests pass
```

### End-to-End Testing

| # | Test | Expected |
|---|------|----------|
| 1 | **Website sign-up**: Visit www.dikta.me → Sign Up → Google OAuth | Profile created, redirected to /dashboard, see greeting + account info + trial section |
| 2 | **Website sign-in (returning user)**: Visit /login → Sign in | Dashboard shows existing profile + usage stats |
| 3 | **Desktop app sign-in**: Settings → Sign in link → browser → OAuth → "Open DiktaMe.App" | Token stored, email shown in UserPaneFooter, AuthMode=Account→Trial after sync, footer shows email+avatar, clicking it navigates to user info page with signed-in state |
| 4 | **Desktop app with expired trial**: Same flow but trial expired | AuthMode stays Account, badge shows "ACCT", pipelines use own API keys, user info page shows email but no trial section |
| 5 | **Settings migration**: Run app with old settings.json (no Account field) | Migration 5 populates Account.Email from Trial.TrialEmail, no crash |
| 6 | **Logout**: Click Sign out in app | AuthMode=None, token cleared, UserPaneFooter shows "Sign in", user info page shows sign-in prompt |

### Settings Backward Compatibility
- Old settings with `"AuthMode": 1` (Trial) → still deserializes as `AuthMode.Trial`
- Old settings with `"AuthMode": 0` (None) → unchanged
- Old settings with no `"Account"` key → deserialized as `new AccountSettings()`, Migration 5 copies email

---

## Dependency Graph

```
Phase 1 (Website fixes — can deploy independently):
  L.1a (trigger) → L.1b (callback safety net)
  L.1c (URL fix — independent)

Phase 2 (Website restructure — depends on Phase 1):
  L.2a (account/me) ← L.1a
  L.2b (status tolerant) — independent
  L.2c (dashboard) ← L.1a
  L.2d (login copy) — independent
  L.2e (profile Bearer auth) — independent

Phase 3 (Desktop restructure — can start in parallel with Phase 2):
  L.3a (interfaces) — independent
  L.3b (AuthMode.Account) — independent
  L.3c (AppSettings.Account) ← L.3a
  L.3d (migration) ← L.3a, L.3c
  L.3e (ITrialService) — independent
  L.3f (bridge) ← L.3a, L.3e
  L.3g (HandleAuth change) ← L.3b, L.3c, L.3d
  L.3h (DI) ← L.3f
  L.3i (consumer migration) ← L.3h
  L.3j (AccountSettings VM) ← L.3h
  L.3k (badge) ← L.3b
  L.3l (URL fix) — independent
  L.3m (tests) ← L.3g

Phase 4 (Cleanup — depends on Phase 3):
  L.4a (obsolete) ← L.3i, L.3j
  L.4b (providers) ← L.3h
  L.4c (provider tests) ← L.4b
```

## Key Design Decisions

1. **Bridge pattern**: `ITrialAccountService` extends `IAccountService + ITrialService`. All existing consumers keep compiling. We migrate them one at a time, then deprecate the bridge.

2. **Single instance, three interfaces**: DI registers one `TrialAccountService` as `IAccountService`, `ITrialService`, and `ITrialAccountService`. No state synchronization issues.

3. **AuthMode.Account vs AuthMode.Trial**: Login always sets `Account`. Trial status sync upgrades to `Trial` only when server confirms `trialActive: true`. Clean separation of "authenticated" from "has free tokens".

4. **Profile creation**: Supabase trigger (server-side) + callback safety net (application-side). No dependency on either alone.

5. **No file renames in Phase 3**: `TrialAccountService.cs` keeps its filename. Renames can happen in a future cleanup after `ITrialAccountService` is fully removed.
