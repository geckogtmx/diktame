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

---

## 8. CP.9 — Snap Bar to Screen Position

### Overview

Add a **snap-to-position** feature: the Control Panel bar can be placed at 6 predefined screen positions. Selecting a position in Settings instantly moves the bar to that spot. Position is persisted across restarts.

```
┌─────────────────────────────────────┐
│ ┌──────┐  ┌──────────┐  ┌──────┐   │
│ │TopL  │  │TopCenter │  │TopR  │   │
│ └──────┘  └──────────┘  └──────┘   │
│                                     │
│                                     │
│ ┌──────┐  ┌──────────┐  ┌──────┐   │
│ │BotL  │  │BotCenter │  │BotR  │   │
│ └──────┘  └──────────┘  └──────┘   │
└─────────────────────────────────────┘
```

**Positions**: `TopLeft`, `TopCenter`, `TopRight`, `BottomLeft`, `BottomCenter`, `BottomRight`

**Behavior**:
- Selecting a position instantly moves the bar (no animation)
- Bottom positions auto-set `ExpandUpward = true`; top positions auto-set `ExpandUpward = false`
- Position is applied on app startup (after `MainWindow` creates the `AppWindow`)
- Bar width uses `_currentWidth` (420 full / 170 collapsed) for correct centering
- A small margin (8px) keeps the bar from touching screen edges

### CP.9.1 — Settings Model

**File**: `src/DiktaMe.Core/Config/AppSettings.cs`

Add to `ControlPanelSettings` record:
```csharp
/// <summary>Screen snap position: TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight.</summary>
public string BarPosition { get; init; } = "TopRight";
```

### CP.9.2 — ControlPanelViewModel

**File**: `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`

- Add `[ObservableProperty] private string _barPosition = "TopRight";`
- In `LoadFromSettings()`: `BarPosition = settings.ControlPanel.BarPosition ?? "TopRight";`
- Add change handler:
  ```csharp
  partial void OnBarPositionChanged(string value)
  {
      if (!_suppressSave)
      {
          var updated = _settings.Current with
          {
              ControlPanel = _settings.Current.ControlPanel with { BarPosition = value }
          };
          _ = _settings.UpdateAsync(updated);

          // Auto-set expand direction based on position
          bool isBottom = value.StartsWith("Bottom", StringComparison.Ordinal);
          if (isBottom != ExpandUpward)
          {
              ExpandUpward = isBottom;
          }
      }
  }
  ```

### CP.9.3 — Snap Position Engine

**File**: `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs`

Add `SnapToPosition()` method:
```csharp
private void SnapToPosition(string position)
{
    var window = App.Current.MainWindow;
    if (window is null) return;

    var appWindow = window.AppWindow;
    var windowId = appWindow.Id;
    var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
        windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
    var workArea = displayArea.WorkArea; // excludes taskbar

    double scale = XamlRoot?.RasterizationScale ?? 1.0;
    int barWidth = (int)(_currentWidth * scale);
    int barHeight = appWindow.Size.Height;
    int margin = (int)(8 * scale); // 8 DIP margin from edges

    int x, y;
    switch (position)
    {
        case "TopLeft":
            x = workArea.X + margin;
            y = workArea.Y + margin;
            break;
        case "TopCenter":
            x = workArea.X + (workArea.Width - barWidth) / 2;
            y = workArea.Y + margin;
            break;
        case "TopRight":
            x = workArea.X + workArea.Width - barWidth - margin;
            y = workArea.Y + margin;
            break;
        case "BottomLeft":
            x = workArea.X + margin;
            y = workArea.Y + workArea.Height - barHeight - margin;
            break;
        case "BottomCenter":
            x = workArea.X + (workArea.Width - barWidth) / 2;
            y = workArea.Y + workArea.Height - barHeight - margin;
            break;
        case "BottomRight":
            x = workArea.X + workArea.Width - barWidth - margin;
            y = workArea.Y + workArea.Height - barHeight - margin;
            break;
        default:
            return; // unknown position, don't move
    }

    appWindow.Move(new Windows.Graphics.PointInt32(x, y));
}
```

**Wire it**:
- Call from `ViewModel.PropertyChanged` handler when `BarPosition` changes
- Call once during initialization (after `XamlRoot` is available) to apply saved position on startup
- Also call from `ApplyWindowWidth()` when bar width changes during collapse/expand — recenter if position is `*Center`

### CP.9.4 — Settings UI

**File**: `src/DiktaMe.App/Views/Settings/GeneralSettingsPage.xaml`

Add a new section between "Control Panel Configuration" and "Visual Effects" cards:

```xaml
<!-- Bar Position -->
<TextBlock l:Uids.Uid="Settings_BarPosition_Title"
           FontSize="18" FontWeight="Bold"
           Foreground="{StaticResource AppTextBrush}" Margin="0,24,0,4"/>
<TextBlock l:Uids.Uid="Settings_BarPosition_Description"
           FontSize="12" Foreground="{StaticResource AppTextDimBrush}" Margin="0,0,0,8"/>

<Border Style="{StaticResource SettingsCardStyle}">
    <StackPanel Spacing="8">
        <!-- 3x2 grid of position buttons -->
        <Grid ColumnSpacing="8" RowSpacing="8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <Button Content="&#xE110; Top Left" Grid.Row="0" Grid.Column="0"
                    Command="{x:Bind ViewModel.SetBarPositionCommand}"
                    CommandParameter="TopLeft"
                    HorizontalAlignment="Stretch" MinHeight="36"/>
            <Button Content="&#xE110; Top Center" Grid.Row="0" Grid.Column="1"
                    Command="{x:Bind ViewModel.SetBarPositionCommand}"
                    CommandParameter="TopCenter"
                    HorizontalAlignment="Stretch" MinHeight="36"/>
            <Button Content="&#xE110; Top Right" Grid.Row="0" Grid.Column="2"
                    Command="{x:Bind ViewModel.SetBarPositionCommand}"
                    CommandParameter="TopRight"
                    HorizontalAlignment="Stretch" MinHeight="36"/>
            <Button Content="&#xE110; Bottom Left" Grid.Row="1" Grid.Column="0"
                    Command="{x:Bind ViewModel.SetBarPositionCommand}"
                    CommandParameter="BottomLeft"
                    HorizontalAlignment="Stretch" MinHeight="36"/>
            <Button Content="&#xE110; Bottom Center" Grid.Row="1" Grid.Column="1"
                    Command="{x:Bind ViewModel.SetBarPositionCommand}"
                    CommandParameter="BottomCenter"
                    HorizontalAlignment="Stretch" MinHeight="36"/>
            <Button Content="&#xE110; Bottom Right" Grid.Row="1" Grid.Column="2"
                    Command="{x:Bind ViewModel.SetBarPositionCommand}"
                    CommandParameter="BottomRight"
                    HorizontalAlignment="Stretch" MinHeight="36"/>
        </Grid>
    </StackPanel>
</Border>
```

**File**: `src/DiktaMe.App/ViewModels/Settings/GeneralSettingsViewModel.cs`

- Add `[RelayCommand] private void SetBarPosition(string position)` that updates the setting
- The command saves to settings, which fires `SettingsChanged`, which ControlPanelViewModel picks up and triggers `SnapToPosition()`

### CP.9.5 — Localization

**Files**: `src/DiktaMe.App/Strings/en/Resources.resw`, `es-MX/Resources.resw`

| Key | English | Spanish |
|-----|---------|---------|
| `Settings_BarPosition_Title.Text` | Bar Position | Posición de la Barra |
| `Settings_BarPosition_Description.Text` | Choose where the control panel bar snaps to on your screen | Elige dónde se coloca la barra del panel de control en tu pantalla |

Button labels are inline content (not localized Uids) — keep English for V1, can localize later.

### CP.9.6 — Edge Cases

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Bar collapses (420→170) while at TopCenter | Re-snap to keep centered (narrower bar, same center point) |
| 2 | Bar expands (170→420) while at TopCenter | Re-snap to keep centered |
| 3 | Bottom position selected | `ExpandUpward` auto-set to `true` |
| 4 | Top position selected | `ExpandUpward` auto-set to `false` |
| 5 | Multi-monitor | `DisplayArea.GetFromWindowId` returns the monitor the bar is currently on |
| 6 | Settings.json missing `BarPosition` | Defaults to `"TopRight"` (init default) |
| 7 | User drags bar manually after snap | Bar stays where dragged; next snap overrides |
| 8 | App restart | Position re-applied from saved `BarPosition` on startup |

### CP.9.7 — File Modification Map

| File | Changes |
|------|---------|
| `src/DiktaMe.Core/Config/AppSettings.cs` | +1 property `BarPosition` on `ControlPanelSettings` |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | +1 observable property, load/save, auto-set ExpandUpward |
| `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs` | +`SnapToPosition()` method, wire to property change + startup + width change |
| `src/DiktaMe.App/MainWindow.xaml.cs` | Call initial snap after window creation (or defer to ControlPanelPage init) |
| `src/DiktaMe.App/ViewModels/Settings/GeneralSettingsViewModel.cs` | +`SetBarPositionCommand`, wire to settings save |
| `src/DiktaMe.App/Views/Settings/GeneralSettingsPage.xaml` | +Bar Position section with 3x2 button grid |
| `src/DiktaMe.App/Strings/en/Resources.resw` | +2 localization keys |
| `src/DiktaMe.App/Strings/es-MX/Resources.resw` | +2 Spanish translations |

---

## 9. Bug 3: Nav Text Contrast — WinUI 3 ThemeResource Gotcha

### Problem
Settings sidebar nav items (General, Audio, Motor IA, etc.) have white text on hover and selected states. User wants blue text on hover and dark text on selected (against the blue selected background).

### Failed approach: NavigationViewItemForeground* ThemeResource keys

Changed `NavigationViewItemForegroundPointerOver` → `p.NavActive` (blue) and `NavigationViewItemForegroundSelected` → `p.Background` (dark navy) in both `ThemeService.cs` BrushKeys array and `App.xaml` ThemeDictionaries.

**Result**: ALL nav item text turned dark/invisible — not just selected or hovered items. Attempted 3 times with same failure.

### Root cause

WinUI 3's `NavigationViewItem` template uses `{ThemeResource NavigationViewItemForeground}` as the base `Style.Setter` for `Foreground`, with per-state overrides via `VisualState.Setters` targeting `ContentPresenter.Foreground`. When `ThemeService.ApplyTheme()` mutates the `SolidColorBrush.Color` of ThemeDictionary entries at runtime, the VisualStateManager does NOT cleanly re-resolve per-state brushes after state transitions. Items that have transitioned through selected/hover states retain stale brush references, causing the dark color to bleed across all items.

**Key lesson**: `NavigationViewItemForeground*` ThemeResource keys CANNOT be used for differentiated per-state foreground colors when using runtime brush Color mutation (our ThemeService pattern). They are safe to set identically (e.g., all to `p.Text` = white), but setting different colors per state causes cross-contamination.

### Attempted approach 2: Code-behind in SettingsWindow.xaml.cs

Applied foreground colors directly on each `NavigationViewItem` element via:
- `SelectionChanged` handler: selected item → `p.Background` (dark navy), previously-selected → `p.TextDim`
- `PointerEntered` / `PointerExited` handlers: hover → `p.NavActive` (blue), exit → `p.TextDim` (unless selected)
- `ThemeChanged` handler: re-apply all foreground colors from new palette
- Uses `ReferenceEquals(navItem, NavView.SelectedItem)` (not `==`) to avoid CS0253

**Result**: Partially working but user reports:
1. **Selected item contrast poor** — `p.Background` (#0A0918 dark navy on Midnight) against the blue NavigationViewItem selected background doesn't read well
2. **Hover contrast not distinctive enough** — `p.NavActive` (#3B82F6 blue) may be getting overridden by WinUI VisualState Setters
3. **Non-selected items too gray** — `p.TextDim` (#99FFFFFF = white at 60% opacity) looks washed out compared to original full-white text

**Suspected additional root cause**: WinUI 3's `NavigationViewItem` ControlTemplate uses `VisualState.Setters` to target `ContentPresenter.Foreground` with `{ThemeResource NavigationViewItemForeground*}` values. These VisualState Setters may **override** the outer `NavigationViewItem.Foreground` property set via code-behind, since VisualState Setters have higher precedence than local values in the WinUI property system.

### Current state of code-behind (in SettingsWindow.xaml.cs)

```csharp
// Methods implemented:
private void ApplyNavItemColors()  // Sets all items: selected=p.Background, others=p.TextDim
private void NavItem_PointerEntered(...)  // Non-selected → p.NavActive
private void NavItem_PointerExited(...)   // Non-selected → p.TextDim

// Called from:
// - Constructor (after NavView.SelectedItem = NavView.MenuItems[0])
// - NavView_SelectionChanged (after ContentFrame.Navigate)
// - ThemeChanged handler
```

### What still needs investigation

1. **VisualState precedence**: The code-behind sets `NavigationViewItem.Foreground`, but the template's `VisualState.Setters` may target `ContentPresenter.Foreground` directly — the inner ContentPresenter may ignore the outer Foreground if the VisualState Setter overrides it. Need to either:
   - Walk the visual tree to find the `ContentPresenter` and set its `Foreground` directly
   - Or override the ThemeResource keys with **new SolidColorBrush instances** (not mutated existing ones) — create fresh brushes in code-behind and inject them into the item's local `Resources` dictionary
   - Or use a completely custom `Style` for `NavigationViewItem` with explicit `VisualState.Setters` that reference code-behind-controlled brushes

2. **Color choices**: Even if the mechanism works, the colors may need tuning:
   - Selected: instead of `p.Background` (nearly black), try white or a contrasting bright color
   - Non-selected: instead of `p.TextDim` (60% opacity white), use `p.Text` (full white) for better readability
   - Hover: `p.NavActive` is correct conceptually but needs to survive VisualState overrides

3. **Per-item Resources approach**: Create a `SolidColorBrush` with key `NavigationViewItemForeground` in each item's local `Resources` dictionary. VisualState Setters resolve `{ThemeResource}` starting from the element's local resources, so this would override the app-level value per-item without ThemeService mutation issues.

### Files (current state)

| File | State |
|------|-------|
| `ThemeService.cs` | REVERTED — all `NavigationViewItemForeground*` back to `p.Text` |
| `App.xaml` | REVERTED — all `NavigationViewItemForeground*` back to `#FFFFFF` / `#1A1A2E` |
| `SettingsWindow.xaml.cs` | Code-behind approach IMPLEMENTED but NOT producing good results |
| `SegmentedButtonStyle.xaml` | `AppNavActiveBrush` on active pill (safe, uses StaticResource) — KEEP |

### Status: UNRESOLVED — needs fresh investigation
