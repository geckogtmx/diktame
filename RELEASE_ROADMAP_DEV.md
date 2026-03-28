# RELEASE_ROADMAP_DEV.md — Technical Task Log

> **Purpose:** Session-to-session handoff document. Every task is code-grounded with exact file paths, line numbers, and method signatures. Pick up at any step from any thread.
> **Last updated:** 2026-03-28
> **Companion:** `RELEASE_ROADMAP.md` (business strategy) | This file (engineering execution)

---

## Status Overview

| Task | Status | Sessions Est. | Depends On |
|------|--------|--------------|------------|
| [T1: TTS Wizard Step](#t1-tts-wizard-step) | `DONE` | 1 | — |
| [T2: License Gate System](#t2-license-gate-system) | `DONE` | 2 | — |
| [T3: Inno Setup Installer](#t3-inno-setup-installer) | `DONE` | 1 | — |
| [T4: CHANGELOG.md](#t4-changelogmd) | `DONE` | 0.5 | — |
| [T5: Wizard License Integration](#t5-wizard-license-integration) | `DONE` | 1 | T1, T2 |
| [T6: Website Updates](#t6-website-updates) | `DONE` | 1 | T2 |
| [T7: CI Release Pipeline](#t7-ci-release-pipeline) | `DONE` | 0.5 | T3 |
| [T8: Manual Testing (P0)](#t8-manual-testing-p0) | `TODO` | 2-3 | T1-T5 |
| [T9: Tag & Ship](#t9-tag--ship) | `TODO` | 0.5 | T8 |

All T1-T4 are parallelizable. T5 depends on T1+T2. T6-T7 can run in parallel after their deps.

---

## DONE (Session 3 — 2026-03-28)

- [x] T5: Wizard license integration — license gate UI on WizardGetStartedPage (BYOK/Local) + WizardTtsPage (Local Kokoro), GoNextAsync guard in WizardViewModel, 3 localization keys (EN+ES), LicenseStateChanged live updates
- [x] T3: Inno Setup installer — `installer/diktame-setup.iss` (LZMA2, bilingual EN+ES, per-user, no admin), `installer/build-installer.cmd`, verified locally: 47 MB output
- [x] T7: CI release pipeline — `ci-v2.yml` extended with Inno Setup build step + installer artifact (30-day), release job on `v*` tags via `softprops/action-gh-release@v2`
- [x] T4: CHANGELOG.md — Keep a Changelog format, comprehensive V2.0.0 entry covering all shipped streams
- [x] Inno Setup 6 installed locally via winget (`%LOCALAPPDATA%\Programs\Inno Setup 6\`)
- [x] Build: 0 warnings, 0 errors | Tests: 1,134 passed, 0 failed
- [x] RELEASE_ROADMAP_DEV.md status table updated (T1-T5, T7 → DONE)

## DONE (Session 2 — 2026-03-28)

- [x] T1: TTS Wizard Step — 8-step wizard, WizardTtsPage (off/cloud/local), Kokoro download with progress, EN+ES strings
- [x] T2: License Gate System — LicenseManager with RSA-2048, PipelineFactory gate, Account Settings UI, 12 localization strings (EN+ES), key pair generated
- [x] Build verified, pushed to origin/main: commit `90a2a16`, `51fe1fa`

## DONE (Session 1 — 2026-03-27)

- [x] MIT LICENSE file created at root
- [x] FIX-1: Wallet terminology — EN: "Trial Credits" → "Wallet" | ES: "Creditos de Prueba" → "Monedero"
- [x] RELEASE_ROADMAP.md written (16 sections, competitor analysis, pitches, marketing playbook)
- [x] CONTRIBUTING.md + CONTRIBUTING.es.md rewritten
- [x] Brand Book tagline updated
- [x] Pushed to origin/main: commit `d748fda`

---

## T1: TTS Wizard Step

**Goal:** Add TTS configuration as wizard step 4 (Off / Local Kokoro / Cloud). Shift subsequent steps.

### Files to Create

**`src/DiktaMe.App/Views/Wizard/WizardTtsPage.xaml`**
- RadioButtons with 3 options:
  - Off (default, no download, no friction)
  - Cloud TTS (if wallet/BYOK — works now, uses credits)
  - Local Kokoro (~88MB download — fast, free forever)
- Download progress panel (ProgressBar + status TextBlock), hidden by default
- Follow pattern from `WizardSttPage.xaml` (radio + progress layout)

**`src/DiktaMe.App/Views/Wizard/WizardTtsPage.xaml.cs`**
- Implement `IWizardStepPage` interface
- `SetViewModel()`: bind `_viewModel`, register `BeforeLeaveStep = OnBeforeLeaveStepAsync`
- `OnBeforeLeaveStepAsync()`: follow exact pattern from `WizardSttPage.xaml.cs` lines 92-158:
  - If "off" or "cloud" selected → return `true` (no download needed)
  - If "local" and Kokoro already downloaded → return `true`
  - If "local" and not downloaded:
    - Set `_viewModel.CanGoNext = false`
    - Show progress panel
    - Create `CancellationTokenSource`
    - Await Kokoro download (use `KokoroTtsProvider.DownloadModelAsync()` or equivalent)
    - On success: show "Ready", set `CanGoNext = true`, return `false` (stay for user to see)
    - On cancel/error: show message, set `CanGoNext = true`, return `false`
- Progress handler: `DispatcherQueue.TryEnqueue()` for UI thread safety (pattern at `WizardSttPage.xaml.cs` lines 160-167)

### Files to Modify

**`src/DiktaMe.App/ViewModels/WizardViewModel.cs`**

| Change | Location | Detail |
|--------|----------|--------|
| Add property | After line 34 | `[ObservableProperty] private string _ttsChoice = "off";` |
| Update constant | Line 40 | `public const int TotalSteps = 8;` (was 7) |
| Update `NeedsApiKeys()` | Lines 289-291 | Add `\|\| string.Equals(TtsChoice, "cloud", StringComparison.Ordinal)` |
| Update `GoNextAsync()` skip logic | Line 119 | Change API Keys step index from 4 to 5: `if (CurrentStep == 5 && !NeedsApiKeys())` |
| Update `CompleteWizardAsync()` | Lines 223-287 | Write TTS settings based on `TtsChoice`: `Tts.Enabled`, `Tts.Provider` |
| Update `StartLocalAsync()` | Lines 183-221 | Enable Kokoro TTS: set `Tts = new TtsSettings { Enabled = true, Provider = "kokoro" }` |

**`src/DiktaMe.App/Views/WizardWindow.xaml.cs`**

| Change | Location | Detail |
|--------|----------|--------|
| Insert step in array | Lines 14-23 | Insert `typeof(WizardTtsPage)` at index 4, shifting ApiKeys→5, Test→6, Ready→7 |

```csharp
private readonly Type[] _stepPages =
{
    typeof(WizardLanguagePage),      // 0
    typeof(WizardGetStartedPage),    // 1
    typeof(WizardSttPage),           // 2
    typeof(WizardLlmPage),           // 3
    typeof(WizardTtsPage),           // 4  ← NEW
    typeof(WizardApiKeysPage),       // 5  (was 4)
    typeof(WizardTestPage),          // 6  (was 5)
    typeof(WizardReadyPage),         // 7  (was 6)
};
```

**`src/DiktaMe.App/Strings/en/Resources.resw`** — Add keys:
```
Wizard_Tts_Title              = "Text-to-Speech"
Wizard_Tts_Subtitle           = "Choose how dIKta.me reads text back to you"
Wizard_Tts_Off                = "Off"
Wizard_Tts_Off_Desc           = "Skip for now. You can enable TTS later in Settings."
Wizard_Tts_Cloud              = "Cloud TTS"
Wizard_Tts_Cloud_Desc         = "Uses wallet credits. Works immediately."
Wizard_Tts_Local              = "Local (Kokoro)"
Wizard_Tts_Local_Desc         = "~88 MB download. Fast, free, private. Runs on your machine."
Wizard_Tts_Downloading        = "Downloading Kokoro model... {0}%"
Wizard_Tts_Ready              = "Kokoro ready. Click Next to continue."
Wizard_Tts_Failed             = "Download failed. You can try again or skip for now."
```

**`src/DiktaMe.App/Strings/es-MX/Resources.resw`** — Spanish equivalents.

### Verification
- `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors
- `dotnet test DiktaMe.sln` — all tests pass
- Wizard step count shows 8 total
- "Off" selection: step 4 → step 5 (or skip to 6 if no API keys needed)
- "Local" selection: triggers download, blocks Next during download, shows progress
- "Cloud" selection: no download, proceeds to API keys step
- Back button from TTS step returns to LLM step
- Local path (step 1 "local") now sets `Tts.Enabled = true, Provider = "kokoro"`

---

## T2: License Gate System

**Goal:** Free = Wallet cloud only. Power License ($20) = unlocks local STT/LLM/TTS + BYOK.

### T2.1: LicenseManager Service

**Create:** `src/DiktaMe.Core/Security/LicenseManager.cs`

```csharp
public sealed class LicenseManager
{
    private readonly SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private const string LicenseKeyName = "license_key";

    // Core API
    public bool IsLicensed { get; private set; }
    public string? LicenseTier { get; private set; }  // "power"
    public event Action<bool>? LicenseStateChanged;

    public async Task ActivateLicenseAsync(string licenseKey, CancellationToken ct = default);
    public void LoadFromStorage();  // Called at startup
    public bool ValidateOffline(string licenseKey);  // RSA signature check
    public void Deactivate();
}
```

**SecureStorage integration:**
- Add `"license_key"` to `ValidProviders` array in `SecureStorage.cs` (line 43-51)
- Store: `_secureStorage.StoreKey("license_key", key)`
- Retrieve: `_secureStorage.RetrieveKey("license_key")`

**Offline validation approach (simplest):**
- License key format: `DIKTAME-{base64_payload}.{base64_signature}`
- Payload: `{ "email": "...", "tier": "power", "issued": "2026-04-01T..." }`
- Signature: RSA-SHA256 with embedded public key
- Public key: embedded as `const string` in `LicenseManager.cs`
- Private key: stays in Supabase edge function only
- No phone-home required. Ever.

**Register in DI:** `services.AddSingleton<LicenseManager>();` in `App.xaml.cs`

### T2.2: License Key Generation (Server-Side)

**Create:** `website/supabase/functions/generate-license/index.ts`

- Input: `{ email: string, tier: "power", order_ref: string }`
- Output: `{ license_key: "DIKTAME-{payload}.{signature}" }`
- RSA-SHA256 signing with private key from Supabase secrets
- Idempotent: same email+order_ref → same key

**Extend:** `website/supabase/functions/wallet-webhook/adapters/lemonsqueezy.ts`
- In `parseLemonSqueezyEvent()` (lines 67-129): after successful order
- Map Power License product ID → call `generate-license` → email key to buyer
- Use existing `PRODUCT_TIER_MAP` (lines 18-22) — add Power License product ID

### T2.3: Gate Checks in Core

**`src/DiktaMe.Core/Config/PipelineFactory.cs` — GetProviders() (line 239)**

Insert before existing wallet check:

```csharp
private (ISTTProvider Stt, ILLMProvider? Llm) GetProviders(string mode, string? modeOverride)
{
    // Wallet mode — no license needed, cloud proxies handle it
    if (_settings.Current.AuthMode == AuthMode.Wallet && _walletStt is not null && _walletLlm is not null)
    {
        return (_walletStt, _walletLlm);
    }

    // License gate — local/BYOK providers require Power License
    string effectiveMode = modeOverride ?? mode;
    ModeSettings ms = _profiles.GetModeSettings(effectiveMode);

    if (!_licenseManager.IsLicensed)
    {
        bool needsLocal = ms.SttProvider is "whisper"
                       || ms.LlmProvider is "ollama"
                       || !string.IsNullOrEmpty(ms.LlmModel); // BYOK implies own keys

        if (needsLocal)
        {
            Log.Warning("PipelineFactory: Local/BYOK provider requires Power License. Falling back to wallet.");
            // Fallback to wallet if available, otherwise throw
            if (_walletStt is not null && _walletLlm is not null)
                return (_walletStt, _walletLlm);

            throw new InvalidOperationException("Power License required for local providers.");
        }
    }

    // ... rest of existing method (line 245+)
}
```

**Settings pages to gate:**
- `AIEngineSettingsPage` — BYOK key entry fields: disable if not licensed, show "Requires Power License" overlay
- `HardwareSettingsPage` — Whisper/Ollama model download buttons: gate behind license
- `TtsSettingsPage` — Local Kokoro options: gate behind license

Pattern: check `App.Current.Services.GetRequiredService<LicenseManager>().IsLicensed` in page `OnNavigatedTo()`.

### T2.4: Account Settings UI

**Modify:** `src/DiktaMe.App/Views/Settings/AccountSettingsPage.xaml`

Add license section below existing sign-in bar:
- "Power License" card with:
  - If licensed: green checkmark + tier + "Licensed" status
  - If not licensed: "Unlock local AI" description + "Buy Power License" button (→ LemonSqueezy) + "Enter license key" text field + "Activate" button
- Use existing `SettingsCard` pattern from other settings pages

### T2.5: Strings

**EN Resources.resw — Add:**
```
License_Title                 = "Power License"
License_Unlocked              = "Licensed"
License_NotLicensed           = "Free (Wallet only)"
License_BuyButton             = "Buy Power License — $20"
License_ActivateButton        = "Activate"
License_KeyPlaceholder        = "Paste your license key"
License_InvalidKey            = "Invalid license key. Check your email for the correct key."
License_Activated             = "License activated! Local AI is now unlocked."
License_Required              = "Requires Power License"
License_RequiredDesc          = "Unlock local STT, LLM, TTS, and bring-your-own API keys."
License_FallbackToast         = "Local mode requires Power License. Using cloud instead."
```

**ES Resources.resw** — Spanish equivalents.

### Verification
- Unlicensed: Wallet path works normally, cloud providers function
- Unlicensed: selecting local providers in Settings shows gate overlay
- Unlicensed: wizard Local/BYOK paths show upgrade prompt
- Licensed: all providers work, no restrictions
- License key survives app restart (DPAPI persistence)
- Invalid key shows error message
- `dotnet test` — all existing tests pass (mock LicenseManager in pipeline tests)

---

## T3: Inno Setup Installer

**Goal:** Single `.exe` installer wrapping `publish/win-x64/` output.

### Create: `installer/diktame-setup.iss`

```iss
[Setup]
AppName=dIKta.me
AppVersion=2.0.0
AppPublisher=geckogtmx
AppPublisherURL=https://dikta.me
DefaultDirName={autopf}\dIKta.me
DefaultGroupName=dIKta.me
OutputDir=output
OutputBaseFilename=dIKta.me-2.0.0-Setup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=..\src\DiktaMe.App\Assets\tray-icon.ico
UninstallDisplayIcon={app}\DiktaMe.App.exe
PrivilegesRequired=lowest
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\dIKta.me"; Filename: "{app}\DiktaMe.App.exe"
Name: "{autodesktop}\dIKta.me"; Filename: "{app}\DiktaMe.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\DiktaMe.App.exe"; Description: "Launch dIKta.me"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Don't delete %APPDATA%\DiktaMe\ — preserve user settings and data
```

### Create: `installer/build-installer.cmd`

```cmd
@echo off
echo === Building dIKta.me Installer ===

echo [1/2] Publishing release...
call ..\publish-release.cmd
if errorlevel 1 (echo Publish failed & exit /b 1)

echo [2/2] Building installer...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" diktame-setup.iss
if errorlevel 1 (echo Installer build failed & exit /b 1)

echo === Done: output\dIKta.me-2.0.0-Setup.exe ===
dir output\*.exe
```

### Key Decisions
- `PrivilegesRequired=lowest` — installs per-user, no admin needed
- `{autopf}` — auto-selects Program Files based on architecture
- `MinVersion=10.0.17763` — matches csproj `TargetPlatformMinVersion`
- Uninstall preserves `%APPDATA%\DiktaMe\` (settings, history, wallet)
- LZMA2 ultra compression: ~175MB → ~65-70MB installer
- Bilingual installer (EN + ES)

### Verification
- `iscc.exe diktame-setup.iss` builds without errors
- Installer runs on clean Windows 10 (1809+) and Windows 11
- Installs to `C:\Users\{user}\AppData\Local\Programs\dIKta.me\`
- Desktop shortcut + Start Menu entry created
- App launches from shortcut
- Uninstall removes program files, preserves `%APPDATA%\DiktaMe\`
- Reinstall over existing installation works
- SmartScreen warning appears (unsigned) — document workaround

---

## T4: CHANGELOG.md

**Goal:** Keep Changelog format documenting V2.0.

### Create: `CHANGELOG.md`

Base on `DEVELOPMENT_ROADMAP.md` completed streams. Scannable, not exhaustive.

**Sections:**
- `[2.0.0] - 2026-04-XX` (fill date at release)
- **Added:** 8 workflow modes, multi-provider STT/LLM/TTS, wizard, wallet/auth, vision module, Quick Chat, 3 themes, audio ducking, i18n (EN + ES), system tray, 1014 tests
- **Changed:** Complete rewrite from Python/Electron to C#/.NET 8/WinUI 3
- **Architecture:** Self-contained x64, ~70MB installer, local-first

### Source of Truth for Feature List
- `DEVELOPMENT_ROADMAP.md` — completed streams A through L + TTS + Vision + UI Revamp
- `RELEASE_ROADMAP.md` Section 2 — V2.0 feature set summary

---

## T5: Wizard License Integration

**Goal:** Connect T1 (TTS wizard) + T2 (license gate) into the wizard flow.

**Depends on:** T1 and T2 both complete.

### Modify: `WizardGetStartedPage.xaml`

Current 3 radio buttons: Wallet / BYOK / Local

Add license status awareness:
- **Wallet radio:** Always available. Default selection. No change.
- **BYOK radio:** If `!IsLicensed`, show inline "Requires Power License" + buy button. Disable radio or show upgrade inline.
- **Local radio:** Same gate as BYOK.

**Pattern:** Don't hide the options. Show everything. Gate with a warm upgrade prompt, not a lock icon.

### Modify: `WizardViewModel.cs`

**`GoNextAsync()` step 1 forks (lines 90-100):**
- Wallet path: no change (always free)
- BYOK path: check `_licenseManager.IsLicensed`. If not → show upgrade UI instead of proceeding.
- Local path: same check.

**`StartLocalAsync()` (lines 183-221):**
- Add guard: `if (!_licenseManager.IsLicensed) { /* show upgrade */ return; }`

### Modify: `WizardTtsPage.xaml.cs`

- "Local Kokoro" radio: if `!IsLicensed`, show "Requires Power License" overlay on that option
- "Cloud TTS" and "Off": always available regardless of license

### Strings

```
Wizard_LicenseRequired        = "Power License required"
Wizard_LicenseUpgrade         = "Unlock local AI — $20, one time"
Wizard_LicenseBuyButton       = "Get Power License"
Wizard_LicenseOrBuild         = "Or build from source — it's MIT"
```

### Verification
- Unlicensed + Wallet path: works end-to-end, no gate
- Unlicensed + BYOK path: shows upgrade prompt, buy link works
- Unlicensed + Local path: shows upgrade prompt, buy link works
- Licensed + any path: no restrictions, all options available
- TTS step: "Off" and "Cloud" always available, "Local" gated when unlicensed
- After activating license mid-wizard: options unlock immediately

---

## T6: Website Updates

**Goal:** Align dikta.me website with V2.0 launch decisions (MIT, license model, pricing).

**Depends on:** T2 (license gate design finalized).

### Files to Review/Update

| File | Check For |
|------|-----------|
| `website/app/components/PricingSection.tsx` | Tier names, prices, feature lists |
| `website/app/[locale]/pricing/page.tsx` | Schema.org markup, meta tags |
| `website/messages/en.json` | "trial", "source-available" → "MIT" / "open source" |
| `website/messages/es.json` | Spanish equivalents |
| `website/app/[locale]/page.tsx` | Landing page hero copy, tagline |
| Footer / Terms / About sections | License mentions |

### Key Changes
- "Source-available" → "MIT licensed, open source"
- "Build It Yourself" tier: update description to reflect MIT reality
- Power License tier: update to reflect local AI unlock (not just "full app")
- Free Trial tier: clarify it's Wallet (cloud) with $1 promo credit
- Primary tagline: "Stop typing at your AI models. Just talk to them."
- Add download link pointing to GitHub Releases (when installer ships)

---

## T7: CI Release Pipeline

**Goal:** Extend `.github/workflows/ci-v2.yml` to build installer and create GitHub Releases.

**Depends on:** T3 (installer script exists).

### Modify: `.github/workflows/ci-v2.yml`

**Add after "Publish (win-x64)" step (line 142):**

```yaml
    - name: Install Inno Setup
      run: choco install innosetup -y --no-progress

    - name: Build installer
      working-directory: installer
      run: |
        & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" diktame-setup.iss
      shell: pwsh

    - name: Upload installer artifact
      uses: actions/upload-artifact@v4
      with:
        name: diktame-installer
        path: installer/output/*.exe
        retention-days: 30
```

**Add release job (triggered on tags):**

```yaml
  release:
    needs: ci
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: diktame-installer

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: "*.exe"
          generate_release_notes: true
```

### Verification
- CI builds installer on every push (artifact available for 30 days)
- Pushing `v2.0.0` tag creates GitHub Release with installer attached
- Release notes auto-generated from commits since last tag

---

## T8: Manual Testing (P0)

**Goal:** Verify critical paths before release.

| Journey | What to Test | Est. Hours |
|---------|-------------|-----------|
| 1: Cloud Dictation | Wallet sign-up → $1 credit → Dictate/Refine/Ask/Translate modes via Deepgram+Gemini | 3 |
| 3: Local Dictation | Power License activate → Whisper download → Ollama pull → all modes locally | 2 |
| 6: Auth + Wallet | Sign in/out, JWT refresh, wallet balance display, top-up flow | 1.5 |
| License Gate | Unlicensed restrictions, activation, persistence across restart | 1 |
| TTS Wizard | All 3 options (off/cloud/local), download/cancel/retry, back navigation | 1 |
| Installer | Fresh install Win10/11, upgrade, uninstall, shortcut, preserve settings | 1 |
| Cross-cutting | 3 themes, control panel, tray icon, hotkeys | 1.5 |

**Total: ~11 hours**

### Test Machines Needed
- Clean Windows 11 (primary)
- Clean Windows 10 1809+ (min version)
- Existing install (upgrade path)

---

## T9: Tag & Ship

1. Finalize CHANGELOG.md date
2. `git tag -a v2.0.0 -m "dIKta.me V2.0.0"`
3. `git push origin v2.0.0` → CI builds + creates GitHub Release with installer
4. Verify GitHub Release page: installer attached, release notes correct
5. Update dikta.me download links
6. Make repo public (if not already)
7. Execute Launch Week Battle Plan (see RELEASE_ROADMAP.md Section 11)

---

## Reference: Key File Paths

### Wizard System
| File | Purpose |
|------|---------|
| `src/DiktaMe.App/ViewModels/WizardViewModel.cs` | Wizard state, navigation, settings persistence |
| `src/DiktaMe.App/Views/WizardWindow.xaml.cs` | Step array (lines 14-23), navigation routing |
| `src/DiktaMe.App/Views/Wizard/WizardSttPage.xaml.cs` | Download pattern template (lines 92-158) |
| `src/DiktaMe.App/Views/Wizard/WizardLlmPage.xaml.cs` | Ollama install/pull pattern (lines 256-362) |
| `src/DiktaMe.App/Views/Wizard/WizardGetStartedPage.xaml` | Three-path radio UI (lines 10-37) |
| `src/DiktaMe.App/Views/Wizard/IWizardStepPage.cs` | Interface: `SetViewModel(WizardViewModel)` |

### Security / License
| File | Purpose |
|------|---------|
| `src/DiktaMe.Core/Security/SecureStorage.cs` | DPAPI key storage. ValidProviders (line 43-51). StoreKey/RetrieveKey. |
| `src/DiktaMe.Core/Config/AuthMode.cs` | Enum: None=0, Wallet=1, ApiKey=2, Account=3 |
| `src/DiktaMe.Core/Config/AppSettings.cs` | Account settings (line 575-586), TTS settings (lines 428-474) |
| `src/DiktaMe.Core/Account/AccountService.cs` | Auth callback pattern (lines 59-93), token storage |
| `src/DiktaMe.Core/Account/TokenRefreshService.cs` | Token lifecycle, refresh timer pattern |
| `src/DiktaMe.Core/Config/PipelineFactory.cs` | `GetProviders()` (line 239) — THE license intercept point |

### Installer / CI
| File | Purpose |
|------|---------|
| `publish-release.cmd` | Publish pipeline: `dotnet publish -c Release -r win-x64 --self-contained` |
| `Directory.Build.props` | Version: 2.0.0 (lines 19-21) |
| `src/DiktaMe.App/DiktaMe.App.csproj` | OutputType=WinExe, WindowsPackageType=None, MinVersion=17763 |
| `src/DiktaMe.App/Assets/tray-icon.ico` | Installer icon (2.5KB) |
| `.github/workflows/ci-v2.yml` | CI pipeline, publish step (line 134-142), artifact upload |
| `ci/test-threshold.json` | Min 470 tests, publish size 130-250MB |

### Website
| File | Purpose |
|------|---------|
| `website/app/components/PricingSection.tsx` | Pricing tier cards |
| `website/messages/en.json` | English copy (pricing at lines 453-500) |
| `website/messages/es.json` | Spanish copy |
| `website/supabase/functions/wallet-webhook/adapters/lemonsqueezy.ts` | Payment webhook (extend for license keys) |

---

## Reference: Patterns to Follow

### Adding a Wizard Step
1. Create `WizardXxxPage.xaml` + `.xaml.cs` implementing `IWizardStepPage`
2. Insert `typeof(WizardXxxPage)` in `WizardWindow._stepPages[]` (line 14-23)
3. Increment `WizardViewModel.TotalSteps` (line 40)
4. Update step index references in `GoNextAsync()` for skip logic
5. Add localization strings in both `en/Resources.resw` and `es-MX/Resources.resw`

### Download Pattern (BeforeLeaveStep)
```
SetViewModel() → register BeforeLeaveStep callback
OnBeforeLeaveStepAsync():
  if no download needed → return true
  if already downloaded → return true
  CanGoNext = false
  Show progress UI
  Await download with CancellationToken
  On success: CanGoNext = true, return false (let user see "Ready")
  On error: CanGoNext = true, return false (let user retry)
  User clicks Next again → check passes → return true
```

### SecureStorage Key Pattern
```csharp
// Store
_secureStorage.StoreKey("provider_name", value);
// Retrieve
string? value = _secureStorage.RetrieveKey("provider_name");
// Delete
_secureStorage.DeleteKey("provider_name");
// Must be in ValidProviders array (SecureStorage.cs line 43-51)
```

### Settings Update Pattern
```csharp
var updated = _settings.Current with
{
    PropertyToChange = newValue,
    Nested = _settings.Current.Nested with { SubProperty = newValue },
};
await _settings.UpdateAsync(updated);
```
