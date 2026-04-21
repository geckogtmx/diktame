# dIKta.me V2 — Manual Test Log

**Session:** 2026-04-20
**Tester:** Eduardo
**Build:** Local dev build (x64 Release)
**Focus:** Journey 3 (Local path: Whisper + Ollama) — fresh run
**Source of truth:** `MANUAL_TEST_PLAN.md`. Prior log archived at `MANUAL_TEST_LOG_2026-04-14.md` (not trusted).

---

## Preconditions Checklist

- [ ] Ollama uninstalled — verify `%LOCALAPPDATA%\Programs\Ollama\ollama.exe` absent
- [ ] `%APPDATA%\DiktaMe\models\` deleted (Whisper `.bin` files)
- [ ] `%APPDATA%\DiktaMe\models\tts\` deleted (Kokoro ONNX)
- [ ] `%APPDATA%\DiktaMe\settings.json` deleted
- [ ] Power License active (valid key, slots available)
- [ ] App rebuilt fresh: `dotnet build src/DiktaMe.App/DiktaMe.App.csproj -c Release "-p:Platform=x64"`

Cleanup commands:
```powershell
Remove-Item "$env:APPDATA\DiktaMe\settings.json" -Force -ErrorAction SilentlyContinue
Remove-Item "$env:APPDATA\DiktaMe\models" -Recurse -Force -ErrorAction SilentlyContinue
```

---

## Session Progress

Check against MANUAL_TEST_PLAN.md §3 (Journey 3). One-line entry per step as it passes.

| Step | Result | Notes |
|------|--------|-------|
|      |        |       |

---

## Bugs Found

| ID      | Severity | Step                      | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | Status                 |
| ------- | -------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------- |
| BUG-013 | Low      | 1.1.19                    | "You're All Set!" / Ready page summary box lists Whisper + Ollama but omits Kokoro when TTS=Local is selected. Root cause: WizardReadyPage.xaml had no TtsSummary TextBlock; code-behind never read TtsChoice. Fix: added TtsSummary element, TtsChoice/CloudTtsProvider resolution, and 7 EN/ES localization keys. Verified via fresh wizard run — Ready page shows 3 lines.                                                                                                                                                                                                                                                                                                                                      | **FIXED**              |
| BUG-014 | High     | 1.2.8 (raw mode)          | Toggling LLM=OFF pill on CP did NOT skip LLM in pipeline. Root cause: `OnLlmModeChanged` in ControlPanelViewModel.cs:941-954 skipped writing `UseLlm=false` when rawMode, leaving pipeline to try Gemini. Fix: always walk profile slots, write `UseLlm=false` on OFF, both `LlmProvider`+`UseLlm=true` on LOCAL/CLOUD. Verified 17:02:12 — raw Whisper-only dictation, no LLM call, no errors.                                                                                                                                                                                                                                                                                                                    | **FIXED**              |
| BUG-015 | High     | 1.2.8 (raw mode)          | Pipeline exception from BUG-014 is logged as `[ERR] Dictate pipeline failed` but produces NO user-facing notification/toast. Silent failure — user sees no feedback, thinks dictation did nothing. Error handling in `LoadingViewModel.RunBatchDictationAsync` (line 1466) swallows without surfacing. NOTE: Code review found `ShowToast` IS called at LoadingViewModel.cs:1495 — needs re-verification after BUG-014 fix whether toast actually renders.                                                                                                                                                                                                                                                         | Open (needs re-verify) |
| BUG-016 | Medium   | Settings > TTS            | Wizard downloads Kokoro `int8` variant (`kokoro-quant-convinteger.onnx`, 88MB) but Settings > Text to Speech defaulted to `gpu` (AppSettings.cs:493 default), causing "Kokoro model not downloaded" warning. Root cause: wizard never persisted the variant it downloaded. Fix: WizardViewModel.cs now writes `Tts.KokoroModelVariant = "int8"` when ttsProvider is kokoro, so Settings and wizard agree. Verified via fresh wizard run — Settings TTS shows int8 + "Model ready".                                                                                                                                                                                                                                 | **FIXED**              |
| BUG-017 | Medium   | Settings (missing UI)     | `AppSettings.General.AdditionalKey` is functional in code (read at LoadingViewModel.cs:1458 → appends Enter/Tab after each injection) but has NO UI anywhere to set it. Zero references in any `src/DiktaMe.App/Views/**/*.xaml`. Only way for a user to change it is hand-editing `%APPDATA%\DiktaMe\settings.json`. Old §1.3.3–1.3.5 referenced a General > Additional Key dropdown that was never built (or was removed). Fix options: (a) add a None/Enter/Tab ComboBox to Settings > General > Application or Dictation Presets, or (b) remove the setting from code if no longer desired.                                                                                                                    | Open                   |
| BUG-018 | High     | 1.3.3 Auto-Start          | Settings > General > Application > Auto-Start toggle persists `AppSettings.General.AutoStart=true` to JSON but performs NO OS-level registration. Verified: Task Scheduler has no `dIKta.me` task (Verify-AutoStart.ps1 fails), HKCU\...\Run has no Dikta entry, Startup folder empty. Root cause: `OnAutoStartChanged` at GeneralSettingsViewModel.cs:283 only calls `Save()`. No `AutoStartManager` or `ScheduledTaskService` exists in `src/DiktaMe.Core` or `src/DiktaMe.App`. Feature is a no-op. Fix: implement registration via Task Scheduler (preferred, matches existing Verify-AutoStart.ps1 contract) or fallback to HKCU Run key; call it from OnAutoStartChanged on both true and false transitions. | Open                   |
| BUG-019 | Low      | 1.3.39 Keyboard Shortcuts | Vision hotkey (default `Ctrl+Alt+S`, `HotkeySettings.Vision` at AppSettings.cs:313) IS fully wired and registered at runtime (LoadingViewModel.cs:433), so the feature works. However, the Keyboard Shortcuts sub-item in Settings > General lists only 8 rows (Dictate, Refine, Ask, Translate, Oops, Note, Chat, ReadSelection) — Vision is absent. Users cannot view or rebind the Vision hotkey from the UI. Classification: missing UI surface, not a functional defect. Fix: add a 9th hotkey row to GeneralSettingsPage.xaml Keyboard Shortcuts section, bound to `ViewModel.Hotkeys.Vision` with a `ResetVisionCommand`. Same pattern as the other 8 rows.                                                 | Open                   |
| BUG-020 | Medium   | 1.3.36 Keyboard Shortcuts | When recording a new hotkey, pressing a combo already bound to another pipeline FIRES THAT PIPELINE instead of being captured by the record field. Repro: Settings > Keyboard Shortcuts > click Record on Ask → press Ctrl+Alt+Q (currently bound to ReadSelection) → ReadSelection TTS pipeline triggers instead of the field capturing the combo or warning about a duplicate. Two defects stacked: (a) global hotkeys not suppressed during Record capture (HotkeyManager should temporarily unregister all hotkeys while recording, or Record handler should intercept before Win32 dispatch), and (b) no duplicate-combo warning even if assignment persists. Fix: on Record start, unregister all global hotkeys; on Accept, check proposed combo against every `HotkeySettings.*` property and warn if taken; on Cancel/Commit, re-register. | Open                   |
| BUG-021 | Low      | 1.4.28 Quick Chat         | Quick Chat window has no dedicated mic button — test plan asks for one. Not a functional gap: the global Dictate hotkey (Ctrl+Alt+D) already works while Quick Chat is focused and injects into its text field like any other input. Classification: cosmetic/convenience affordance, not a defect. Fix if we want the in-window affordance: add a mic button that just triggers the same Dictate pipeline with Quick Chat as the focus target. Otherwise, update test plan step 1.4.28 to reflect "Use global Dictate hotkey while Quick Chat is focused" instead. | Open (cosmetic)        |
| BUG-022 | Critical | 1.4.31 Quick Chat (Esc) → hotkey/tray reopen | Pressing Esc in Quick Chat uses `this.Close()` (QuickChatWindow.xaml.cs:116) which tears down the WinUI Window instead of hiding it. `AppWindow.Closing` handler at App.xaml.cs:509 fires with `e.Cancel=true` but loses the race — `_quickChatWindow` field is left pointing at a zombie window whose `AppWindow` is null. Next Ctrl+Alt+C hits `ToggleQuickChat` line 499 (`_quickChatWindow is not null` = true) and dereferences `.AppWindow.Hide()` at line 502 → NullReferenceException, swallowed. Tray menu "Open Quick Chat" then calls `Activate()` on the zombie → hard native crash (log file truncates mid-write). Repro: open Quick Chat, press Esc, press Ctrl+Alt+C (fails silently 3–4×), open from tray → app crashes. Fix: change QuickChatWindow.xaml.cs:116 from `this.Close()` to `this.AppWindow.Hide()` to match the hide-not-destroy pattern the rest of the codebase uses. | Open                   |
| BUG-023 | High     | 1.6.3–1.6.5 API Keys     | Settings > AI Engine > API Keys has NO "Test Connection" button for any provider. Each provider row has only Save + Delete commands (AIEngineSettingsPage.xaml confirms). "Test Connection" exists only in the wizard STT/LLM pages. Worse, the save-side validation at ApiKeyValidator.cs is format-only with loose rules: Deepgram accepts ≥30 chars of ANY characters (30 digits pass!), Gemini accepts ≥30 chars with no prefix check, OpenAI requires `sk-` + ≥48 chars (trivially bypassed), Anthropic requires `sk-ant-` + ≥20. Repro (user): entered gibberish numbers as Deepgram key → saved without error. Fix: (a) add async TestConnectionCommand per provider that makes a minimal authenticated API call (e.g. Deepgram `/v1/projects`, OpenAI `/v1/models`, Gemini `/v1/models`, Anthropic `/v1/messages` ping) and surfaces success/failure in the row status text; (b) surface the same command in the Settings UI next to Save, matching the wizard pattern; optionally (c) tighten format validation to reduce silent garbage acceptance. | Open                   |
| BUG-024 | Medium   | 1.7.4 Privacy UX          | Privacy panel has two independent controls that can enter silently-inconsistent states: Logging Intensity slider (Ghost/Stats/Balanced/Full) + PII Scrubber toggle. Root: HistoryManager.cs:117-122 — when Level=Full, stored text/raw_transcript are set verbatim regardless of `PiiScrubEnabled`. The PII Scrubber toggle can show "On" at Full and look effective to the user, but it is silently ignored. Repro: Full + Scrubber ON → dictate "My email is elen@gmail.com" → stored verbatim (ids 161–162). Fix: remove the Full-level override in HistoryManager.cs:117-122. The two controls are orthogonal by design — Logging Intensity controls what rows/metadata get logged; PII Scrubber controls whether stored text is redacted. "Full + Scrubber ON" is a legitimate, valuable combination ("full telemetry, redacted text") and should work. Corrected storage rule: at Balanced AND Full, store text/raw_transcript; if PiiScrubEnabled apply PIIScrubber.Scrub in both cases. At Stats/Ghost, text is suppressed entirely regardless of toggle. No UI lock required — both controls remain independently meaningful. Copy tweak to go with the fix: replace the current PII Scrubber description with something explicit about the relationship — e.g. "Redacts emails, phone numbers, and other personal data from stored history text. Works at Balanced and Full logging levels; at Stats or Ghost, text isn't stored at all." Localization keys: `Settings_Privacy_PiiScrubber_Description` (EN + ES). | Open                   |

---

## Observations / UX Findings

| ID      | Step                    | Description                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ------- | ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| OBS-001 | 3.1.6                   | Wizard LLM step blocks progression when Ollama absent — no Skip option. Heavy mid-wizard install. Candidate redesign: finish wizard with Whisper only, auto-open Settings post-wizard for Ollama install.                                                                                                                                                                                                                                |
| OBS-002 | 1.1.14                  | Whisper model download UX: no visible numeric progress (% or MB/total) or live progress bar. Feels static during multi-minute download. Needs realtime % + "XX MB / YY MB" indicator.                                                                                                                                                                                                                                                    |
| OBS-003 | 1.1.15                  | Ollama winget install spawns a visible Ollama setup window/UI mid-wizard. Before clicking Install, the wizard must show an upfront notice: "Ollama will open its own window during install. Please minimize or close it when it appears — the wizard handles everything automatically." Without this warning, users interact with Ollama's window, cancel it, or get confused whether progress is in the wizard or the Ollama installer. |
| OBS-004 | 1.2.x (first dictation) | Local pipeline latency: REC 6.59s, TRNS 1.03s, PROC 0.33s, INJ 0.09s, TOT 1.45s. Clean local dictation working end-to-end on fresh install.                                                                                                                                                                                                                                                                                              |
| OBS-005 | 1.1.15                  | Ollama model pull in wizard is very long with no realtime feedback. User cannot tell if the process is moving or stuck. Required UX: live percentage AND "XX MB / YY MB" counter, updating at least every 1-2 seconds. Parse `ollama pull` stdout (emits progress lines) and surface to wizard LLM page. Without this the wizard feels frozen for several minutes.                                                                       |
| OBS-006 | Settings > General / CP | Auto-Collapse toggle for the Control Panel defaults to OFF. Should default to ON — auto-collapse is the intended steady-state behavior (CT.2 in test plan), and the compact CP is the design baseline. New users who leave the default sit with the full-size panel always expanded. Change default in AppSettings.                                                                                                                      |
| OBS-007 | Settings > General / CP | Always-On-Top toggle for the Control Panel defaults to OFF. Should default to ON — the CP is a HUD and needs to remain visible while the user works in other apps (Notepad, browser, etc). Change default in AppSettings alongside OBS-006.                                                                                                                                                                                              |

---

## Timing Captures (Wizard Local path, cold machine)

| Phase | Duration | Notes |
|-------|----------|-------|
| Whisper model download |  |  |
| Ollama winget install |  |  |
| Ollama service start |  |  |
| Model pull (gemma3:4b) |  |  |
| Kokoro model download |  |  |
| **Total wizard wall-clock** |  |  |

### STT-Only Dictation Benchmark (post BUG-014 fix, 2026-04-20 17:02–17:05)

**Setup:** Whisper (small, Vulkan GPU), LLM=OFF, 6 consecutive dictations, cache warm.

| # | Audio | Whisper ms | Inject ms | Rec-end → Injected | Chars |
|---|-------|-----------|-----------|---------------------|-------|
| 1 | 5.7s  | 254 | 102 | 981ms | 48 |
| 2 | 4.8s  | 585 |  84 | 670ms | 42 |
| 3 | 6.2s  | 423 |  85 | 509ms | 68 |
| 4 | 10.7s | 567 |  85 | 653ms | 107 |
| 5 | 20.3s | 679 |  85 | 765ms | 192 |
| 6 | 17.3s | 658 |  86 | 745ms | 203 |

**Averages:** Whisper=528 ms · Inject=88 ms · Rec-end→Injected=**720 ms** · STT/audio ratio=~6%.
**Observation:** Latency is near-flat across 4.8s–20.3s audio (ratio 3–6%), so STT scales sub-linearly. Inject latency is effectively constant (~85ms). Compared to full Whisper+Ollama pipeline (earlier sample 17:02 was dictate+LLM ~335ms LLM processing on top of STT), STT-only saves ~300–500ms per dictation. Useful mode for speed-sensitive, verbatim capture.

---

## Deferred / Follow-ups

- Wizard UX redesign (defer Ollama install to Settings, auto-open Settings first time after wizard completes) — spec-worthy, not this session.
- Re-verify BUG-009 through BUG-012 from archived log against current code — separate pass.
- BUG-008 (license slot burn on same PC after data wipe) — reproducible? Track if it recurs.

---

## Session Summary (fill at close)

- Steps passed: __ / __
- Bugs found: __ critical, __ high, __ medium, __ low
- Time spent: __ hours
- Journey 3 complete: [ ] yes / [ ] no
