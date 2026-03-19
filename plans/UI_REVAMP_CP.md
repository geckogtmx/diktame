# UI Revamp: Control Panel Auto-Collapse & Voice Waveform

This document is the comprehensive spec for two related Control Panel enhancements: **auto-collapse** (the bar shrinks to a minimal width after idle) and **voice waveform** (a live visual indicator behind the status text).

---

## 1. Feature Overview

### Auto-Collapse
When the Control Panel is in bar mode (`IsExpanded=false`), after a configurable idle timeout the bar smoothly shrinks from 420px to ~170px, hiding the right-side controls (STT/LLM/TTS badges, chevron, gear, close). Only the status dot and text remain visible.

A second, longer timeout triggers the existing auto-hide (opacity fade to near-invisible).

**Only mouse hover expands the collapsed bar.** Pipeline activity (recording, transcribing, etc.) restores opacity but does NOT expand width — the bar stays minimal during active use.

### Voice Waveform
A subtle voice-reactive visual behind the status text in the header bar. Two user-selectable styles:

- **Sine Wave**: A smooth flowing line that modulates amplitude with voice level
- **Amplitude Bars**: Horizontal VU-meter-style bars whose heights track voice activity

Only visible during activity (recording/processing). Fades in when recording starts, fades out when idle. Works in both full-width and collapsed states.

---

## 2. Two-Stage Idle Behavior

```
┌──────────────────────────────────────────────────────────────┐
│ IDLE                                                          │
│                                                                │
│  0s ──────── Xs ──────────────── Ys ───────────────────────── │
│  │           │                    │                            │
│  │ Full bar  │ Stage 1: Collapse  │ Stage 2: Hide (fade)      │
│  │ (420px)   │ (420→170px anim)   │ (opacity 255→5)           │
│  │           │                    │                            │
│  └───────────┴────────────────────┴────────────────────────── │
│                                                                │
│  Hover → Expand (170→420px)  +  Restore opacity               │
│  Activity → Restore opacity ONLY (bar stays collapsed)        │
└──────────────────────────────────────────────────────────────┘
```

**Constraint**: Hide delay (Y) must always be ≥ Collapse delay (X). Enforced in settings save.

---

## 3. Task Breakdown

### CP.1 — Settings Model
**Description**: Add `AutoCollapseEnabled`, `AutoCollapseDelaySeconds`, and `WaveformStyle` to `ControlPanelSettings`.
**Files**: `src/DiktaMe.Core/Config/AppSettings.cs`
**Success Criteria**:
- Three new properties with sensible defaults (`false`, `10`, `"Wave"`)
- Build passes with no warnings
- Existing settings deserialization unaffected (new properties use `init` defaults)

### CP.2 — ControlPanelViewModel Properties
**Description**: Add observable properties for collapse and waveform settings, load from `SettingsManager`.
**Files**: `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`
**Success Criteria**:
- `AutoCollapseEnabled`, `AutoCollapseDelaySeconds`, `WaveformStyle` properties exist
- Loaded correctly in `LoadFromSettings()`
- Existing properties unaffected

### CP.3 — Waveform XAML Elements
**Description**: Add `Polyline` (sine wave) and bar container to `HeaderBar` grid, behind status text.
**Files**: `src/DiktaMe.App/Views/ControlPanelPage.xaml`
**Success Criteria**:
- Both elements present in XAML, `IsHitTestVisible="False"`, `Opacity="0"`
- Build passes (no XAML compiler crashes)
- Existing header layout unaffected visually (waveform starts invisible)

### CP.4 — Waveform Rendering Engine
**Description**: Implement `UpdateWaveform()`, `UpdateWaveformSine()`, `UpdateWaveformBars()`, `FadeWaveform()` in the 33ms timer tick. Wire into `OnEffectTimerTick()`.
**Files**: `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs`
**Success Criteria**:
- Sine wave: smooth flowing line, amplitude tracks `AudioLevelMonitor.SmoothedLevel`
- Bars: 20 vertical rectangles, heights modulate with voice
- Fades in on recording start (~300ms), fades out on idle (~600ms)
- Respects `WaveformStyle` setting ("Wave", "Bars", "Off")
- Theme-aware colors (uses `AppAccentBrush`)
- No performance regression (30fps maintained)

### CP.5 — Collapse/Expand Animation
**Description**: Implement the two-stage idle behavior: width animation (420→170px), expand on hover, separate opacity restore on activity.
**Files**: `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs`
**Success Criteria**:
- Bar collapses smoothly after X seconds idle (configurable)
- `HeaderButtons` opacity fades to 0 during first 60% of collapse animation
- `HeaderButtons.Visibility = Collapsed` after animation completes
- Window width shrinks via `AppWindow.Resize()`, anchored to left edge
- Mouse hover expands bar back to 420px with smooth animation
- Pipeline activity restores opacity but does NOT expand width
- Hotkey press restores opacity but does NOT expand width
- Collapse only triggers when `IsExpanded == false` (bar mode)
- User expanding panel (chevron) immediately restores full width
- Auto-hide fade only triggers after collapse delay (Y ≥ X enforced)
- `OnRootGridSizeChanged` uses `_currentWidth` instead of hardcoded 420

### CP.6 — Settings UI
**Description**: Add Waveform style selector to Visual Effects card, add Auto-Collapse section to GeneralSettingsPage.
**Files**:
- `src/DiktaMe.App/ViewModels/Settings/GeneralSettingsViewModel.cs`
- `src/DiktaMe.App/Views/Settings/GeneralSettingsPage.xaml`
**Success Criteria**:
- Waveform style: RadioButtons (Sine Wave / Amplitude Bars / Off), inside Visual Effects card
- Auto-Collapse: ToggleSwitch + ComboBox (5s/10s/30s/1m), between Visual Effects and Auto-Hide sections
- Changing settings persists to `settings.json` immediately
- Constraint enforced: if both enabled, hide delay ≥ collapse delay
- All controls themed correctly (3 themes)

### CP.7 — Localization
**Description**: Add all localization strings for new settings.
**Files**:
- `src/DiktaMe.App/Strings/en/Resources.resw`
- `src/DiktaMe.App/Strings/es-MX/Resources.resw`
**Success Criteria**:
- All Uids resolve without fallback warnings
- Spanish translations are accurate

### CP.8 — Build & Integration Test
**Description**: Full build + test suite pass, manual visual verification.
**Files**: N/A (verification only)
**Success Criteria**:
- `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors
- `dotnet test DiktaMe.sln` — all tests pass
- Manual test matrix completed (see Section 5)

---

## 4. Edge Cases

| # | Scenario | Expected Behavior |
|---|----------|-------------------|
| 1 | User expands panel while bar is collapsed | Full width restored immediately, `HeaderButtons` visible |
| 2 | User changes expand direction while collapsed | Direction swaps, width stays collapsed (no visual glitch) |
| 3 | Both collapse and hide disabled | No idle behavior; bar stays at full width |
| 4 | Collapse enabled, hide disabled | Bar collapses but never fades |
| 5 | Hide enabled, collapse disabled | Existing behavior: bar fades at full width |
| 6 | Recording starts while collapsed | Bar stays collapsed, opacity restores, status text updates |
| 7 | Hotkey pressed while collapsed | Opacity restores, bar stays collapsed |
| 8 | Collapse delay > hide delay in settings | Hide delay clamped up to match collapse delay |
| 9 | Waveform set to "Off" | No visual during recording, glow/shimmer still work |
| 10 | Theme switch during active waveform | Waveform color updates on next tick (uses shared brush) |
| 11 | Window hidden to tray while collapsed | Timer pauses (existing behavior), width state preserved |
| 12 | Bar collapses, user hovers briefly then leaves | Bar expands on enter, idle timer restarts on leave |

---

## 5. Manual Test Matrix

### Waveform Tests

| # | Test | Steps | Expected |
|---|------|-------|----------|
| W1 | Sine wave appears on record | Set style=Wave, press dictation hotkey, speak | Flowing sine line fades in behind text, amplitude tracks voice |
| W2 | Sine wave fades on idle | Stop speaking, release hotkey | Wave amplitude decays, line fades out (~600ms) |
| W3 | Bars appear on record | Set style=Bars, dictate | Vertical bars fade in, heights modulate with voice |
| W4 | Bars fade on idle | Stop | Bars shrink and fade out |
| W5 | Off disables waveform | Set style=Off, dictate | No waveform visible, glow/shimmer still work |
| W6 | Waveform in collapsed bar | Collapse bar, wait for auto-collapse, dictate | Waveform renders in narrow 170px bar |
| W7 | Theme switch | Switch Midnight→Ember→Frost while waveform active | Colors update correctly |

### Auto-Collapse Tests

| # | Test | Steps | Expected |
|---|------|-------|----------|
| C1 | Collapse after idle | Enable collapse (5s), collapse to bar, wait 5s | Bar shrinks to 170px, badges fade out |
| C2 | Hover expands | Hover over collapsed bar | Bar expands to 420px with animation |
| C3 | Activity does NOT expand | While collapsed, press dictation hotkey | Bar stays collapsed, opacity restores, status shows LISTENING |
| C4 | Full dictation while collapsed | Dictate while collapsed | Status cycles through states, bar stays narrow |
| C5 | Hide after collapse | Enable both (collapse=5s, hide=30s), wait 35s | Bar collapses at 5s, fades at 30s |
| C6 | Expand panel cancels collapse | While collapsed, double-click header | Full panel expands, width restored |
| C7 | Constraint enforcement | Set collapse=60s, hide=10s, save | Hide delay clamped to 60s |
| C8 | Expand-up mode | Set direction=Up, enable collapse | Collapse/expand works correctly with bottom-anchored header |
| C9 | Disabled | Disable auto-collapse | Bar never collapses regardless of idle time |

### Settings UI Tests

| # | Test | Steps | Expected |
|---|------|-------|----------|
| S1 | Waveform selector | Open Settings > General > Application > Visual Effects | RadioButtons: Sine Wave / Amplitude Bars / Off |
| S2 | Collapse toggle | Open Settings > General > Application > Auto-Collapse | ToggleSwitch + delay ComboBox |
| S3 | Persistence | Change settings, close/reopen app | Settings preserved |
| S4 | All three themes | Switch themes | Settings controls render correctly |

---

## 6. Technical Constraints

1. **No Win2D/CanvasControl**: Using native WinUI 3 `Polyline` and `Rectangle` shapes — no extra NuGet dependency needed.
2. **30fps budget**: Waveform updates ~40 points per tick. Must not regress the existing glow/shimmer/auto-hide timer.
3. **Window resize**: `AppWindow.Resize()` during animation must not conflict with `OnRootGridSizeChanged()`. Guard with `_currentWidth` field.
4. **Collapsed width (170 DIPs)**: Must accommodate longest status text "TRANSCRIBING" at 13pt Bold Inter with CharacterSpacing=100 + 10px dot + 8px gap + 24px padding.
5. **Polyline.Points.Clear()**: Clearing and repopulating each tick is acceptable for 40 points; no significant GC pressure.

---

## 7. File Modification Map

| File | Tasks | Changes |
|------|-------|---------|
| `src/DiktaMe.Core/Config/AppSettings.cs` | CP.1 | +3 properties on `ControlPanelSettings` |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | CP.2 | +3 observable properties, update `LoadFromSettings()` |
| `src/DiktaMe.App/Views/ControlPanelPage.xaml` | CP.3 | +Polyline, +bar container in HeaderBar |
| `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs` | CP.4, CP.5 | Waveform engine, collapse animation, restore logic |
| `src/DiktaMe.App/ViewModels/Settings/GeneralSettingsViewModel.cs` | CP.6 | +waveform/collapse fields, wire Save() |
| `src/DiktaMe.App/Views/Settings/GeneralSettingsPage.xaml` | CP.6 | +waveform selector, +auto-collapse section |
| `src/DiktaMe.App/Strings/en/Resources.resw` | CP.7 | +11 localization keys |
| `src/DiktaMe.App/Strings/es-MX/Resources.resw` | CP.7 | +11 Spanish translations |

---

**Status:** Approved Plan | **Priority:** Medium | **Target:** V2.1 UI Refresh Phase 4+
