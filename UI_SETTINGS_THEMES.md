# Theme System — Full Technical History & Architecture

> **STATUS: v19 — MAJOR PROGRESS. Frost theme fully applying.** (as of March 20, 2026)
> v19 per-key try-catch in `MutateBrushes()` fixed the silent crash. 8 Foreground brush keys
> crash with "unauthorized operation" but are handled gracefully. Ancestor-scope injection
> (`InjectControlBrushes` + `InjectPageBrushes`) covers those keys. Frost backgrounds, controls,
> toggles, ComboBoxes all rendering correctly. Remaining: nav selected text color polish.

> **Purpose**: Prevent future AI models and developers from repeating the same failed approaches.
> This document covers the full story of the theme system, what was tried, what failed, why,
> and how it works now.

---

## Architecture Overview

### How Themes Work

1. **ThemePalette** (immutable record in `ThemeService.cs`) — defines ALL colors for one theme (19 color properties + `IsDark` flag)
2. **ThemeService.ApplyTheme()** — mutates `SolidColorBrush.Color` in-place on existing brush objects in `Application.Current.Resources`
3. **StaticResource bindings** — XAML elements hold direct references to brush objects. When `.Color` changes, rendering updates immediately.
4. **RequestedTheme** — set to `Dark`/`Light` on all windows so WinUI built-in controls render correctly.

### File Locations

| File | Purpose |
|------|---------|
| `src/DiktaMe.App/Services/ThemeService.cs` | **Single source of truth** — palettes, brush key mappings, mutation logic |
| `src/DiktaMe.App/Themes/SharedResources.xaml` | App\*Brush placeholder brushes (mutation targets, initial values = Midnight) |
| `src/DiktaMe.App/App.xaml` | ThemeDictionaries — WinUI control ThemeResource overrides (Default + Light) |
| `src/DiktaMe.App/Views/SettingsWindow.xaml` | Gradient/glow/glass overlay Borders (named GradientStops set by code-behind) |
| `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` | Code-behind for gradient, glow, glass, and nav item color updates |
| `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs` | Similar gradient/glass code-behind for Control Panel |

### Theme Layers in SettingsWindow (Z-order, bottom to top)

```
1. RootGrid.Background      — LinearGradientBrush (BgGradStart/Mid/End) → set by ApplyGradientBackground()
2. NavigationView            — Background = {StaticResource AppBackgroundTranslucentBrush}
   ├─ Pane                   — background from {ThemeResource NavigationViewDefaultPaneBackground}
   └─ ContentFrame           — Background = {StaticResource AppSurface2TranslucentBrush}
3. Glow overlay Border       — LinearGradientBrush (GlowStop1/2/3) → set by ApplyGradientBackground()
4. Glass overlay Border      — LinearGradientBrush (GlassStop1/2/3/4) → set by ApplyGlassmorphicGradient()
```

---

## The Root Cause Bug (3 Days of Pain)

### Symptom
Switching theme from Midnight to Frost/Ember: backgrounds stayed Midnight purple. Some control colors changed (toggles, text), but App\*Brush backgrounds never updated.

### Root Cause: MergedDictionary Flat Resource Mutation Gap

`ThemeService.ApplyTheme()` had a 3-step brush search:

```
Step 1: resources.TryGetValue(key)              → root flat resources
Step 2: resources.ThemeDictionaries              → App.xaml ThemeDictionaries (Default + Light)
Step 3: merged.ThemeDictionaries                 → MergedDictionaries' ThemeDictionaries (none exist)
```

**MISSING: Step 4 — MergedDictionaries' flat resources.**

All `App*Brush` keys (AppBackgroundBrush, AppBackgroundTranslucentBrush, AppSurface2TranslucentBrush, etc.) live in `SharedResources.xaml` — which is loaded as a MergedDictionary in App.xaml.

**WinUI 3's `ResourceDictionary.TryGetValue()` on the root dictionary does NOT reliably delegate into MergedDictionaries.** Step 1 silently returned `false` for these keys. The brushes were never mutated.

### Why Midnight Worked (By Accident)
SharedResources.xaml initial values ARE Midnight palette colors. The mutation fails but values are already correct. So Midnight always looked fine.

### Why Frost/Ember Failed
Mutation fails → brush Colors stay at Midnight initial values → backgrounds remain dark purple. Only `RequestedTheme = Light` takes effect, giving partial light-mode appearance for built-in WinUI controls.

### Fix Attempt 1: Step 4 — MergedDictionary flat search (FAILED)
Added Step 4 to ApplyTheme():

```csharp
// 4. Search MergedDictionaries' flat resources
foreach (var merged in resources.MergedDictionaries)
{
    if (merged.TryGetValue(key, out var mFlatObj) && mFlatObj is SolidColorBrush mFlatBrush)
    {
        mFlatBrush.Color = accessor(palette);
        found = true;
    }
}
```

**Status**: Code is in ThemeService.cs. Build v14 confirmed running via About page banner. Result: Frost still shows purple backgrounds. The brush objects found by `merged.TryGetValue()` are likely NOT the same instances that XAML `{StaticResource}` bindings hold. See Failed Approach #8 for full analysis.

### Fix Attempt 2 (v15): Move brushes to App.xaml root — PARTIAL SUCCESS
Moved the 12 `App*Brush` definitions from `SharedResources.xaml` to `App.xaml`'s root flat `ResourceDictionary`.
**Result**: Frost backgrounds are NOW LIGHT. Cards, content area, nav pane backgrounds all changed from Midnight purple to light white/grey. The core brush mutation IS working.
**Remaining issues**:
1. Main nav text still white (should be dark `#1A1A2E`) — nav item foreground ThemeResource keys in Default dictionary still have white values
2. Sub-nav text light instead of dark
3. Toggle switches / ComboBoxes / TextBoxes — styling from Default ThemeDictionary (dark theme values) bleeding through because `RequestedTheme=Light` should pull from Light ThemeDictionary, but step 2 of ApplyTheme() mutates BOTH Default and Light dictionaries with the same Frost palette values. The issue is that the ThemeDictionary brush objects may also have identity problems (same as the App\*Brush MergedDictionary issue was).
4. Slight purple/blue tint at top of nav pane

---

## Build Gotcha: Incremental Builds May Skip Recompilation

**Critical**: `dotnet build` (incremental) may silently skip recompilation of changed .cs files.
Always use `dotnet clean` before `dotnet build` when testing theme changes:

```powershell
dotnet clean src/DiktaMe.App/DiktaMe.App.csproj -c Release "-p:Platform=x64"
dotnet build src/DiktaMe.App/DiktaMe.App.csproj -c Release "-p:Platform=x64"
```

Also ensure the app is fully closed (check system tray — tray icon keeps the process alive).

---

## Diagnostic Logging (Temporary — Remove After Fix Verified)

`ApplyTheme()` in ThemeService.cs logs:
- `████ THEME APPLYING: {Theme} (IsDark={IsDark}) MergedDicts={Count}` — on every call
- Per-brush: `{Key} → #{ARGB} [flat={F} theme={T} mTheme={MT} mFlat={MF}]` — at Debug level
- `████ THEME BRUSH SUMMARY: flat=X themeDicts=Y mergedTheme=Z mergedFlat=W notFound=N total=T`

SettingsWindow title bar includes `[v14]` to confirm which binary is running.

Log file: `%APPDATA%\DiktaMe\logs\diktame_YYYYMMDD.log`

---

## Failed Approaches (DO NOT REPEAT)

### 1. Adjusting palette opacity values
**What was tried**: Changing translucent brush alpha values (e.g., making Frost's BackgroundTranslucent opaque at 0xFF).
**Why it failed**: The brushes were never being mutated in the first place. Changing palette values had zero effect because `ApplyTheme()` couldn't find the App\*Brush objects to mutate.

### 2. Adjusting glow/glass overlay alphas
**What was tried**: Setting glow overlay alpha to 0 for light themes, setting glass overlay to transparent.
**Why it failed**: These fixes ARE correct (glow was adding a blue wash). But they were invisible because the underlying backgrounds were still Midnight purple from unmutated brushes.

### 3. Changing Ember gradient/glow colors to brighter pink
**What was tried**: Making GradientEnd, GlowBase, GlassColor1 more vivid.
**Why it failed**: Same reason — the background layers (AppBackgroundTranslucentBrush, AppSurface2TranslucentBrush) stayed at Midnight values, masking the gradient changes.

### 4. Moving brushes between XAML files
**What was tried**: Various reorganizations of where brushes are defined.
**Why it failed**: The location of brushes matters for `TryGetValue` resolution. Moving them between MergedDictionaries doesn't help if the search doesn't cover MergedDictionaries.

### 5. ThemeDictionaries in MergedDictionary XAML files
**What was tried**: Placing `ResourceDictionary.ThemeDictionaries` inside SharedResources.xaml.
**Why it failed**: WinUI 3 XAML compiler crashes silently (exit code 1) when `ThemeDictionaries` are inside a file loaded via `<ResourceDictionary Source="..."/>`.

### 6. Fixing glow alpha + Ember palette + glass XAML without fixing brush mutation (Steps 11-13)
**What was tried**: Setting glow overlay alpha to 0 for light themes, brightening Ember palette colors, changing glass XAML initial values to Transparent.
**Why it failed**: All three changes were CORRECT but had ZERO visible effect. The underlying App\*Brush backgrounds (NavView, ContentFrame, cards) were never being mutated by ThemeService, so backgrounds stayed Midnight purple regardless of overlay/glow changes. Root cause (Step 14: MergedDictionary flat resource search) was added but incremental build did not recompile. Verification still pending.

### 7. Trusting `dotnet build` incremental compilation
**What was tried**: Running `dotnet build` without `dotnet clean` and expecting changed .cs files to be recompiled.
**Why it failed**: The incremental build silently used stale .obj/.dll files. Binary search of the compiled DLL confirmed the diagnostic string was NOT present until a clean build was performed. This wasted multiple test cycles where the user rebuilt and re-ran but always got the old binary.

### 8. Step 14 — MergedDictionary flat resource search (v14 build, verified running)
**What was tried**: Added a 4th search step to `ApplyTheme()` that explicitly iterates `resources.MergedDictionaries` and calls `merged.TryGetValue(key)` to find and mutate `App*Brush` objects in `SharedResources.xaml`.
**Build confirmed**: Red "BUILD v14 — THEME DIAG ACTIVE" banner visible on About page. `dotnet clean` + `dotnet build` used. The new binary IS running — this is NOT a stale build issue.
**What happened**: Frost theme shows partial change (lighter than Midnight, dark text from `RequestedTheme=Light`) but backgrounds are still purple/lavender instead of clean white. The diagonal purple gradient from the root grid bleeds through everything. Compared to the Frost design mock (`E:\dIKtame\Themes\Frost-GlassmorphicSettingsLight.png`), the result is not even close.
**Likely cause — brush object identity mismatch**: When XAML parses `{StaticResource AppBackgroundTranslucentBrush}` in `SettingsWindow.xaml`, it resolves through WinUI's internal resource lookup and grabs a reference to a brush object. When `ThemeService.ApplyTheme()` calls `merged.TryGetValue("AppBackgroundTranslucentBrush")` on the same MergedDictionary, it may get the same object OR a different cached copy. If the XAML element holds brush object A but ThemeService mutates brush object B, the UI never updates.
**Evidence**: The `NavigationView.Background = {StaticResource AppBackgroundTranslucentBrush}` should be `#FFFAFBFF` (near-white, fully opaque) for Frost. If this brush were actually mutated, the gradient behind it would be completely invisible. But the gradient IS visible through the nav pane — proving the brush the NavigationView holds was NOT mutated to the opaque Frost value.
**Conclusion**: Mutating brushes found via `TryGetValue()` (any step) is unreliable because there's no guarantee the returned object is the same instance that XAML elements hold references to. The approach of searching for brushes to mutate is fundamentally fragile in WinUI 3.

### Fix (v15, APPLIED — PARTIAL SUCCESS): Move App\*Brush to App.xaml root
Moved the 12 `App*Brush` `<SolidColorBrush>` definitions from `SharedResources.xaml` (a MergedDictionary) to `App.xaml`'s root `<ResourceDictionary>` (flat level, alongside the existing ThemeDictionaries block). This ensures:
- `resources.TryGetValue()` in step 1 finds them directly (no MergedDictionary delegation needed)
- XAML `{StaticResource}` resolution finds the same objects at the root level
- No ambiguity about which brush instance is held by XAML elements vs. found by ThemeService
- SharedResources.xaml retains non-brush resources (fonts, converters, styles, CornerRadius)

**Result**: Backgrounds now correctly light on Frost. But ThemeDictionary controls (nav text, toggles, comboboxes) still wrong.

### Diagnostic Log Analysis (v15)

Log at `%APPDATA%\DiktaMe\logs\diktame_20260320.log` shows for Frost application at 13:12:19:
```
AppBackgroundBrush         → #FFFAFBFF [flat=true theme=false mTheme=false mFlat=false]  ← correct
AppTextBrush               → #FF1A1A2E [flat=true theme=false mTheme=false mFlat=false]  ← correct
SliderTrackValueFill       → #FF0EA5E9 [flat=true theme=true mTheme=true mFlat=true]     ← correct
NavigationViewItemForeground → implicit (via ThemeDictionaries)                          ← mutated but WinUI re-resolves
```

ALL 76 brushes found and mutated (`notFound=0`). Colors are correct Frost values. The mutations succeed but `RequestedTheme=Light` (set AFTER mutations at line 349) causes WinUI to re-resolve `{ThemeResource}` to fresh brush instances from the Light dictionary, discarding the mutations done in step 2.

### 9. v16 — Reorder RequestedTheme before mutations (FAILED)
**What was tried**: Moved `RequestedTheme` assignment from AFTER the brush mutation loop (old line 348) to BEFORE it (new line 238), so WinUI re-resolves `{ThemeResource}` to Light dictionary brush instances first, then mutations hit those final instances.
**Build confirmed**: Window title shows `[v16]`, About page shows `BUILD v16`. Clean build verified.
**Result**: ZERO visible improvement over v15. Screenshots are pixel-identical. Light backgrounds still work (App\*Brush via root flat resources = fine). Main nav text still invisible (white-on-white), toggles still dark-themed, comboboxes still dark, purple tint at top of nav pane.
**Conclusion**: The ordering of `RequestedTheme` relative to brush mutations does NOT matter. The fundamental problem is that **ThemeDictionary brush instances found via `ThemeDictionaries["Default"].TryGetValue()` are NOT the same instances that WinUI controls internally use** after `{ThemeResource}` resolution. This is the exact same object-identity mismatch that affected MergedDictionary brushes (Failed Approach #8), now confirmed for ThemeDictionary brushes too. Mutating `.Color` on a brush found via dictionary lookup does not affect the brush the control is actually rendering with.

---

## Critical WinUI 3 Theme Gotchas

1. **`ResourceDictionary.TryGetValue()` may not search MergedDictionaries** — Always explicitly iterate MergedDictionaries when looking for brushes programmatically.

2. **`ThemeDictionaries` in MergedDictionary files = crash** — Place ThemeDictionaries only in App.xaml's root ResourceDictionary.

3. **`ThemeResource` vs `StaticResource`** — ThemeResource re-resolves when RequestedTheme changes. StaticResource resolves once. For live theme switching via Color mutation, StaticResource is correct (holds reference to the brush object we mutate).

4. **`RequestedTheme` timing is IRRELEVANT for brush mutation** — Tested both orderings (v15: mutations first, v16: `RequestedTheme` first). Neither works for ThemeDictionary brushes. The reason: `{ThemeResource}` resolved brush instances and `ThemeDictionaries["Default"].TryGetValue()` returned instances are NOT the same objects. Mutation via dictionary lookup never reaches the brush the control is rendering.

5. **Implicit styles crash complex controls** — Setting Foreground/Background on implicit Slider/ToggleSwitch styles causes exit 127. Use ThemeResource key overrides instead.

6. **NavigationViewItemForeground\* keys** — All states must use the same color when using runtime brush mutation. Per-state differentiation requires per-item local brush injection (see SettingsWindow.xaml.cs `InjectNavItemBrushes`).

---

## How to Add a New Theme

1. Add a `ThemePalette` entry to the `Palettes` dictionary in `ThemeService.cs`
2. That's it. Everything else is automatic:
   - `AvailableThemes` is derived from `Palettes.Keys`
   - Theme ComboBox in GeneralSettingsPage picks it up automatically
   - All brush mutations use the palette's color accessors
   - Glass/glow overlays compute from `IsDark` + palette colors
   - App.xaml ThemeDictionaries have both Default/Light entries — `RequestedTheme` is set based on `IsDark`

### ThemePalette Properties (19 colors + 1 flag)

| Property | Purpose |
|----------|---------|
| GradientStart/Mid/End | SettingsWindow root grid background gradient |
| GlowBase | Atmospheric glow overlay color (alpha controlled by IsDark) |
| GlassColor1/2 | Glassmorphic diagonal sweep colors |
| BackgroundTranslucent | Nav pane background (AppBackgroundTranslucentBrush) |
| SurfaceTranslucent | Surface overlay (AppSurfaceTranslucentBrush) |
| Surface2Translucent | Content area/card background (AppSurface2TranslucentBrush) |
| Background | Solid background (AppBackgroundBrush) |
| Surface | Solid surface (AppSurfaceBrush) |
| Surface2 | Solid surface 2 (AppSurface2Brush) |
| Border | Border color (AppBorderBrush) |
| NavActive | Selected nav item background |
| Accent | Accent color for sliders, toggles, links |
| Text | Primary text color |
| TextDim | Secondary/muted text color |
| PerfGreen | Performance stats green color |
| IsDark | Controls RequestedTheme (Dark/Light) + glow/glass alpha |

### Tips for Light Themes
- Set all `*Translucent` alpha to `0xFF` (fully opaque) — translucency on light backgrounds creates muddy appearance
- Set `IsDark = false` — this zeroes glow/glass overlays and sets `RequestedTheme = Light`
- Use visible `Border` color (e.g., `#E2E5EB`) — on dark themes `#14FFFFFF` works, on light themes it's invisible
- ComboBox/TextBox BrushKeys have `IsDark` conditionals for appropriate light/dark styling

---

## Current Themes (as of March 2026)

- **Midnight** (default, dark) — Deep blue/purple, cyan accent, white text
- **Ember** (dark) — Charcoal base with pink/magenta gradient bloom, amber accent
- **Frost** (light) — Clean near-white, sky blue accent, dark text
- **Emerald** (future) — Design mock exists at `E:\dIKtame\Themes\Emerald-GlassSettings.png`

---

## Progress Log

### v15 (March 20, 2026) — PARTIAL SUCCESS
- **Fixed**: App\*Brush backgrounds — moved 12 brush definitions from SharedResources.xaml to App.xaml root flat resources
- **Confirmed**: Frost backgrounds are now light (white/grey). Cards, content area, nav pane backgrounds all correct.
- **Confirmed via logs**: ALL 76 brush mutations succeed with correct colors, `notFound=0`
- **Still broken**: Nav text white (should be dark), toggle/combobox/textbox controls pulling wrong theme styling
- **Root cause of remaining issues**: `RequestedTheme` set AFTER brush mutations (line 349) — WinUI re-resolves `{ThemeResource}` to fresh brush instances after the theme switch, discarding step 2 mutations

### v16 (March 20, 2026) — FAILED
- **Fix attempted**: Moved `RequestedTheme` assignment to BEFORE the brush mutation loop in `ThemeService.ApplyTheme()`
- **File changed**: `ThemeService.cs` — moved lines 348-356 to right after palette lookup (before line 244)
- **Rationale**: WinUI needs to re-resolve to Light dictionary brush instances first, then mutations hit the correct final instances
- **Result**: ZERO visible improvement over v15. Screenshots pixel-identical. RequestedTheme ordering is irrelevant.
- **Root cause confirmed**: ThemeDictionary brush instances from `TryGetValue()` ≠ brush instances controls use. Same object-identity bug as Failed Approach #8 but for ThemeDictionaries instead of MergedDictionaries.

### Design mocks (target appearance)
- Frost: `E:\dIKtame\Themes\Frost-GlassmorphicSettingsLight.png`
- Ember: `E:\dIKtame\Themes\Ember-GlassmorphicSettings.png`
- Midnight: `E:\dIKtame\Themes\GlassmorphicSettings.png`
- Emerald: `E:\dIKtame\Themes\Emerald-GlassSettings.png`

### Temporary diagnostics (v16) — REMOVED in v17
- `AboutPage.xaml` — red "BUILD v16" banner → removed
- `SettingsWindow.xaml.cs` — `[v16]` in window title → removed
- `ThemeService.cs` — `████ THEME` diagnostic logging → downgraded from Information to Debug

---

## v17 (March 20, 2026) — COMPLETE STRATEGY PIVOT

### The Breakthrough Insight
After 9 failed approaches trying to make ThemeDictionary brush mutation work, we identified that
**one pattern in the codebase DOES reliably control WinUI control colors**: `InjectNavItemBrushes()`.

This pattern works because it injects brushes into `element.Resources["key"]` — the element's local
Resources dictionary. WinUI's `{ThemeResource}` resolution walks UP the visual tree and checks each
ancestor's Resources BEFORE checking `Application.Resources.ThemeDictionaries`. Local/ancestor-scope
brushes take priority over ThemeDictionaries.

### Strategy E: Ancestor-Scope Injection (THE FIX)
Instead of trying to mutate ThemeDictionary brush instances (which are different objects than what controls
hold internally), inject ALL control ThemeResource brushes into `NavView.Resources`. The NavigationView
is the visual ancestor of nav items AND the ContentFrame (which hosts all settings pages). Every control
walks up and finds these brushes at the NavView scope before reaching the broken ThemeDictionaries.

### What Changed

**`ThemeService.cs`**:
- Added `GetControlBrushValues(ThemePalette)` — public static method that yields all non-App\*Brush
  key→color pairs from BrushKeys. Avoids duplicating the 60+ brush key mappings in window code-behind.
- Downgraded `████ THEME` diagnostic logs from `Information` to `Debug`.
- ThemeDictionary mutation (steps 2-3) KEPT as fallback — costs nothing, may help future windows.

**`SettingsWindow.xaml.cs`**:
- Added `InjectControlBrushes(ThemePalette)` — iterates `ThemeService.GetControlBrushValues()` and
  sets `NavView.Resources[key] = new SolidColorBrush(color)` for each. Called on init + theme change.
- **Fixed nav text bug**: Changed `var dark = palette.Background` → `var contrast = palette.Text` in
  both `CreateBrushSet()` and `ApplyNavItemColors()`. The old code used the page background color
  (#FAFBFF for Frost = near-white) as the "dark" selected-item text, making it invisible on sky-blue.
  `palette.Text` gives correct contrast: white for dark themes, dark navy for light themes.
- Removed `[v16]` from window title.

**`AboutPage.xaml`**:
- Removed red "BUILD v16 — THEME DIAG ACTIVE" diagnostic banner.

### Why This Works (vs. Failed Approaches)
1. **No brush identity issue**: We create NEW brush instances and inject them into an ancestor's
   Resources dict. WinUI controls walk up the tree and find them FIRST.
2. **Proven pattern**: Same mechanism as `InjectNavItemBrushes` (which has always worked), just at
   a higher scope (NavigationView instead of individual NavigationViewItem).
3. **Single injection point**: NavigationView is ancestor of ALL settings UI — nav items, ContentFrame,
   every settings page, every ToggleSwitch/ComboBox/Slider/TextBox within them.
4. **No re-templating**: Default WinUI ControlTemplates use `{ThemeResource}` internally — they just
   resolve from a closer scope now.

### Failed Approaches Archive (DO NOT REPEAT)

| # | Strategy | Why It Failed |
|---|----------|--------------|
| 1 | Adjusting palette opacity | Brushes were never mutated (MergedDictionary gap) |
| 2 | Adjusting glow/glass alphas | Correct fix but invisible under unmutated backgrounds |
| 3 | Changing Ember gradient colors | Same as #2 — backgrounds masked gradient changes |
| 4 | Moving brushes between XAML files | TryGetValue resolution doesn't cover MergedDictionaries |
| 5 | ThemeDictionaries in MergedDictionary XAML | Crashes WinUI XAML compiler (exit code 1) |
| 6 | Fixing overlays without fixing mutation | All correct but zero visual effect — root cause unfixed |
| 7 | Trusting incremental builds | Stale binary — dotnet build skipped recompilation silently |
| 8 | MergedDictionary flat search (Step 4) | Brush identity mismatch — XAML holds different instances |
| 9 | Reorder RequestedTheme before mutations | Same identity mismatch applies to ThemeDictionary brushes |
| 10 | v17 NavView.Resources injection only | Works for nav pane controls (ToggleSwitch) but NOT for ComboBox/TextBox inside ContentFrame pages — Frame creates scope boundary |
| 11 | v17-fix Page.Resources injection via Navigated | Page-level injection also does NOT reach ComboBox/TextBox ControlTemplate {ThemeResource} keys. Nav text still white (palette init timing + brush mutation vs re-creation issue) |
| 12 | v18 RequestedTheme on SettingsWindow | Correct fix but irrelevant — `ApplyTheme("Frost")` CRASHES before completing, `ThemeChanged` never fires, SettingsWindow never gets Frost notification |

### v17 Test Results (March 20, 2026)
- **Midnight REGRESSION**: Nav text changed from dark-on-blue to white-on-blue — caused by changing
  `palette.Background` → `palette.Text` unconditionally. For Midnight, `palette.Text` = white.
- **Frost partially fixed**: ToggleSwitches responded (blue fills), but ComboBoxes and TextBoxes
  still rendered with dark backgrounds/text. Nav text still wrong.
- **Root cause**: `NavView.Resources` injection works for controls IN the nav pane (ToggleSwitches),
  but NOT for controls inside `ContentFrame` pages. Frame creates a scope boundary — controls in
  pages resolve `{ThemeResource}` from their page scope, not NavView.

### v17-fix (March 20, 2026) — Page-Level Injection + IsDark Conditional

**Constraint**: Midnight is LOCKED — must NOT change from v16 appearance.

**Changes to `SettingsWindow.xaml.cs`**:
1. **Reverted nav text to IsDark conditional**: `var selectedFg = palette.IsDark ? palette.Background : palette.Text`
   - Dark themes (Midnight, Ember): uses `palette.Background` = dark color on blue selection bg (v16 behavior)
   - Light themes (Frost): uses `palette.Text` = dark navy (#1A1A2E) on sky blue selection bg
   - Applied in both `CreateBrushSet()` and `ApplyNavItemColors()`

2. **Added `ContentFrame.Navigated` handler**: Injects ALL control brushes into each page's Resources
   as it navigates into the ContentFrame. `Page.Resources` is checked by controls' `{ThemeResource}`
   resolution BEFORE walking up to NavView or Application.Resources.
   ```csharp
   ContentFrame.Navigated += (_, args) =>
   {
       if (args.Content is FrameworkElement page)
       {
           var pal = ThemeService.GetPalette(_themeService.CurrentTheme);
           InjectPageBrushes(page, pal);
       }
   };
   ```

3. **Re-inject on theme change**: In the `ThemeChanged` handler, after `InjectControlBrushes(palette)`,
   also injects into the currently displayed page:
   ```csharp
   if (ContentFrame.Content is FrameworkElement page)
   {
       InjectPageBrushes(page, palette);
   }
   ```

4. **Re-added `[v17]` to window title** for build verification.

**Expected result**: Midnight identical to v16. Frost: nav text dark navy, ComboBoxes/TextBoxes with
light backgrounds and dark text, ToggleSwitches with blue fills.

**ACTUAL RESULT — FAILED** (Screenshots: `dIKta.me — Settings [v17] 3_20_2026 2_34_49 PM.png`):
- Midnight: appears unchanged (LOCKED constraint satisfied)
- Frost nav text: STILL white/invisible. The `IsDark ? palette.Background : palette.Text` conditional
  should give `palette.Text` = #1A1A2E for Frost, but nav items still show white text.
  Likely cause: constructor runs with Midnight as default theme (settings load happens after).
  `InjectNavItemBrushes()` at line 102 uses `initPalette` which may be Midnight if the theme
  hasn't switched yet. The `ThemeChanged` handler calls `ApplyNavItemColors()` but that only
  mutates `.Color` on existing brushes — it does NOT re-inject new brushes into `navItem.Resources`.
- Frost ComboBoxes/TextBoxes: STILL dark backgrounds with white text. Page-level injection via
  `ContentFrame.Navigated` did NOT fix them either.
- Frost ToggleSwitches: Still have correct blue fills (NavView.Resources injection works for these).

### Failed Approach #11: Page.Resources injection via ContentFrame.Navigated
Page-level brush injection does not reach ComboBox/TextBox built-in ControlTemplate `{ThemeResource}` keys.
Nav text still white — wrong palette at init time, and `ApplyNavItemColors()` only mutates `.Color`
on existing brush objects (which may be the Midnight brush set, not re-created for Frost).

### Next Steps to Investigate
1. **Nav text**: `InjectNavItemBrushes()` runs at constructor time with `initPalette`. If app starts
   on Midnight and user switches to Frost, `ThemeChanged` calls `ApplyNavItemColors()` which mutates
   `.Color` but the brush instances in `navItem.Resources` were created with Midnight colors. Need to
   verify: does mutating `.Color` on a brush that was injected into `navItem.Resources` actually
   cause WinUI to re-resolve the `{ThemeResource}` key? Or does WinUI cache the initial brush
   reference and ignore subsequent Color changes on locally-injected brushes?
2. **ComboBox/TextBox**: Neither NavView.Resources nor Page.Resources injection works. The built-in
   ControlTemplates may resolve `{ThemeResource}` from `XamlControlsResources` (the WinUI system
   dictionary) which is the LOWEST priority MergedDictionary in App.xaml. Page/NavView scope brushes
   should take priority, but they clearly don't for these controls. May need to investigate the
   actual WinUI source for ComboBox ControlTemplate to see which keys it uses.
3. **Alternative approach**: Instead of trying to make `{ThemeResource}` resolve from injected scopes,
   consider walking the visual tree after page load and setting control properties directly
   (e.g., `comboBox.Background = new SolidColorBrush(color)`). This is brute-force but guaranteed
   to work since it bypasses template resolution entirely.

### v18 (March 20, 2026) — RequestedTheme + Logging — FAILED (but revealed ROOT CAUSE)

**Changes**:
- Set `RequestedTheme = Light/Dark` on SettingsWindow root content at init and on ThemeChanged
- Added diagnostic logging to SettingsWindow (init theme, RequestedTheme, injection counts)

**ACTUAL RESULT — FAILED** but logging revealed the **true root cause**:

**DISCOVERY: `ApplyTheme("Frost")` SILENTLY CRASHES midway through brush mutation loop!**

Log evidence:
```
15:00:51 ████ THEME APPLYING: Midnight ... ← completes, logs BRUSH SUMMARY + "Theme applied"
15:01:04 SettingsWindow: init theme=Midnight IsDark=true  ← SettingsWindow opens with MIDNIGHT
15:01:13 ████ THEME APPLYING: Frost ...
15:01:13   AppBackgroundBrush → #FFFAFBFF  ← flat brushes mutated OK
15:01:13   ...
15:01:13   TextControlBorderBrushFocused → #FF0EA5E9  ← LAST KEY LOGGED
                                                        ← TextControlForeground NEVER LOGGED
                                                        ← NO "THEME BRUSH SUMMARY"
                                                        ← NO "Theme applied: Frost"
                                                        ← NO ThemeChanged event fired
```

The crash happens **between `TextControlBorderBrushFocused` and `TextControlForeground`** in the
BrushKeys mutation loop. The method never completes for Frost, so:
1. `_currentTheme` stays as `"Midnight"` (never set to `"Frost"` at line 361)
2. `ThemeChanged?.Invoke()` at line 364 never fires
3. SettingsWindow's `ThemeChanged` handler never runs
4. `RequestedTheme` stays at Dark, injection never re-runs for Frost
5. **ALL previous approaches (#1-#11) were doomed from the start** — the theme was never fully applied

**Likely crash cause**: `TextControlForeground` has `[flat=true theme=true mTheme=true mFlat=true]`
(found in all 4 locations). After `RequestedTheme = Light` is set (line 243-249), mutating
`.Color` on a brush from the **now-inactive** Default ThemeDictionary may cause a silent native
WinUI crash. Midnight → Midnight works because `RequestedTheme` stays at `Dark` (Default dict
stays active). Frost changes to `Light`, making Default inactive — stale brush mutation = crash.

**Secondary discovery**: VFX system (`ControlPanelPage.ApplyGlowToBackground()`) mutates shared
`AppTextBrush`, `AppBorderBrush` etc. across ALL windows including Settings. When `wholeApp=true`
and dictation is active, Settings text and borders shift colors. This is a separate issue from
the theme crash but explains the user's report of "weird borders and text color changes."

### Failed Approach #12: v18 RequestedTheme on SettingsWindow
Setting `RequestedTheme` on SettingsWindow was correct but insufficient — the theme was never
fully applied because `ApplyTheme("Frost")` crashes midway through. The `ThemeChanged` event
never fires, so SettingsWindow never gets notified and never runs its injection/color update code.

### v19 (March 20, 2026) — MAJOR PROGRESS (try-catch fix)

**The fix**: Extracted brush mutation loop into `MutateBrushes()` static method in `ThemeService.cs`
with per-key try-catch. Individual brush mutations that crash are caught and logged as warnings,
allowing the loop to always complete. `_currentTheme` is always set. `ThemeChanged` always fires.

**8 brush keys that crash with "Attempted to perform an unauthorized operation"**:
1. `TextControlForeground`
2. `ComboBoxForeground`
3. `ComboBoxDropDownForeground`
4. `ListViewItemForegroundSelected`
5. `NavigationViewItemForeground`
6. `NavigationViewItemForegroundPointerOver`
7. `NavigationViewItemForegroundSelected`
8. `NavigationViewItemForegroundSelectedPointerOver`

All are `*Foreground*` keys. These crash when `RequestedTheme` changes from Dark to Light —
WinUI protects brush instances from the now-inactive Default ThemeDictionary against `.Color` mutation.
The ancestor-scope injection (`InjectControlBrushes` into NavView.Resources + `InjectPageBrushes`
into each page) covers these keys with fresh brush instances, so controls still render correctly.

**Log evidence (v19 working)**:
```
15:23:15 ████ THEME BRUSH SUMMARY: flat=57 themeDicts=56 mergedTheme=47 mergedFlat=41 notFound=0 crashed=8 total=76
15:23:15 Theme applied: Midnight (RequestedTheme="Dark")
15:23:16 SettingsWindow: ThemeChanged → Midnight IsDark=true
15:23:24 Theme applied: Frost (RequestedTheme="Light")    ← FIRST TIME EVER COMPLETING
15:23:24 SettingsWindow: ThemeChanged → Frost IsDark=false ← SettingsWindow gets notification
```

**Screenshot confirms** (`dIKta.me — Settings [v19] 3_20_2026 3_21_00 PM.png`):
- Frost backgrounds: fully light/white ✓
- ComboBoxes: light background, dark text, "Frost" value visible ✓
- Toggle switches: blue fills with correct styling ✓
- Settings cards: clean white backgrounds with proper borders ✓
- Sub-nav "Application" tab: correct blue selected state ✓
- Description text: dark and readable ✓

**Remaining issue**: Main nav selected item text is dark navy (#1A1A2E from `palette.Text`) on
sky blue background. User feedback: "White works better on selected."

### Design mocks (target appearance)
- Frost: `E:\dIKtame\Themes\Frost-GlassmorphicSettingsLight.png`
- Ember: `E:\dIKtame\Themes\Ember-GlassmorphicSettings.png`
- Midnight: `E:\dIKtame\Themes\GlassmorphicSettings.png`
- Emerald: `E:\dIKtame\Themes\Emerald-GlassSettings.png`
