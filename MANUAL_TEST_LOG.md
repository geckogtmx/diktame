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

| ID | Severity | Step | Description | Status |
|----|----------|------|-------------|--------|
| BUG-013 | Low | 1.1.19 | "You're All Set!" / Ready page summary box lists Whisper + Ollama but omits Kokoro when TTS=Local is selected | Open |
| BUG-014 | High | 1.2.8 (raw mode) | Toggling LLM=OFF pill on CP did NOT skip LLM in pipeline. Root cause: `OnLlmModeChanged` in ControlPanelViewModel.cs:941-954 skipped writing `UseLlm=false` when rawMode, leaving pipeline to try Gemini. Fix: always walk profile slots, write `UseLlm=false` on OFF, both `LlmProvider`+`UseLlm=true` on LOCAL/CLOUD. Verified 17:02:12 — raw Whisper-only dictation, no LLM call, no errors. | **FIXED** |
| BUG-015 | High | 1.2.8 (raw mode) | Pipeline exception from BUG-014 is logged as `[ERR] Dictate pipeline failed` but produces NO user-facing notification/toast. Silent failure — user sees no feedback, thinks dictation did nothing. Error handling in `LoadingViewModel.RunBatchDictationAsync` (line 1466) swallows without surfacing. NOTE: Code review found `ShowToast` IS called at LoadingViewModel.cs:1495 — needs re-verification after BUG-014 fix whether toast actually renders. | Open (needs re-verify) |
| BUG-016 | Medium | Settings > TTS | Wizard downloads Kokoro `int8` variant (`kokoro-quant-convinteger.onnx`, 88MB) but Settings > Text to Speech defaults the variant pulldown to a different value (fp16/fp32/gpu), causing "Kokoro model not downloaded" warning even though the file exists on disk. Fix: either default Settings pulldown to match wizard-downloaded variant, or have wizard write the chosen variant to settings so both agree. User workaround: change pulldown to `int8` → works. | Open |

---

## Observations / UX Findings

| ID | Step | Description |
|----|------|-------------|
| OBS-001 | 3.1.6 | Wizard LLM step blocks progression when Ollama absent — no Skip option. Heavy mid-wizard install. Candidate redesign: finish wizard with Whisper only, auto-open Settings post-wizard for Ollama install. |
| OBS-002 | 1.1.14 | Whisper model download UX: no visible numeric progress (% or MB/total) or live progress bar. Feels static during multi-minute download. Needs realtime % + "XX MB / YY MB" indicator. |
| OBS-003 | 1.1.15 | Ollama winget install spawns a visible Ollama setup window/UI mid-wizard. Before clicking Install, the wizard must show an upfront notice: "Ollama will open its own window during install. Please minimize or close it when it appears — the wizard handles everything automatically." Without this warning, users interact with Ollama's window, cancel it, or get confused whether progress is in the wizard or the Ollama installer. |
| OBS-004 | 1.2.x (first dictation) | Local pipeline latency: REC 6.59s, TRNS 1.03s, PROC 0.33s, INJ 0.09s, TOT 1.45s. Clean local dictation working end-to-end on fresh install. |
| OBS-005 | 1.1.15 | Ollama model pull in wizard is very long with no realtime feedback. User cannot tell if the process is moving or stuck. Required UX: live percentage AND "XX MB / YY MB" counter, updating at least every 1-2 seconds. Parse `ollama pull` stdout (emits progress lines) and surface to wizard LLM page. Without this the wizard feels frozen for several minutes. |

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
