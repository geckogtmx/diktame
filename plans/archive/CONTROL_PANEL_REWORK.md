# Control Panel V2 Rework — Implementation Plan

> **Status**: Complete (Phase 1–4). All tasks implemented, committed, and pushed.

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
   0    |    0    |   --   |    0            0    |    0    |    0   |   --
  SESS  |  WORDS  |  WPM   |   TOK         REQ  |  CHAR  | WORDS  | WORD/MIN
-------------------------------------      -------------------------------------
  --  |  --  |   --   |   --   |   --       --  |  --  |   --   |   --   |   --
  TOT |  REC |  TRNS  |  PROC  |  INJ      REC | TRNS |  PROC  |  INJ   |  TOT
-------------------------------------      -------------------------------------
 Hotkeys: [Ctrl+Alt+D] [R] [A] [N] [T]             dIKta.me V2.0
```

**Key differences from V1**:
- ~~V2 has a WinUI title bar~~ — Title bar removed via `OverlappedPresenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)` + `ExtendsContentIntoTitleBar`. Header row is the drag region via `SetTitleBar()`.
- Dictation presets are dynamic (1-8), arranged in a responsive 2x4 grid (max 2 rows x 4 columns)
- RAW is a toggle switch (5th toggle in the actions row), not a preset button
- Window size 369×274 (vs V1's 400×265 frameless) — nearly identical footprint

---

## Completed Changes (Phase 1–4)

### Files Modified

| # | File | Status | Changes |
|---|---|---|---|
| 1 | `src/DiktaMe.App/Views/ControlPanelPage.xaml` | DONE | Complete XAML rewrite — 6 rows, V1 color palette, grid-based metric cells, footer |
| 2 | `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | DONE | Auth badge, IsCloudMode→IsLocalMode rename+wiring, +KEY fix, RAW toggle, RefineVoice toggle, formatted perf strings (FormatMs), label properties, hotkey properties, DictationModeItem with Subtitle/BackgroundHex/ForegroundHex |
| 3 | `src/DiktaMe.App/MainWindow.xaml.cs` | DONE | Window 369×274, custom title bar (ExtendsContentIntoTitleBar + OverlappedPresenter, no caption buttons), SetTitleBar on Loaded |
| 4 | `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | DONE | Refine toggle wiring (Auto/Voice), `pipeline.StateChanged += _controlPanel.OnPipelineStateChanged` in all batch+streaming methods |
| 5 | `src/DiktaMe.App/App.xaml.cs` | DONE | Added `HideMainWindow()` method for tray-hide on close |

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
- **Row 0 (Header)**: 10x10 status dot (color-bound via StateToColor), 14pt status text, 3 badges (STT/LLM/Auth) with MaxWidth=90 truncation + tooltips, gear + close buttons
- **Row 1 (Presets)**: ItemsRepeater with UniformGridLayout 4-col, preset buttons with title+subtitle, active state via pre-computed colors
- **Row 2 (Toggles)**: 5-column Grid — SOUND, LOCAL, +KEY, RAW, REFINE — all ToggleSwitch with dynamic labels
- **Row 3 (Session)**: 4-column metric grid (1px dividers via background color trick) — REQ, CHAR, WORDS, WORD/MIN — with tooltips
- **Row 4 (Perf)**: 5-column metric grid — REC, TRNS, PROC, INJ, TOT (pipeline flow order) — Consolas font, green (#7aff9e) — with tooltips. TOT = pipeline-only latency (excludes recording)
- **Row 5 (Footer)**: Centered `"dIKta.me V2.0"` branding text

#### LoadingViewModel.cs
- Added `_controlPanel.OnPipelineStateChanged(this, PipelineState.Transcribing)` after audio recording completes in all 5 pipeline methods (Dictate, Refine, Ask, Translate, Note)

### Phase 2 — Visual Polish & Custom Title Bar

#### Custom Title Bar (MainWindow.xaml.cs)
- `ExtendsContentIntoTitleBar = true` — removes default WinUI chrome
- `OverlappedPresenter`: `IsMinimizable = false`, `IsMaximizable = false`, `SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)` — removes system caption buttons (min/max/close)
- `SetTitleBar(headerBar)` on `root.Loaded` — makes Row 0 the drag region, auto-excludes interactive children (gear + close buttons)
- `FindDescendant<T>()` helper traverses visual tree from Window to locate `ControlPanelPage.HeaderBar`
- Window sized down from 520×380 → 410×340 → 369×306 → 369×274 (final)

#### Close Button (ControlPanelPage.xaml + ControlPanelViewModel.cs)
- Added [X] button (`&#xE10A;` Segoe MDL2) next to gear in `HeaderButtons` StackPanel
- `CloseWindowCommand` calls `App.Current.HideMainWindow()` → `AppWindow.Hide()` (tray stays alive)

#### Toggle Alignment Fix
- ToggleSwitch has internal header column that reserves space even when `OffContent=""` / `OnContent=""`
- Fixed with `Padding="0" Margin="12,0,0,0"` to visually center the track above labels
- Reduced toggle `Spacing` from 4 to 2 for tighter grouping

#### Footer Rework
- Replaced hotkey display (5 border-styled tags) with simple centered `"dIKta.me V2.0"` branding text
- `FontSize="10"`, `Foreground="#666666"`, `FontWeight="Medium"`

---

## Phase 3 — Refine Wiring, State Propagation, Bug Fixes ✅

All 3 tasks completed (commit `684cfb6`):

1. **REFINE toggle → pipeline wiring** — `LoadingViewModel` reads `_controlPanel.RefineMode` to pick `refine_auto` vs `refine_instruction` profile. Auto mode skips audio recording.
2. **Pipeline state propagation** — `pipeline.StateChanged += _controlPanel.OnPipelineStateChanged` wired in all 7 batch+streaming methods. Manual Transcribing calls removed.
3. **Stale description text** — `ControlPanelConfigPage.xaml` updated to match current row contents.

Also fixed: RefineMode persisted to settings, stale text after cancel, silent toast notifications.

---

## Phase 4 — Telemetry Rework + Badge Truncation ✅

Completed across commits `849e782`, `c32af04`, `8db3491`:

1. **Session stats rework** — Renamed SESS→REQ, removed TOK (fake `words×1.3`), added CHAR (`Text.Length`), fixed WPM to wall-clock calculation (`words / minutesSinceFirstRequest`). Final order: REQ > CHAR > WORDS > WORD/MIN.
2. **Perf row reorder** — Reordered to pipeline flow: REC > TRNS > PROC > INJ > TOT.
3. **Tooltips** — All 9 telemetry cells have `ToolTipService.ToolTip` descriptions.
4. **TOT pipeline-only** — `TotalMs = total.ElapsedMilliseconds` (excludes recording). Tooltip: "Pipeline latency (excludes recording)".
5. **Header badge truncation** — STT/LLM badges: `MaxWidth="90"` + `TextTrimming="CharacterEllipsis"` + tooltip showing full provider name. Auth badge: static tooltip "Authentication mode".

### Files modified in Phase 4
- `ControlPanelPage.xaml` — session row, perf row, tooltips, badge truncation
- `ControlPanelViewModel.cs` — `RequestCount`, `CharCount`, removed `TokenCount`, wall-clock WPM
- `MetricsCollector.cs` — `_sessionChars`, `_firstRequestTime`, `SessionStats` record extended
- `PipelineResult.cs` — added `CharCount` computed property
- `ControlPanelConfigPage.xaml` — updated description text
- 5 pipeline files — reverted `TotalMs` to pipeline-only

---

### Skipped (optional cosmetic — revisit later)
- Left-border state indicator (4px colored border)
- Recording pulse animation (background alpha fade)

---

## Telemetry Fields Reference

### Session Stats (Row 3, 4 columns)
| Label | Property | Source | Tooltip | Format |
|---|---|---|---|---|
| REQ | `RequestCount` | `SessionStats.Sessions` | "Requests this session" | Integer |
| CHAR | `CharCount` | `SessionStats.Chars` | "Characters produced" | Integer |
| WORDS | `WordCount` | `SessionStats.Words` | "Words produced" | Integer |
| WORD/MIN | `WordsPerMinuteFormatted` | `Words / MinutesSinceFirstRequest` | "Words per minute since first request" | Integer or "--" |

### Performance Stats (Row 4, 5 columns — pipeline flow order)
| Label | Property | Source | Tooltip | Format |
|---|---|---|---|---|
| REC | `LastRecordingFormatted` | `PipelineResult.RecordingMs` | "Recording duration" | `0.50s` / `12.5s` / "--" |
| TRNS | `LastTranscriptionFormatted` | `PipelineResult.TranscriptionMs` | "Transcription latency" | Same format |
| PROC | `LastProcessingFormatted` | `PipelineResult.ProcessingMs` | "LLM processing latency" | Same format |
| INJ | `LastInjectionFormatted` | `PipelineResult.InjectionMs` | "Text injection latency" | Same format |
| TOT | `LastTotalFormatted` | `PipelineResult.TotalMs` (pipeline-only, excludes recording) | "Pipeline latency (excludes recording)" | Same format |

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
