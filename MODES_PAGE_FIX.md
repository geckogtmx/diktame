# Modes Page Fix Plan

## Problem Summary

Task J.6 (UI for CRUD Dictation Modes) was implemented incorrectly, causing several critical issues:

### Issues Identified

1. **Lost utility pipeline configuration**
   - The new DictationModesSettingsPage replaced the old ModesSettingsPage entirely
   - Ask, Refine, Translate, Note, Chat configuration became inaccessible
   - User explicitly stated: "Ask, Refine, Translate, Note, Chat lost all their settings that were located in Mopdeas before"

2. **Build failures**
   - DictationModesSettingsViewModel references `_pipelineManager` on lines 240, 355
   - This field doesn't exist in the constructor (was removed during refactoring)
   - CS0103 errors prevent compilation

3. **Duplicate hotkey configuration**
   - Hotkey fields appear in both Hotkeys tab AND Dictation Modes page
   - User stated: "Hotkeyas are already assigned in the Hoykeys section of Settings, the option to configuyre one here is a duplication that needs to be removed"

4. **Settings corruption**
   - settings.json has wrong data structure:
     - `DictationModes`: 1 item with GUID ID instead of 4 built-in modes
     - `UtilityPipelines`: Empty array `[]` instead of 5 items
   - UI shows only "New Mode" instead of Standard/Prompt/Professional/Raw

5. **Empty model dropdown**
   - Logs show "Gemini returned 30 models" but UI displays only "(Default)"
   - Threading/dispatcher issue in LoadModelsAsync

6. **Architectural confusion**
   - Dictation modes (CRUD - user can create/edit/delete custom modes)
   - Utility pipelines (update-only - fixed Ask/Refine/Translate/Note/Chat)
   - These should be separate UI pages, not combined

## V1 Reference Design

From V1 screenshot (diktate repo), the Modes page had:

**Left Sidebar - Two Sections:**
- **DICTATION** (4 built-in modes, user can add custom):
  - Standard (General Purpose)
  - Prompt (For LLM Prompts)
  - Professional (Business)
  - Raw
- **PROCESSING MODES** (5 fixed utility pipelines):
  - Ask (Q&A)
  - Refine
  - Translate
  - Note
  - Chat

**Right Panel:**
- Dual-profile configuration (Local/Cloud toggle)
- System prompt editor (custom prompt in use indicator)
- Save/Reset buttons
- NO hotkey fields (managed separately)

**Top Controls:**
- "Default Mode" dropdown (startup mode selection)
- "Ask Mode Output" dropdown (inject text vs display answer)

## Solution: Separate Pages

### Architecture

V2 should have FOUR separate navigation items in Settings:

1. **"Modes" tab** (existing ModesSettingsPage - SIMPLIFIED)
   - Configures utility pipelines: Ask, Refine, Translate ONLY
   - Update-only (no create/delete)
   - Uses PipelineConfigManager
   - Dual-profile system prompt editing
   - NO hotkey fields

2. **"Dictation Modes" tab** (new DictationModesSettingsPage)
   - Configures dictation modes: Standard, Prompt, Professional, Raw + custom modes
   - Full CRUD (create, update, delete, reorder)
   - Uses DictationModeManager
   - Dual-profile system prompt editing + per-mode model selection
   - NO hotkey fields

3. **"Notes" tab** (NotesSettingsPage - check if exists from V1)
   - Note File Path (text input + Browse button)
   - LLM Processing toggle (clean up transcription into professional note)
   - Timestamp Format (custom format string)
   - Note System Prompt (guides AI in formatting voice notes)
   - Save/Reset buttons
   - Live Preview showing sample note with timestamp
   - ADVANCED (Coming Soon): Default Folder, File Name Template

4. **"Chat" tab** (new ChatSettingsPage - TBD)
   - Chat configuration options
   - System prompt, model selection
   - Chat-specific settings (context window, history, etc.)

## Implementation Steps

### Step 1: Revert SettingsWindow Navigation

**File:** `src/DiktaMe.App/Views/SettingsWindow.xaml.cs`

**Current (broken):**
```csharp
"modes" => typeof(Settings.DictationModesSettingsPage),  // WRONG
```

**Fix:**
```csharp
"modes" => typeof(Settings.ModesSettingsPage),  // REVERT - restore utility pipeline config
```

This immediately restores access to Ask/Refine/Translate/Note/Chat configuration.

### Step 2: Add New Navigation Items

**File:** `src/DiktaMe.App/Views/SettingsWindow.xaml`

Add THREE new NavigationViewItems. Based on V1 structure, Notes was already a separate menu item:

```xaml
<NavigationViewItem Content="Modes" Tag="modes" Icon="ViewAll"/>  <!-- Ask, Refine, Translate only -->
<NavigationViewItem Content="Dictation Modes" Tag="dictationmodes" Icon="Edit"/>  <!-- NEW -->
<!-- Notes already exists in V1 navigation, verify it exists -->
<!-- Chat needs to be added as new item -->
<NavigationViewItem Content="Chat" Tag="chat" Icon="Message"/>  <!-- NEW -->
```

**File:** `src/DiktaMe.App/Views/SettingsWindow.xaml.cs`

Update switch statement to handle all pages:
```csharp
string tag = item.Tag?.ToString() ?? "general";
Type? pageType = tag switch
{
    "general" => typeof(Settings.GeneralSettingsPage),
    "aiengine" => typeof(Settings.AIEngineSettingsPage),
    "modes" => typeof(Settings.ModesSettingsPage),  // Ask, Refine, Translate ONLY
    "dictationmodes" => typeof(Settings.DictationModesSettingsPage),  // NEW - Dictation CRUD
    "notes" => typeof(Settings.NotesSettingsPage),  // Check if this already exists, create if needed
    "chat" => typeof(Settings.ChatSettingsPage),  // NEW - to be created
    "audio" => typeof(Settings.AudioSettingsPage),
    // ... rest unchanged
};
```

**NOTE:**
- ModesSettingsPage needs to be UPDATED to remove Note and Chat from its sidebar list, showing only Ask, Refine, Translate.
- V2 currently does NOT have a "Notes" navigation item (V1 had it). This needs to be created as part of Phase 2.
- Current V2 navigation items: General, Hotkeys, AI Engine, Modes, Audio, Privacy, API Keys, Ollama, Snippets, Control Panel, About

### Step 3: Fix Build Failures - Remove Utility Pipeline Code

**File:** `src/DiktaMe.App/ViewModels/Settings/DictationModesSettingsViewModel.cs`

**Remove these methods entirely:**
- `LoadPipelineDetail()` (lines 238-256)
- `SavePipelineAsync()` (lines 335-357)

**Update `LoadDetail()` to ONLY handle dictation modes:**
```csharp
private void LoadDetail()
{
    if (SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        return;

    var item = ModeItems[SelectedIndex];
    if (item.IsSeparator)
        return;

    Title = item.Title;
    IsBuiltIn = item.IsBuiltIn;
    IsDictationMode = item.IsDictationMode;

    // Only dictation modes - no utility pipelines
    LoadDictationModeDetail(item.Id);
}
```

**Update `SaveAsync()` command:**
```csharp
[RelayCommand]
private async Task SaveAsync()
{
    if (SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        return;

    var item = ModeItems[SelectedIndex];
    if (item.IsSeparator)
        return;

    try
    {
        // Only save dictation modes
        await SaveDictationModeAsync(item.Id).ConfigureAwait(true);

        // Update sidebar title
        item.Title = Title;
        int idx = SelectedIndex;
        LoadModeList();
        SelectedIndex = idx;

        Log.Information("DictationModesSettingsVM: Saved {Id}", item.Id);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "DictationModesSettingsVM: Failed to save {Id}", item.Id);
    }
}
```

### Step 4: Remove Hotkey Fields from UI

**File:** `src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml`

**Delete these XAML sections:**
- Lines 130-137: Cloud profile hotkey TextBox
- Lines 163-168: Local profile hotkey TextBox

Hotkeys are already managed in the Hotkeys tab - no duplication needed.

### Step 5: Remove Hotkey Properties from ViewModel

**File:** `src/DiktaMe.App/ViewModels/Settings/DictationModesSettingsViewModel.cs`

**Remove observable properties:**
```csharp
[ObservableProperty]
private string _cloudHotkey = "";  // DELETE - line 50

[ObservableProperty]
private string _localHotkey = "";  // DELETE - line 64
```

**Update `LoadDictationModeDetail()` - remove hotkey assignment:**
```csharp
private void LoadDictationModeDetail(string modeId)
{
    var mode = _modeManager.GetModeById(modeId);
    if (mode is null)
        return;

    // Cloud profile
    CloudSystemPrompt = mode.CloudProfile.SystemPrompt ?? "";
    CloudUseLlm = mode.CloudProfile.UseLlm;
    // CloudHotkey - REMOVE
    SelectedCloudModelIndex = FindModelIndex(mode.CloudProfile.ModelName);

    // Local profile
    LocalSystemPrompt = mode.LocalProfile.SystemPrompt ?? "";
    LocalUseLlm = mode.LocalProfile.UseLlm;
    // LocalHotkey - REMOVE
}
```

**Update `SaveDictationModeAsync()` - save null for hotkeys:**
```csharp
private async Task SaveDictationModeAsync(string modeId)
{
    string? cloudModel = SelectedCloudModelIndex > 0 && SelectedCloudModelIndex < _cloudModelIds.Count
        ? _cloudModelIds[SelectedCloudModelIndex]
        : null;

    var cloudProfile = new DictationProfile
    {
        SystemPrompt = string.IsNullOrWhiteSpace(CloudSystemPrompt) ? null : CloudSystemPrompt,
        UseLlm = CloudUseLlm,
        ModelName = cloudModel,
        Hotkey = null,  // CHANGE from CloudHotkey to null
    };

    var localProfile = new DictationProfile
    {
        SystemPrompt = string.IsNullOrWhiteSpace(LocalSystemPrompt) ? null : LocalSystemPrompt,
        UseLlm = LocalUseLlm,
        ModelName = null, // Local always uses global Ollama model
        Hotkey = null,  // CHANGE from LocalHotkey to null
    };

    await _modeManager.UpdateModeAsync(modeId, Title, cloudProfile, localProfile).ConfigureAwait(false);
}
```

### Step 6: Fix settings.json Corruption

**Problem:** Current settings.json has:
- `DictationModes`: 1 item with GUID ID instead of 4 built-in modes
- `UtilityPipelines`: Empty array `[]` instead of 5 items

**Solution:** Delete the file to trigger fresh migration.

**Manual steps:**
1. Close the app
2. Delete `C:\Users\gecko\AppData\Roaming\DiktaMe\settings.json`
3. Restart the app
4. LoadingViewModel.InitializeAsync() calls SettingsMigrationService.MigrateAsync()
5. Migration detects empty arrays and populates defaults

**Expected result after migration:**
- `DictationModes`: 4 items with IDs `dictate-standard`, `dictate-prompt`, `dictate-professional`, `dictate-raw`
- `UtilityPipelines`: 5 items with types `ask`, `refine`, `translate`, `note`, `chat`

### Step 7: Fix Model Dropdown Population

**File:** `src/DiktaMe.App/ViewModels/Settings/DictationModesSettingsViewModel.cs`

**Problem:** Current code has race condition - `PopulateModelList()` called on both UI thread and background thread.

**Fix `LoadModelsAsync()`:**
```csharp
private async Task LoadModelsAsync()
{
    IsLoadingModels = true;

    try
    {
        var models = await _modelListService.GetAvailableModelsAsync().ConfigureAwait(false);

        // ALWAYS dispatch to UI thread (remove dual-branch logic)
        if (App.Current.MainWindow?.DispatcherQueue is { } dispatcher)
        {
            dispatcher.TryEnqueue(() =>
            {
                PopulateModelList(models);
                IsLoadingModels = false;  // Set on UI thread
            });
        }
        else
        {
            // Fallback for tests
            PopulateModelList(models);
            IsLoadingModels = false;
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "DictationModesSettingsViewModel: Failed to load models");
        IsLoadingModels = false;
    }
}
```

## Critical Files

### Files to Modify
1. `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` - Revert "modes" to ModesSettingsPage, add "dictationmodes" and "chat" cases
2. `src/DiktaMe.App/Views/SettingsWindow.xaml` - Add new NavigationViewItems (Dictation Modes, Chat), verify Notes exists
3. `src/DiktaMe.App/ViewModels/Settings/DictationModesSettingsViewModel.cs` - Remove utility pipeline code, remove hotkey properties, fix dispatcher
4. `src/DiktaMe.App/Views/Settings/DictationModesSettingsPage.xaml` - Remove hotkey TextBoxes
5. `src/DiktaMe.App/Views/Settings/ModesSettingsPage.xaml` - Update to show ONLY Ask, Refine, Translate (remove Note, Chat from sidebar)
6. `src/DiktaMe.App/ViewModels/Settings/ModesSettingsViewModel.cs` - Update to load ONLY Ask, Refine, Translate pipelines

### Files to Create (Deferred - mark as TODO)
7. `src/DiktaMe.App/Views/Settings/NotesSettingsPage.xaml` - If doesn't exist, create page for Note pipeline config
8. `src/DiktaMe.App/ViewModels/Settings/NotesSettingsViewModel.cs` - ViewModel for Notes page
9. `src/DiktaMe.App/Views/Settings/ChatSettingsPage.xaml` - New page for Chat configuration
10. `src/DiktaMe.App/ViewModels/Settings/ChatSettingsViewModel.cs` - ViewModel for Chat page

### Files to Delete (Manual)
11. `C:\Users\gecko\AppData\Roaming\DiktaMe\settings.json` - Trigger fresh migration

## Verification

### Build Test
```bash
dotnet build DiktaMe.sln -c Debug
# Expect: 0 errors, 0 warnings
dotnet test DiktaMe.sln
# Expect: 521 tests pass
```

### Manual UI Test
1. Delete settings.json, restart app
2. Open Settings → "Modes" tab
   - Shows ONLY Ask, Refine, Translate (Note and Chat removed)
   - Can edit system prompts
   - No dictation modes visible
3. Open Settings → "Dictation Modes" tab
   - Shows Standard, Prompt, Professional, Raw
   - Can create custom modes
   - Model dropdown populated
   - NO hotkey fields
4. Open Settings → "Notes" tab (if exists, otherwise mark as TODO)
   - Note pipeline configuration
   - File path, timestamp format, etc.
5. Open Settings → "Chat" tab (NEW - will be TODO for now)
   - Chat configuration options
6. Model dropdown test:
   - First entry: "(Default — use provider default)"
   - Remaining entries: "ModelName (Provider)"
7. CRUD test:
   - Create new mode → appears in sidebar
   - Edit built-in mode → changes persist
   - Delete custom mode → removed from sidebar
   - Cannot delete built-in modes

## Success Criteria

### Phase 1 (Immediate Fix)
✅ Build succeeds (0 errors)
✅ All 521 tests pass
✅ "Modes" tab shows Ask/Refine/Translate ONLY (Note and Chat removed from sidebar)
✅ "Dictation Modes" tab shows Standard/Prompt/Professional/Raw + custom (new CRUD)
✅ No hotkey duplication in Dictation Modes page
✅ Model dropdown populates correctly
✅ settings.json has 4 DictationModes + 5 UtilityPipelines (migration works)
✅ Built-in modes editable but not deletable
✅ Custom dictation modes fully CRUD-able

### Phase 2 (Deferred - Future Tasks)
⏸️ "Notes" tab exists with Note pipeline configuration (file path, timestamp format, etc.)
⏸️ "Chat" tab exists with Chat configuration options (TBD)
⏸️ Note and Chat removed from UtilityPipelines array in settings.json (become standalone configs)

## Implementation Priority

**Do NOW (Phase 1 - This Task):**
1. Fix build failures in DictationModesSettingsViewModel (remove _pipelineManager references)
2. Revert SettingsWindow navigation "modes" tag back to ModesSettingsPage
3. Add "Dictation Modes" navigation item to SettingsWindow
4. Remove hotkey fields from DictationModesSettingsPage XAML and ViewModel
5. Update ModesSettingsPage to show only Ask/Refine/Translate (remove Note and Chat from sidebar)
6. Fix settings.json corruption (delete file to trigger migration)
7. Fix model dropdown dispatcher issue
8. Build and verify 521 tests pass

**Do LATER (Phase 2 - Separate Future Task):**
1. Create NotesSettingsPage + ViewModel (V1 had this as separate page)
   - File path with Browse button
   - LLM Processing toggle
   - Timestamp format input
   - Note System Prompt editor
   - Live preview
2. Create ChatSettingsPage + ViewModel (new for V2)
   - Chat-specific configuration options (TBD)
3. Add "Notes" and "Chat" navigation items to SettingsWindow
4. Consider migrating Note and Chat out of UtilityPipelines array into standalone config sections
5. Wire up DI registrations for new ViewModels

## Quick Reference: Navigation Structure

### Current V2 (Before Fix)
- General, Hotkeys, AI Engine, **Modes** (broken - shows DictationModesSettingsPage), Audio, Privacy, API Keys, Ollama, Snippets, Control Panel, About

### After Phase 1 Fix
- General, Hotkeys, AI Engine, **Modes** (Ask/Refine/Translate only), **Dictation Modes** (Standard/Prompt/Professional/Raw + custom), Audio, Privacy, API Keys, Ollama, Snippets, Control Panel, About

### After Phase 2 (Future)
- General, Hotkeys, AI Engine, **Modes** (Ask/Refine/Translate), **Dictation Modes** (CRUD), **Notes** (file config + prompt), **Chat** (TBD), Audio, Privacy, API Keys, Ollama, Snippets, Control Panel, About

### V1 Reference
- General, Audio, **Modes**, **Notes**, Control Panel, Ollama, API Keys, Privacy, About
