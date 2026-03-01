# Control Panel V2 Rework — Implementation Plan

> **Status**: Phase 1 complete (XAML + ViewModel rewrite). Builds clean, 509 tests pass.
> **Session resumable**: Yes — all remaining work items listed in "Remaining Work" section.

## Context

The V2 Control Panel (WinUI 3) needed a visual and structural overhaul to match the V1 (Electron) look and feel. The old V2 implementation had the right data/ViewModel backbone but the XAML layout was crude — oversized fonts (32pt status!), wrong proportions, no state-driven visuals, missing features (no RAW toggle, no auth badge, no footer), and broken bindings (LOCAL toggle unwired, +KEY destroying key value).

**V1 reference screenshot**: `C:\Users\gecko\Videos\Captures\Control Panel 2_27_2026 2_39_55 PM.png`

---

## V1 vs V2 Layout Comparison

```
V1 (400x265px, frameless Electron)         V2 Target (520x380px, WinUI 3 with title bar)
-------------------------------------      -------------------------------------
[*] READY     [SMALL][GEMMA3:1B][LOC] [x]  [*] READY     [SMALL][GEMMA3:1B][LOC] [gear]
-------------------------------------      -------------------------------------
[Standard][Prompt][Professional][RAW]       [Preset1][Preset2][...up to 8 in 2x4 grid]
-------------------------------------      -------------------------------------
 (o)SOUND  (o)LOCAL  (o)+KEY  (o)REFINE     (o)SOUND (o)LOCAL (o)+KEY (o)RAW (o)REFINE
-------------------------------------      -------------------------------------
   0    |    0    |   --   |    0            0    |    0    |   --   |    0
  SESS  |  WORDS  |  WPM   |   TOK         SESS  |  WORDS  |  WPM   |   TOK
-------------------------------------      -------------------------------------
  --  |  --  |   --   |   --   |   --       --  |  --  |   --   |   --   |   --
  TOT |  REC |  TRNS  |  PROC  |  INJ      TOT |  REC |  TRNS  |  PROC  |  INJ
-------------------------------------      -------------------------------------
 Hotkeys: [Ctrl+Alt+D] [R] [A] [N] [T]    Hotkeys: [Ctrl+Alt+D] [R] [A] [N] [T]
```

**Key differences from V1**:
- V2 has a WinUI title bar (can't be frameless without major complexity)
- Dictation presets are dynamic (1-8), arranged in a responsive 2x4 grid (max 2 rows x 4 columns)
- RAW is a toggle switch (5th toggle in the actions row), not a preset button
- Window size 520x380 (vs V1's 400x265 frameless)

---

## Completed Changes (Phase 1)

### Files Modified

| # | File | Status | Changes |
|---|---|---|---|
| 1 | `src/DiktaMe.App/Views/ControlPanelPage.xaml` | DONE | Complete XAML rewrite — 6 rows, V1 color palette, grid-based metric cells, footer |
| 2 | `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | DONE | Auth badge, IsCloudMode→IsLocalMode rename+wiring, +KEY fix, RAW toggle, RefineVoice toggle, formatted perf strings (FormatMs), label properties, hotkey properties, DictationModeItem with Subtitle/BackgroundHex/ForegroundHex |
| 3 | `src/DiktaMe.App/MainWindow.xaml.cs` | DONE | Window resize 520x380 |
| 4 | `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | DONE | Added `OnPipelineStateChanged(this, PipelineState.Transcribing)` to all 5 pipeline methods |

### What Changed in Detail

#### ControlPanelViewModel.cs
- **`DictationModeItem`** record: Added `Subtitle`, `BackgroundHex`, `ForegroundHex` fields (pre-computed colors for active/inactive state since x:Bind in DataTemplate can't do complex converters)
- **`IsCloudMode` → `IsLocalMode`**: Renamed + inverted semantics. Added `OnIsLocalModeChanged` that persists `ActiveProfileName` ("Local"/"Cloud")
- **`AuthBadgeText`**: New property, "LOC" when local, "API" when cloud
- **`IsRefineVoice`**: New bool toggle (replaces the old pill Button approach). Synced to `RefineMode` enum
- **`OnIsAdditionalKeyEnabledChanged`**: Fixed to preserve existing key value instead of hardcoding "Enter"
- **`OnIsRawModeEnabledChanged`**: New handler (was orphaned)
- **Toggle labels**: `SoundLabel`, `LocalLabel`, `KeyLabel`, `RawLabel`, `RefineLabel` — dynamic computed strings
- **Formatted perf**: `LastTotalFormatted` etc. via `FormatMs(long ms)` — all in seconds, F2 under 10s, F1 at 10s+, "--" default
- **`WordsPerMinuteFormatted`**: Shows "--" when 0, integer value otherwise
- **Hotkey display**: 5 string properties synced from `HotkeySettings`
- **Badge defaults**: Changed from "Cloud STT"/"Gemini" to "--"/"--"

#### ControlPanelPage.xaml
- **Color palette**: V1 CSS values (`#002029` ink-black, `#00303d` jet-black, `#004052` dark-teal, etc.)
- **Layout**: 6-row Grid with Padding="0", 1px border separators
- **Row 0 (Header)**: 10x10 status dot (color-bound via StateToColor), 14pt status text, 3 badges (STT/LLM/Auth), gear button
- **Row 1 (Presets)**: ItemsRepeater with UniformGridLayout 4-col, preset buttons with title+subtitle, active state via pre-computed colors
- **Row 2 (Toggles)**: 5-column Grid — SOUND, LOCAL, +KEY, RAW, REFINE — all ToggleSwitch with dynamic labels
- **Row 3 (Session)**: 4-column metric grid (1px dividers via background color trick) — SESS, WORDS, WPM, TOK
- **Row 4 (Perf)**: 5-column metric grid — TOT, REC, TRNS, PROC, INJ — Consolas font, green (#7aff9e)
- **Row 5 (Footer)**: Hotkey tags with border styling

#### LoadingViewModel.cs
- Added `_controlPanel.OnPipelineStateChanged(this, PipelineState.Transcribing)` after audio recording completes in all 5 pipeline methods (Dictate, Refine, Ask, Translate, Note)

---

## Remaining Work (Future Sessions)

### Visual Polish (after first visual test)
- [ ] Tune window height if content overflows or has too much space at 380px
- [ ] Verify ToggleSwitch appearance fits in 5-column grid (may need custom template or smaller control)
- [ ] Test with 1, 4, and 8 presets to verify UniformGridLayout wrapping
- [ ] Consider adding V1's left-border state indicator (4px colored border on the entire panel)
- [ ] Consider V1's recording pulse animation (background alpha fade)

### Functional Wiring (not yet connected)
- [ ] **RAW toggle → pipeline**: `IsRawModeEnabled` is set but LoadingViewModel doesn't read it yet. Need to override `DictationProfile.UseLlm` when RAW is on.
- [ ] **REFINE toggle → pipeline**: `RefineMode` toggles between Auto/Voice but LoadingViewModel always runs `refine_instruction`. Need to route to `refine_auto` when `RefineMode == Auto`.
- [ ] **Pipeline state granularity**: Currently only fires `Transcribing` after recording stops. Could add `Processing` and `Injecting` states if pipelines expose stage events.

### Settings Integration
- [ ] **ControlPanelConfigPage.xaml**: Description text still says "Standard, Prompt, Professional, RAW mode buttons" — update to reflect dynamic presets

---

## Telemetry Fields Reference

### Session Stats (Row 3, 4 columns)
| Label | Property | Source | Format |
|---|---|---|---|
| SESS | `SessionCount` | `MetricsCollector.GetSessionStats().Sessions` | Integer |
| WORDS | `WordCount` | `MetricsCollector.GetSessionStats().Words` | Integer |
| WPM | `WordsPerMinuteFormatted` | `Words / (AverageLatencyMs / 60000)` | Integer or "--" |
| TOK | `TokenCount` | `Words * 1.3` (rough LLM token estimate) | Integer |

### Performance Stats (Row 4, 5 columns)
| Label | Property | Source | Format |
|---|---|---|---|
| TOT | `LastTotalFormatted` | `PipelineResult.TotalMs` | `0.50s` / `12.5s` / "--" |
| REC | `LastRecordingFormatted` | `PipelineResult.RecordingMs` | Same format |
| TRNS | `LastTranscriptionFormatted` | `PipelineResult.TranscriptionMs` | Same format |
| PROC | `LastProcessingFormatted` | `PipelineResult.ProcessingMs` | Same format |
| INJ | `LastInjectionFormatted` | `PipelineResult.InjectionMs` | Same format |

All perf values in seconds. Under 10s = F2 (e.g. `0.50s`), 10s+ = F1 (e.g. `12.5s`). Default = "--".

---

## V1 Color Palette (implemented)

| Brush Key | Hex | V1 CSS Variable |
|---|---|---|
| `V1BackgroundBrush` | `#002029` | `--ink-black` |
| `V1HeaderBrush` | `#00303d` | `--jet-black` |
| `V1BorderBrush` | `#004052` | `--dark-teal` |
| `V1ActiveModeBrush` | `#00607a` | `--dark-teal-3` |
| `V1TextPrimaryBrush` | `#e0e0e0` | `--text-color` |
| `V1TextSecondaryBrush` | `#888888` | inactive text |
| `V1PerfGreenBrush` | `#7aff9e` | `.perf-green` |
| `V1AuthBadgeBrush` | `#ff8c00` | auth badge |
