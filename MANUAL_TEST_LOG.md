# dIKta.me V2 — Manual Test Log

**Session:** 2026-04-14
**Tester:** Eduardo
**Build:** Local dev build (x64 Release)
**Recording:** Video recording in progress

---

## Session Progress

### Steps Completed
- [x] 1.1.1 — App launches, wizard appears. **Finding: No desktop icon/logo (BUG-001, BUG-002)**
- [x] 1.1.2 — Language selection works (tested EN + ES)
- [x] 1.1.3 — Get Started page: Wallet default, BYOK/Local disabled for unlicensed, info text shown
- [x] 1.1.3a — "I Have a Key!" button works, activation page, key activates, returns to Get Started with BYOK/Local enabled
- [x] 1.1.3b — Wallet path: Features page shows Power License benefits (Local AI, BYOK, Vision), "Get yours now" link, "No rush" footer
- [x] 1.1.3c — Wallet path completes: browser opens for sign-in, app loads with Wallet mode active

### Steps Blocked / Deferred
- [ ] 1.1.4+ (BYOK path) — Not yet tested end-to-end with cloud API keys
- [ ] Local path — Needs retest after wizard lane-filtering fix (STT/LLM/TTS now show only relevant options)
- [ ] License re-activation test — Key burned 3 slots on same PC (BUG-008), need new key

---

## Bugs Found

| ID | Severity | Step | Description | Status |
|----|----------|------|-------------|--------|
| BUG-001 | Minor | 1.1.1 | No desktop icon/logo on app window/taskbar | Open |
| BUG-002 | Minor | Pre-test | Compiled .exe has no embedded icon | Open |
| BUG-003 | Critical | 1.1.3 | Wizard blocked BYOK/Local with no way to continue | **FIXED** |
| BUG-004 | High | 1.1.3c | Wallet path didn't set AuthMode=Wallet after sign-in | **FIXED** |
| BUG-005 | Critical | 1.2 | WebSocket streaming died after first dictation (singleton reuse) | **FIXED** |
| BUG-006 | Medium | 1.2 | Settings file contention (IOException on settings.json.tmp) | Open (pre-existing) |
| BUG-007 | Low | Startup | Double hotkey registration in logs | Open (pre-existing) |
| BUG-008 | Medium | 6.5 | License re-activation burns slots on same PC after app data wipe | Open |

## Observations

| ID | Step | Description |
|----|------|-------------|
| OBS-001 | Local path | Model download failed (network/CDN — not a code bug) |

---

## Code Changes Made This Session

### 1. Wizard License Gate UX (BUG-003 fix)
- `WizardGetStartedPage.xaml` — BYOK/Local radio buttons disabled for unlicensed, info text panel
- `WizardGetStartedPage.xaml.cs` — `UpdateOptionAvailability()` enables/disables based on license state
- `WizardViewModel.cs` — Removed blocking `return;` in `GoNextAsync()`, removed `_licenseManager` field
- `PipelineFactory.cs` — Runtime license gate: non-wallet + unlicensed = blocked (covers both BYOK and Local)

### 2. Wallet AuthMode Default (BUG-004 fix)
- `WizardViewModel.cs` — `StartWalletAsync()` now sets `AuthMode = AuthMode.Wallet` before opening browser

### 3. WebSocket Singleton Reuse (BUG-005 fix)
- `WalletStreamingSTTProxy.cs` — `ConnectAsync()` resets all state (WebSocket, flags, buffers, TaskCompletionSource) for singleton reuse across multiple dictations

### 4. Features Showcase Page (new)
- `WizardFeaturesPage.xaml` + `.xaml.cs` — Shows Power License benefits (Local AI, BYOK, Vision) for Wallet path users
- Inserted as step 2 (Wallet-only), skipped for BYOK/Local paths

### 5. License Activation Page (new)
- `WizardActivatePage.xaml` + `.xaml.cs` — "I Have a Key!" detour page with key input, activate button, buy link
- Accessible via red "I Have a Key!" button on Get Started step
- On success: returns to Get Started with all options enabled

### 6. Wizard Lane Filtering
- Removed `StartLocalAsync()` shortcut — Local path now goes through same STT/LLM/TTS steps as BYOK
- `WizardSttPage.xaml.cs` — Hides cloud option for Local lane, hides local option for BYOK lane
- `WizardLlmPage.xaml.cs` — Same lane filtering
- `WizardTtsPage.xaml.cs` — Same lane filtering
- `WizardViewModel.cs` — Pre-selects defaults based on onboarding choice (local→Whisper/Ollama/Kokoro, BYOK→Cloud/Cloud/Off)

### 7. Wizard Step Restructure
- `WizardWindow.xaml.cs` — Step array now 10 entries (0-8 sequential + 9 activate detour)
- `WizardWindow.xaml` — "I Have a Key!" red button, progress bar updated
- `WizardViewModel.cs` — TotalSteps=9, skip logic updated for new step numbers

### 8. Localization (EN + ES)
- New strings: `Wizard_Features_*` (Local AI, BYOK, Vision descriptions), `Wizard_Activate_*`, `Wizard_HaveKey`, `Wizard_LicenseInfo`
- Removed old strings: `Wizard_LicenseRequired`, `Wizard_LicenseOrBuild`

### 9. Test Plan Updates
- `MANUAL_TEST_PLAN.md` — Updated steps 1.1.3, 2.1.3, 3.1.4, 4.1.3, 6.5.6, 6.5.7 to reflect new wizard flow

---

## Decisions Made This Session

1. **Wizard never blocks** — License enforcement at runtime, not wizard setup
2. **BYOK + Local both require Power License** — Only Wallet is free
3. **Features page for Wallet users** — Shows what Power License unlocks (Local AI, BYOK, Vision), not free pipelines
4. **"I Have a Key!" button** — Red button on Get Started step, takes to activation page, returns after success
5. **Lane filtering in wizard** — BYOK path shows only cloud options, Local path shows only local options
6. **No more Local shortcut** — Both paths go through the same wizard steps with different defaults
7. **Informational note planned** — "You can change these settings after the wizard" (not yet implemented)
8. **Dedicated license landing page needed** — dikta.me/pricing needs a focused conversion page

---

## Additional Bugs Found (End of Session)

| ID | Severity | Step | Description | Status |
|----|----------|------|-------------|--------|
| BUG-009 | High | Wizard close | WizardCompleted set too early in Wallet path — closing wizard mid-flow skips it on next launch | Open |
| BUG-010 | High | TTS page | TTS has 4 cloud providers (Deepgram, OpenAI, Gemini, Inworld) each with own keys — page wrongly says "shares LLM key" | Open |
| BUG-011 | High | LLM page | Only shows "Cloud AI" — needs dropdown for Anthropic/Gemini/OpenAI/OpenRouter/Requesty + per-provider key | Open |
| BUG-012 | Medium | Ready page | Last wizard page crops text at bottom — reduce padding | Open |

## Next Steps (When Resuming)

1. **Fix BYOK provider selection** (BUG-010, BUG-011) — LLM and TTS pages need provider dropdowns + per-provider API key fields (STT page is fine as-is)
2. **Fix LLM page copy** — text is vague, needs clearer description
3. **Fix Ready page layout** (BUG-012) — reduce top padding
4. **Fix WizardCompleted timing** (BUG-009) — don't set until wizard actually completes
5. Get new license key (old one burned 3 slots — BUG-008)
6. Add informational note to Get Started page
7. Test Local wizard path end-to-end
8. Continue Journey 1 from step 1.2 (Core Dictation)
9. Update MANUAL_TEST_PLAN.md with remaining flow changes

## Future Consideration (After Test Plan Complete)
- **STT provider dropdown for BYOK**: Currently hardcoded to Deepgram. Gemini Audio is used for Wallet. May want to let BYOK users choose between Deepgram and Gemini Audio for STT. Needs testing first to confirm both work with user-provided keys.
