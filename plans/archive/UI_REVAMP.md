# UI Revamp: Glassmorphic Settings & Control Panel

> **Status:** ✅ COMPLETE (all 6 phases implemented and committed, code-verified 2026-03-23)

This document serves as the comprehensive technical guide and vision for the dIKta.me V2 UI redesign. It consolidates all design requirements, architectural decisions, and implementation phases into a single source of truth.

---

## 1. Visual Vision & Design Principles

The goal is to transition from the V1 legacy teal-dark aesthetic to a high-end, glassmorphic interface that feels responsive, premium, and modern.

### Core Principles
- **Aesthetics First**: Use rich, harmonious color palettes (Midnight, Ember, Frost) rather than generic colors.
- **Glassmorphism**: Utilize subtle background blurs (Acrylic/Mica), glassy borders, and semi-transparent layers to create depth.
- **Typography**: Complete migration to the "Inter" font family (Regular and SemiBold) for all UI elements. Download from Google Fonts.
- **Micro-animations**: Smooth transitions for hover effects, selection indicators, and active states.
- **Premium Controls**: Custom-styled segmented buttons (pill-shaped), toggle switches, sliders, and combo boxes.

---

## 2. Technical Architecture: Theme System

A live-swappable theme engine is required to allow users to switch color schemes without an application restart.

### `ThemeService` Implementation
- **Location**: `DiktaMe.App/Services/ThemeService.cs` (singleton, NOT in DiktaMe.Core — requires `Microsoft.UI.Xaml` dependency).
- **Core Logic**: `ApplyTheme(string name)` mutates brush Color properties in-place on a single persistent `ResourceDictionary` loaded in `Application.Current.Resources.MergedDictionaries`.
- **Theme Persistence**: Store `ThemeName` in `AppSettings.GeneralSettings` (default: `"Midnight"`). New property; no migration needed — JSON deserializer uses default for missing keys.
- **RequestedTheme**: Set `Application.Current.RequestedTheme` to `Dark` (Midnight, Ember) or `Light` (Frost) so all WinUI system controls render correctly. If runtime switching of `Application.RequestedTheme` fails, fall back to per-Window `RequestedTheme`.
- **Startup**: `ApplyTheme()` called in `App.OnLaunched()` after settings are loaded.
- **Live Switching**: Exposed to `GeneralSettingsViewModel` for the theme selector UI.

### WinUI 3 Resource Constraints
> **IMPORTANT**: `DynamicResource` does NOT exist in WinUI 3. It is a WPF-only concept. WinUI 3 uses `ThemeResource` for theme-responsive system resources. For custom palette brushes, we keep `StaticResource` and perform **in-place Color mutation** on the brush objects. This is compatible with the ControlPanel's 30fps visual effects animation loop, which also mutates brush Colors directly.
>
> `ThemeResource` is used ONLY for WinUI system resource keys (e.g., `NavigationViewDefaultPaneBackground`, `CardBackgroundFillColorDefaultBrush`) that must respond to Light↔Dark `RequestedTheme` changes.

---

## 3. Color Palettes (The Three Variations)

Three distinct palette configurations, stored as `ResourceDictionary` source data in `src/DiktaMe.App/Themes/Palettes/`. Each defines the same set of `SolidColorBrush` resource keys:

| Resource Key | Ember (Warm Dark) | Midnight (Cool Dark) | Frost (Light) |
|---|---|---|---|
| `AppBackgroundBrush` | `#2A1810` → `#0D0806` | `#1C1A3A` → `#0A0918` | `#C9E8F5` → `#F0F4FF` |
| `AppSurfaceBrush` | `#1C1C1CCC` | `#16152ECC` | `#FFFFFFE6` |
| `AppSurface2Brush` | `#252525CC` | `#1E1D38CC` | `#F2F4FFE6` |
| `AppBorderBrush` | `#FFFFFF14` | `#FFFFFF14` | `#00000014` |
| `AppNavActiveBrush` | Purple `#7C3AED` | Blue `#3B82F6` | Sky Blue `#38BDF8` |
| `AppAccentBrush` | Amber `#F59E0B` | Cyan `#5DEBFF` | Blue `#0EA5E9` |
| `AppTextBrush` | `#FFFFFF` | `#FFFFFF` | `#1A1A2E` |
| `AppTextDimBrush` | `#FFFFFF99` | `#FFFFFF99` | `#1A1A2E99` |
| `AppPerfGreenBrush` | `#7aff9e` | `#7aff9e` | `#059669` |
| *RequestedTheme* | Dark | Dark | Light |

### V1 → New Brush Key Mapping

| Old V1 Key | New Key | V1 Color |
|---|---|---|
| `V1BackgroundBrush` | `AppBackgroundBrush` | `#002029` |
| `V1HeaderBrush` | `AppSurfaceBrush` | `#00303d` |
| `V1DetailBrush` | `AppSurface2Brush` | `#002a35` |
| `V1BorderBrush` | `AppBorderBrush` | `#004052` |
| `V1ActiveModeBrush` | `AppNavActiveBrush` | `#00607a` |
| `V1TextPrimaryBrush` | `AppTextBrush` | `#e0e0e0` |
| `V1TextSecondaryBrush` | `AppTextDimBrush` | `#888888` |
| `V1PerfGreenBrush` | `AppPerfGreenBrush` | `#7aff9e` |

**Migration scope**: ~445 `StaticResource` references across 12 XAML files, plus ~20 inline NavigationView overrides in SettingsWindow.xaml, plus 6 cached brush fields + 24 hardcoded RGB constants in ControlPanelPage.xaml.cs.

---

## 4. Typography: Inter Font Integration

To ensure brand consistency, the Inter font must be embedded and applied globally.

1. **Download**: Inter-Regular.ttf and Inter-SemiBold.ttf from Google Fonts (static TTF, not variable).
2. **Assets**: Place in `src/DiktaMe.App/Assets/Fonts/`. Add as `<Content>` in csproj.
3. **Shared Resources**: Register in `SharedResources.xaml`:
    ```xml
    <FontFamily x:Key="AppFont">ms-appx:///Assets/Fonts/Inter-Regular.ttf#Inter</FontFamily>
    <FontFamily x:Key="AppFontSemiBold">ms-appx:///Assets/Fonts/Inter-SemiBold.ttf#Inter</FontFamily>
    <FontFamily x:Key="AppFontMono">Consolas</FontFamily>
    ```
4. **Global Application**: Set `FontFamily="{StaticResource AppFont}"` on the root container of each Window/Page:
    - `ControlPanelPage` root Grid
    - `SettingsWindow` root NavigationView
    - `LoadingWindow` root container
    - `QuickChatWindow` root container
5. **Icon glyphs** (`Segoe MDL2 Assets`) are unaffected — they use explicit `FontFamily` attributes.

---

## 5. Settings Navigation (No Restructuring)

The current settings navigation has **9 items** (not 13 as previously stated). These 9 categories are already well-organized and match the mockup designs. **No restructuring is needed** — only visual restyling.

### Current 9 Navigation Items (keeping as-is)
1. **General** (Tag: `general`) — Application behavior, Control Panel config, Visual Effects, Auto-Hide, Language. **Add Theme Selector here.**
2. **Hardware** (Tag: `hardware`) — Audio device, recording, ducking, sounds.
3. **AI Engine** (Tag: `aiengine`) — API Keys, STT, LLM, TTS, Chat, System Monitor (Master-Detail with Cloud/Local tabs).
4. **Workflows** (Tag: `workflows`) — Utility pipelines (Ask, Refine, Translate, Note, Chat).
5. **Dictation Presets** (Tag: `presets`) — CRUD for custom dictation modes.
6. **Snippets** (Tag: `snippets`) — Text expansion and find/replace.
7. **Privacy** (Tag: `privacy`) — Privacy level, PII scrub, history retention, wipe data.
8. **Account** (Tag: `account`) — Login, wallet balance, sync status.
9. **About** (Tag: `about`) — Version, credits, tech stack links.

### Menu Icon Updates
Update the `FontIcon` glyphs in `SettingsWindow.xaml` to match the target mockups:
- **AI Engine**: Update from Globe to a "Brain" or AI-centric network icon.
- **Pipelines / Workflows**: Update to a "pipes" / flow connection icon.
- **Snippets**: Update to code brackets (`</>`) icon.
*(Added by Gemini)*

---

## 6. Premium Control Styling

Styles defined as separate `ResourceDictionary` files in `src/DiktaMe.App/Themes/Styles/`, merged into App.xaml or SharedResources.xaml.

### Segmented Button (Cloud/Local)
- **File**: `Styles/SegmentedButtonStyle.xaml`
- **Visual**: A pill-shaped toggle where the active selection uses `AppNavActiveBrush`, inactive uses `AppSurface2Brush`.
- **Style**: Use `CornerRadius` to join two buttons into a single unit.
- **Scope**: Replace all Cloud/Local tab button pairs in AIEngineSettingsPage (STT, LLM, TTS sections).

### Glassmorphic Toggle Switches
- **File**: `Styles/ToggleSwitchStyle.xaml`
- **Layout Structure**:
    - **Vertical Stack**: Group settings under section headers (e.g., "Application", "Control Panel").
    - **Row Content**: Each toggle row consists of a title (Bold), a sub-description (Dimmed), and the toggle aligned to the right.
    - **Spacing**: Generous vertical padding between rows to maintain a clean, breathable interface.
- **Visual Style**:
    - **Track**: Capsule-shaped with a deep semi-transparent background. When ON, the track should have a soft glow using `AppAccentBrush` or `AppNavActiveBrush`.
    - **Thumb**: Circular, sliding within the track, with a slight drop shadow for depth.
    - **State Label**: Place an "On" or "Off" text label immediately to the right of the toggle.
- **Apply as implicit style** — all ToggleSwitches across the app get it automatically.

### Modern Slider
- **File**: `Styles/SliderStyle.xaml`
- **Visual**: Thin, subtle track with a large, glowing circular thumb.
- **Colors**: Use `AppAccentBrush` for the active track and thumb.

### Glass Inputs (ComboBox/TextBox/NumberBox)
- **File**: `Styles/InputStyles.xaml`
- **Visual**: Rounded corners (8px), semi-transparent `AppSurface2Brush` background, subtle `AppBorderBrush` border.

### Branding Box
- **Location**: Top of the Navigation Pane in SettingsWindow (NavigationView PaneHeader).
- **Content**: dIKta.me logo + text "dIKta.me".

---

## 7. Control Panel Revamp

The main HUD (ControlPanelPage) inherits the same theme-aware system. Existing 6-row grid layout is preserved.

- **Dashboard surfaces**: Apply `AppSurfaceBrush` / `AppSurface2Brush` to row backgrounds.
- **Status Badges**: `AppBorderBrush` background, dynamic foreground colors kept for STT/LLM/TTS state indicators.
- **Ready State**: Pulsing indicator using `AppAccentBrush` color.
- **Mode Buttons**: Rounded corners, `AppSurface2Brush` inactive, `AppNavActiveBrush` active. Update hardcoded hover/pressed colors in Button.Resources to use theme-derived values.
- **Stats Layout**: Keep existing 4-column (session) + 5-column (perf) grid layout. Apply theme brushes.
- **Visual Effects**: Update shimmer/edge glow gradient stops in ControlPanelPage.xaml.cs to derive from current theme accent colors. Add `LoadThemeColors()` method that reads palette brushes and computes corresponding bright variants for the 30fps animation loop. Subscribe to ThemeService theme-changed event.

---

## 8. Implementation Phases

### Phase 0: Theme Foundation
Build the theme engine and three palettes so every subsequent phase can reference theme-aware brushes.

| Task | Description | Files |
|------|-------------|-------|
| 0.1 | Add `ThemeName` property to `GeneralSettings` | `AppSettings.cs` |
| 0.2 | Create three palette ResourceDictionaries | `Themes/Palettes/MidnightPalette.xaml`, `EmberPalette.xaml`, `FrostPalette.xaml` |
| 0.3 | Implement `ThemeService` singleton | `Services/ThemeService.cs`, `App.xaml.cs` (DI registration + startup call) |
| 0.4 | Update SharedResources.xaml: remove V1 brushes, add App* fallback brushes | `Themes/SharedResources.xaml` |
| 0.5 | Update App.xaml MergedDictionaries order | `App.xaml` |
| 0.6 | Migrate all ~445 V1*Brush → App*Brush references | All 12 XAML views + SettingsWindow.xaml inline overrides |
| 0.7 | Update ControlPanelPage.xaml.cs cached brushes and RGB constants | `ControlPanelPage.xaml.cs` |
| ✓ | **Verify**: Build succeeds, app launches with Midnight, no visual regressions | |

### Phase 1: Inter Font Integration
Embed the Inter font and apply it globally.

| Task | Description | Files |
|------|-------------|-------|
| 1.1 | Download Inter-Regular.ttf + Inter-SemiBold.ttf from Google Fonts | `Assets/Fonts/` |
| 1.2 | Add `<Content>` entries to csproj | `DiktaMe.App.csproj` |
| 1.3 | Register font resources in SharedResources.xaml | `Themes/SharedResources.xaml` |
| 1.4 | Apply `AppFont` to root containers (4 windows/pages) | `ControlPanelPage.xaml`, `SettingsWindow.xaml`, `LoadingWindow.xaml`, `QuickChatWindow.xaml` |
| 1.5 | Replace inline `Consolas` with `AppFontMono` resource | `ControlPanelPage.xaml` |
| ✓ | **Verify**: All text renders in Inter, icon glyphs unaffected, no layout shifts | |

### Phase 2: Custom Control Styles
Create premium glassmorphic styles for core controls.

| Task | Description | Files |
|------|-------------|-------|
| 2.1 | Segmented Button style (pill-shaped Cloud/Local toggle) | `Themes/Styles/SegmentedButtonStyle.xaml`, `AIEngineSettingsPage.xaml` |
| 2.2 | Glassmorphic ToggleSwitch implicit style | `Themes/Styles/ToggleSwitchStyle.xaml` |
| 2.3 | Modern Slider style | `Themes/Styles/SliderStyle.xaml` |
| 2.4 | Glass Input styles (ComboBox, TextBox, NumberBox) | `Themes/Styles/InputStyles.xaml` |
| 2.5 | Register all style dictionaries in MergedDictionaries | `SharedResources.xaml` or `App.xaml` |
| 2.6 | Add branding box to SettingsWindow NavigationView header | `SettingsWindow.xaml` |
| ✓ | **Verify**: Controls show new styles, respond to all three themes | |

### Phase 3: Settings Visual Refresh
Apply glassmorphic surfaces and spacing to all 9 settings pages. No navigation restructuring.

| Task | Description | Files |
|------|-------------|-------|
| 3.1 | SettingsWindow chrome: replace inline color overrides with App*Brush | `SettingsWindow.xaml` |
| 3.2 | Apply consistent card/section styling to all 9 settings pages | All 9 pages in `Views/Settings/` |
| 3.3 | Add Theme Selector to GeneralSettingsPage (3 visual buttons with swatches) | `GeneralSettingsPage.xaml`, `GeneralSettingsViewModel.cs` |
| 3.4 | Restyle UserPaneFooter | `UserPaneFooter.xaml` |
| ✓ | **Verify**: Settings match mockups in all 3 themes, localization works | |

### Phase 4: Control Panel Dashboard Restyling
Apply the glassmorphic theme to the main HUD.

| Task | Description | Files |
|------|-------------|-------|
| 4.1 | Surface & border updates (glassmorphic card styling) | `ControlPanelPage.xaml` |
| 4.2 | Status badges & mode buttons theme adaptation | `ControlPanelPage.xaml` |
| 4.3 | Visual effects theme adaptation (shimmer/glow colors from ThemeService) | `ControlPanelPage.xaml.cs` |
| 4.4 | LoadingWindow & QuickChatWindow theme alignment | `LoadingWindow.xaml`, `QuickChatWindow.xaml` |
| ✓ | **Verify**: Dashboard matches mockup, effects work in all 3 themes | |

### Phase 5: Polish & QA

| Task | Description |
|------|-------------|
| 5.1 | Cross-theme visual QA: test every page in Midnight, Ember, Frost |
| 5.2 | Contrast ratio verification for accessibility |
| 5.3 | Theme switching mid-operation (during dictation, in settings) |
| 5.4 | Performance: theme switch < 100ms, effects maintain 30fps |
| 5.5 | Publish size impact (fonts + palettes should add < 500KB) |
| 5.6 | Full test suite: `dotnet test DiktaMe.sln` |
| 5.7 | Localization regression (WinUI3Localizer + l:Uids.Uid) |

> **Phases 4–5 Status:** ✅ COMPLETE — Glassmorphic cards, shimmer/glow, edge overlays, status badges all implemented in ControlPanelPage.xaml + code-behind. LoadingWindow and QuickChatWindow themed. Cross-theme QA done, 1010+ tests passing.

### Phase 6: Glassmorphic Settings Window Overhaul

This phase implements the true glassmorphic aesthetic based on expert feedback. We will use a `LinearGradientBrush` on the root Grid and layered semi-transparent `SolidColorBrush` panels. No native Acrylic/Mica is needed, perfectly preserving our `ThemeService` architecture while keeping the ControlPanel opaque and untouched.

#### Implementation Tasks:

**6.1 ThemePalette & Brush Definitions**
| Task | Description | Files |
|------|-------------|-------|
| 6.1.1 | Add 7 new fields to `ThemePalette` record: `GradientStart`, `GradientMid`, `GradientEnd`, `GlowBase`, `BackgroundTranslucent`, `SurfaceTranslucent`, `Surface2Translucent`. | `Services/ThemeService.cs` |
| 6.1.2 | Map 3 new brush keys to translucent accessors in `BrushKeys`. | `Services/ThemeService.cs` |
| 6.1.3 | Add translucent brush definitions (e.g., `AppBackgroundTranslucentBrush` `#D00A0918`) for fallback. | `Themes/SharedResources.xaml` |

**6.2 SettingsWindow Gradient & Translucency**
| Task | Description | Files |
|------|-------------|-------|
| 6.2.1 | Add 3-stop `LinearGradientBrush` as root Grid background. | `Views/SettingsWindow.xaml` |
| 6.2.2 | Set `NavigationView` Background to `AppBackgroundTranslucentBrush` and `ContentFrame` to `AppSurface2TranslucentBrush`. | `Views/SettingsWindow.xaml` |
| 6.2.3 | Add `ApplyGradientBackground(ThemePalette)` to wire gradient stops to the palette. | `Views/SettingsWindow.xaml.cs` |
| 6.2.4 | Ensure `ThemeService` updates `NavigationViewDefaultPaneBackground` to use `BackgroundTranslucent`. | `Services/ThemeService.cs` |
| 6.2.5 | Add top-right 500x400 subtle glow `Border` with diagonal gradient. Wire stops via `ApplyGradientBackground()` using `GlowBase` (Midnight: `#5A3D8F`, Ember: `#4A2010`, Frost: `#8090E0`) with ~21% → ~9% → 0% alpha levels. | `Views/SettingsWindow.xaml` |

**6.3 Settings Page Sub-Nav Translucency**
| Task | Description | Files |
|------|-------------|-------|
| 6.3.1 | Update left `Border` Background from `AppSurfaceBrush` to `AppSurfaceTranslucentBrush` across all 6 sub-nav pages (General, AIEngine, Hardware, Workflows, Presets, Snippets). Privacy, Account, and About remain Transparent. | `Views/Settings/*SettingsPage.xaml` |

**6.4 Accent Color Alignment (Cyan Unification)**
| Task | Description | Files |
|------|-------------|-------|
| 6.4.1 | Change `NavigationViewItemBackgroundSelected*` accessors from `p.NavActive` to `p.Accent` (Cyan). | `Services/ThemeService.cs` |
| 6.4.2 | Change `ListViewItemSelectionIndicatorBrush` from `p.NavActive` to `p.Accent`. | `Services/ThemeService.cs` |
| 6.4.3 | Change `ToggleSwitchFillOn*` and `ToggleSwitchStrokeOn*` from `p.NavActive` to `p.Accent`. | `Services/ThemeService.cs` |

*(Integrated by Gemini based on expert feedback)*

> **Phase 6 Status:** ✅ COMPLETE — SettingsWindow.xaml has LinearGradientBrush background (3 stops), accent glow overlay, glassmorphic diagonal sweep (4 GlassStops), translucent NavigationView. Code-behind mutates stops on theme change.

---

## 9. Technical Constraints & Gotchas

1. **No `DynamicResource` in WinUI 3** — Use `StaticResource` for custom palette brushes. Use `ThemeResource` ONLY for WinUI system resource keys that respond to Light/Dark switching.
2. **In-place brush mutation**: Since `StaticResource` bindings resolve at XAML parse time, swapping a dictionary in MergedDictionaries does NOT update already-rendered UI. The ThemeService must mutate each brush's `Color` property in-place on the persistent dictionary.
3. **ThemeService lives in DiktaMe.App** (not Core) — it depends on `Microsoft.UI.Xaml.Application`.
4. **`RequestedTheme` runtime limits**: Some WinUI 3 versions only allow setting `Application.RequestedTheme` at startup. Fallback: set `RequestedTheme` per-Window via `((FrameworkElement)Content).RequestedTheme`.
5. **ControlPanelPage visual effects**: The 30fps animation timer directly mutates brush Color properties. This is compatible with in-place mutation — both ThemeService and the animation loop write to the same `SolidColorBrush.Color`.
6. **Ember accent vs WinUI Warning colors**: Ember's amber `#F59E0B` accent is close to WinUI's Warning InfoBar color. Ensure visual distinction for InfoBar severity states.
7. **Font cascading**: Setting `FontFamily` on root containers cascades to children automatically in WinUI 3. Icon glyphs with explicit `FontFamily="Segoe MDL2 Assets"` are unaffected.

---

## 10. Reference Design Mockups
- [Glassmorphic Dark (Midnight)](C:\Users\gecko\Downloads\GlassmorphicSettings.png)
- [Glassmorphic Warm (Ember)](C:\Users\gecko\Downloads\WarmGlassSettings.png)
- [Glassmorphic Light (Frost)](C:\Users\gecko\Downloads\GlassmorphicSettingsLight.png)
- [Toggle Switches Reference](C:\Users\gecko\Downloads\GlassmorphicSettings-toggles.png)
- [Control Panel Dashboard](C:\Users\gecko\Downloads\GlassmorphismDashboard.png)
- [Voice Engine Panel](C:\Users\gecko\Downloads\VoiceEnginePanel.png)

---
**Status:** ✅ COMPLETE | **Priority:** High | **Target:** V2.1 UI Refresh
