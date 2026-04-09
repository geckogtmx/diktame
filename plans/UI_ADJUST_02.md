# UI_ADJUST_02: Fix Theme Switching — Foreground Brush Mutation Failure

## Status: READY FOR IMPLEMENTATION

## Context

When switching themes (e.g., Midnight → Frost or Frost → Midnight), 8 Foreground-type brushes fail to update. WinUI 3 throws "Attempted to perform an unauthorized operation" when you try to mutate `.Color` on a `SolidColorBrush` that WinUI's rendering pipeline has claimed. This produces:

- **Frost (light)**: white text on light ComboBox dropdown (inherited from previous dark theme)
- **Midnight (dark)**: dark navy text on dark ComboBox dropdown (inherited from previous light theme)

The issue is permanent until the user navigates away and back (which triggers `InjectPageBrushes` on a fresh page scope).

**First mutation always succeeds** (brush is fresh from XAML). Every subsequent switch fails because the brush has been claimed by WinUI rendering.

### Root cause

`ThemeService.MutateBrushes()` uses `.Color = newColor` on existing brush objects. For Background/Border brushes, this works. For Foreground brushes in ThemeDictionaries, WinUI permanently protects them after first rendering use.

Additionally, `resources.TryGetValue(key)` walks into the active ThemeDictionary (confirmed by logs: `flat=true` for ThemeDictionary-only keys). The crash in this "scope 1" lookup aborts the entire key processing — scopes 2, 3, and 4 are never reached for that key.

### Affected keys (8 total)

1. `TextControlForeground` (ThemeService.cs BrushKeys line ~131)
2. `ComboBoxForeground` (line ~143)
3. `ComboBoxDropDownForeground` (line ~146)
4. `ListViewItemForegroundSelected` (line ~152)
5. `NavigationViewItemForeground` (line ~161)
6. `NavigationViewItemForegroundPointerOver` (line ~162)
7. `NavigationViewItemForegroundSelected` (line ~167)
8. `NavigationViewItemForegroundSelectedPointerOver` (line ~168)

## Solution: Pre-replace ThemeDictionary brush objects before RequestedTheme change

Instead of mutating `.Color` on existing brush objects (which WinUI blocks), **replace the entire brush object** in the ThemeDictionary with a new `SolidColorBrush`. Do this BEFORE setting `RequestedTheme`, so when WinUI re-resolves `{ThemeResource}` bindings, it picks up the fresh brush.

### New flow for `ApplyTheme()`

```
BEFORE (current):
  1. Set RequestedTheme on all windows (WinUI re-resolves from ThemeDictionaries)
  2. MutateBrushes — tries .Color mutation on ALL brushes in 4 scopes
     → Foreground brushes crash in scope 1, skipping scopes 2-4

AFTER (proposed):
  1. ReplaceBrushesInThemeDictionaries — replace ALL control brush objects in BOTH
     Default and Light ThemeDictionaries with new SolidColorBrush instances
  2. Set RequestedTheme on all windows (WinUI re-resolves → picks up new brushes)
  3. MutateFlatBrushes — mutate ONLY flat App*Brush resources in-place
     (these are not in ThemeDictionaries, never have the protection issue)
```

### Why this is safe

- **App\*Brush flat resources** (12 keys): continue with in-place `.Color` mutation. These are at the root `Application.Current.Resources` level, not in ThemeDictionaries. They never trigger the "unauthorized operation". XAML elements hold direct references to these brush objects, so in-place mutation is required.
- **Control ThemeDictionary resources** (66+ keys): switch to object replacement. These use `{ThemeResource}` bindings which WinUI re-resolves on `RequestedTheme` change, so replacement works — controls will fetch the new brush object from the dictionary.
- **No color logic changes**: The same `BrushKeys` accessor functions determine colors. Only the mechanism changes (replace vs mutate).
- **InjectControlBrushes/InjectPageBrushes remain**: These are supplementary local-scope overrides that still run in ThemeChanged handlers. They provide a safety net.

## Implementation Tasks

### TASK 1: Restructure ApplyTheme() in ThemeService.cs
**File**: `src/DiktaMe.App/Services/ThemeService.cs`
**Method**: `ApplyTheme()` (currently lines 244-310)

**1a.** Add a new private method `ReplaceBrushesInThemeDictionaries(ResourceDictionary resources, ThemePalette palette)`:
- Iterate ALL entries in `BrushKeys` that do NOT start with `"App"` (control keys only)
- For each: iterate `resources.ThemeDictionaries` → for each `ResourceDictionary`, if it contains the key, replace: `themeDict[key] = new SolidColorBrush(accessor(palette))`
- Same for `resources.MergedDictionaries` → each `.ThemeDictionaries`
- Log a summary: count of replacements
- Each dictionary entry should have its own try-catch (some entries may still fail — log and continue)

**1b.** Modify `ApplyTheme()` to call `ReplaceBrushesInThemeDictionaries` BEFORE setting `RequestedTheme`:
```csharp
public void ApplyTheme(string? themeName)
{
    // ... validate theme name ...

    var resources = Application.Current.Resources;
    Log.Debug("████ THEME APPLYING: ...");

    // NEW — Phase 1: Replace brush objects in ThemeDictionaries
    // Must happen BEFORE RequestedTheme so WinUI picks up new brushes during re-resolve
    var replaced = ReplaceBrushesInThemeDictionaries(resources, palette);
    Log.Debug("████ THEME REPLACED: {Count} brush objects in ThemeDictionaries", replaced);

    // Phase 2: Set RequestedTheme (triggers WinUI {ThemeResource} re-resolve)
    var elementTheme = palette.IsDark ? ElementTheme.Dark : ElementTheme.Light;
    foreach (var window in App.Current.ActiveWindows)
    {
        if (window.Content is FrameworkElement fe)
            fe.RequestedTheme = elementTheme;
    }

    // Phase 3: Mutate flat App*Brush resources in-place
    var (foundFlat, notFound, crashed) = MutateFlatBrushes(resources, palette);
    Log.Debug("████ THEME FLAT SUMMARY: flat={Flat} notFound={NF} crashed={Crashed}", ...);

    // ... SystemAccentColor overrides, _currentTheme, ThemeChanged event ...
}
```

**1c.** Replace `MutateBrushes()` with `MutateFlatBrushes()`:
- Only processes keys that START with `"App"` (the 12 flat resource keys)
- Simple: `resources.TryGetValue(key)` → `brush.Color = accessor(palette)`
- Retains try-catch for safety
- Remove scopes 2, 3, 4 (ThemeDictionaries are handled by replacement now)

**1d.** Delete or rename the old `MutateBrushes()` method. Ensure no other code calls it.

### TASK 2: Update App.xaml ThemeDictionaries (already done in current session)
**File**: `src/DiktaMe.App/App.xaml`

Verify these entries exist in BOTH Default and Light ThemeDictionaries (added during current session):
- `ComboBoxItemBackgroundPointerOver` — subtle hover highlight
- `ComboBoxItemBackgroundSelected` — subtle selected highlight

Also verify `ComboBoxDropDownBackground` XAML values match the `p.Surface` mapping (fully opaque, not translucent).

### TASK 3: Update BrushKeys ComboBoxDropDownBackground mapping (already done in current session)
**File**: `src/DiktaMe.App/Services/ThemeService.cs`

Verify the BrushKeys entry:
```csharp
("ComboBoxDropDownBackground", p => p.Surface),  // was p.Surface2Translucent
```

### TASK 4: Verify InjectControlBrushes includes new ComboBoxItem keys
**File**: `src/DiktaMe.App/Services/ThemeService.cs`

`GetControlBrushValues()` filters by `!key.StartsWith("App")`, so the new `ComboBoxItem*` keys are automatically included. No change needed — just verify.

### TASK 5: Verify all other session changes are intact
These changes from earlier in the session must NOT be reverted:

| File | Change | Purpose |
|------|--------|---------|
| `ThemeService.cs` line ~302-309 | try-catch around `ThemeChanged?.Invoke()` | Prevent crash handler exceptions from killing app |
| `SettingsWindow.xaml.cs` lines ~98-138 | Step-by-step logging + try-catch in ThemeChanged handler, duplicate RequestedTheme removed | Diagnostic logging + crash protection |
| `ControlPanelPage.xaml.cs` line ~451 | try-catch around `LoadThemeColors()` | Crash protection |
| `ControlPanelViewModel.cs` lines ~1306-1330 | try-catch around `OnThemeChanged` body | Crash protection |
| `QuickChatWindow.xaml.cs` lines ~39-50 | try-catch + duplicate RequestedTheme removed | Crash protection |
| `VisionActionWindow.xaml.cs` lines ~65-76 | try-catch + duplicate RequestedTheme removed | Crash protection |
| `GeneralSettingsViewModel.cs` lines ~304-315 | `.ContinueWith()` error handler | Fire-and-forget error reporting |

### TASK 6: Build and test
```bash
dotnet build DiktaMe.sln -c Release    # Must: 0 warnings, 0 errors
dotnet test DiktaMe.sln                # Must: 1134+ tests pass, 0 failures
```

### TASK 7: Manual verification
Build and run the app:
```bash
dotnet clean src/DiktaMe.App/DiktaMe.App.csproj -c Release "-p:Platform=x64"
dotnet build src/DiktaMe.App/DiktaMe.App.csproj -c Release "-p:Platform=x64"
E:\git\diktame\src\DiktaMe.App\bin\x64\Release\net8.0-windows10.0.19041.0\DiktaMe.App.exe
```

Test matrix (each theme switch = open ComboBox dropdown and verify text contrast):

| From | To | Verify |
|------|----|--------|
| Midnight | Frost | Dropdown text is dark navy (#1A1A2E), not white |
| Frost | Midnight | Dropdown text is white (#FFFFFF), not dark |
| Midnight | Ember | Dropdown text is white |
| Ember | Frost | Dropdown text is dark navy |
| Frost | Ember | Dropdown text is white |
| Ember | Midnight | Dropdown text is white |

Also verify:
- Dropdown background is opaque (no bleed-through of content behind)
- Hover highlight is subtle, not a solid bright color
- Nav sidebar text is readable on all themes
- TextBox text is readable on all themes (TextControlForeground)
- No crashes on rapid theme switching (5+ switches in quick succession)

Check logs at `%APPDATA%\DiktaMe\logs\`:
- `crashed=0` in THEME BRUSH SUMMARY (no more mutation failures)
- New `THEME REPLACED` log line showing replacement count
- No `CRASH in ThemeChanged handler` errors

## Success Criteria

1. ✅ `crashed=0` in brush summary logs on ALL theme switches (not just first)
2. ✅ ComboBox dropdown text readable on first load for all 3 themes
3. ✅ ComboBox dropdown background is opaque
4. ✅ ComboBox hover highlight has good contrast
5. ✅ All existing text (nav items, settings pages, control panel) unaffected
6. ✅ No crashes during theme switching (including rapid switching)
7. ✅ 1134+ unit tests pass
8. ✅ Build with 0 warnings, 0 errors
9. ✅ `dotnet format --verify-no-changes` passes

## Fallback Plan

If Option A (pre-replace) doesn't work — e.g., WinUI still throws on dictionary replacement, or controls don't pick up new brushes:

**Fallback: Force page re-navigation after theme switch**

In `SettingsWindow.xaml.cs` ThemeChanged handler, after all brush operations, force the ContentFrame to re-navigate to the current page:

```csharp
// Force re-navigation to apply fresh InjectPageBrushes
if (ContentFrame.Content is FrameworkElement currentPage)
{
    var currentType = currentPage.GetType();
    ContentFrame.Navigate(currentType);
}
```

This triggers `ContentFrame.Navigated` → `InjectPageBrushes()` on a fresh page instance → all ThemeResource keys get correct brushes at local scope.

**Pro**: Guaranteed to work (already proven — navigating away and back fixes the issue)
**Con**: Brief visual flash as page reloads, scroll position reset

## Rollback Plan

If anything goes wrong, revert ONLY the `ThemeService.cs` changes in TASK 1 (the `ApplyTheme` restructure). All other session changes (try-catch handlers, ComboBoxItem keys, opaque dropdown) are independent improvements that should be kept.

```bash
git diff src/DiktaMe.App/Services/ThemeService.cs  # Review what changed
git checkout src/DiktaMe.App/Services/ThemeService.cs  # Revert ThemeService only
```

Then re-apply the per-entry try-catch and ComboBoxItem keys manually (they were correct, just insufficient for the Foreground mutation issue).

## Files Modified

| File | Tasks | Type |
|------|-------|------|
| `src/DiktaMe.App/Services/ThemeService.cs` | 1a, 1b, 1c, 1d, 3, 4 | Core fix |
| `src/DiktaMe.App/App.xaml` | 2 | Verify only (already changed) |
| `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` | 5 | Verify only (already changed) |
| `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs` | 5 | Verify only (already changed) |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | 5 | Verify only (already changed) |
| `src/DiktaMe.App/Views/QuickChatWindow.xaml.cs` | 5 | Verify only (already changed) |
| `src/DiktaMe.App/Views/VisionActionWindow.xaml.cs` | 5 | Verify only (already changed) |
| `src/DiktaMe.App/ViewModels/Settings/GeneralSettingsViewModel.cs` | 5 | Verify only (already changed) |

## First Prompt for New Session

```
I need you to implement UI_ADJUST_02 from `plans/UI_ADJUST_02.md`. Read the full plan first.

Summary: ThemeService.ApplyTheme() needs restructuring. Currently it sets RequestedTheme
then calls MutateBrushes() which tries to mutate .Color on ALL brushes. 8 Foreground
brushes in ThemeDictionaries throw "unauthorized operation" after first use because WinUI
protects rendered brushes.

The fix: split into 3 phases:
1. ReplaceBrushesInThemeDictionaries() — replace control brush OBJECTS (not mutate .Color)
   in both Default and Light ThemeDictionaries with new SolidColorBrush instances
2. Set RequestedTheme — WinUI re-resolves {ThemeResource} and picks up new brushes
3. MutateFlatBrushes() — mutate only the 12 App*Brush flat resources in-place

Key files:
- `src/DiktaMe.App/Services/ThemeService.cs` — main changes (ApplyTheme restructure)
- `src/DiktaMe.App/App.xaml` — verify ComboBoxItem keys exist (already added)
- All other files listed in TASK 5 — verify session changes intact, don't modify

Start by reading ThemeService.cs fully, then implement TASK 1a through 1d.
Build and test after. Then manual verification per the test matrix.
```
