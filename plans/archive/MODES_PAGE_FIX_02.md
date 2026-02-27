# Plan: Consolidate Modes Settings Page

## Context

The Modes settings page currently shows Ask, Refine, Translate with a legacy ProfileManager-based system (STT/LLM provider dropdowns, prompt slots). Notes and Chat are separate top-level nav items. The user wants:

1. **Remove "Active Profile" selector** from the Modes page (Control Panel already has a toggle)
2. **Add Ask output mode** — ComboBox: Toast Only, Clipboard Only, Inject Only, Clipboard + Toast
3. **Move Notes and Chat into the Modes page** below Translate
4. **Replace legacy STT/LLM Provider dropdowns** with real-time API model discovery (Cloud Model ComboBox, same as Dictation Presets — no Ollama models)
5. **Replace "Model Override" TextBox with "Local Model" ComboBox** showing only Ollama models from API
6. **Replace "System Prompt" slot ComboBox** with multi-line TextBox for direct prompt editing
7. **Split Refine into two entries** — "Refine (Auto)" and "Refine (Verbal)" — each with separate system prompts and models

**Refine Background:**
- `RefinePipeline` supports two modes: Autopilot (no audio, captures selection → LLM cleanup → inject) and Instruction (records audio, captures selection, applies spoken instruction to selection)
- Currently only Instruction mode is wired to `Ctrl+Alt+R` hotkey via `RunRefinePipelineAsync()` in LoadingViewModel
- Autopilot mode exists in the pipeline code (when `_stt = null`) but has no hotkey binding yet
- Both modes share the same `PipelineConfig` with `PipelineType = "refine"` and use the same system prompt

**Solution:**
- Add second `PipelineConfig` with `PipelineType = "refine_auto"` for Autopilot mode
- Show both in sidebar: "Refine (Auto)" and "Refine (Verbal)"
- Each has separate Cloud/Local system prompts and model selections
- Hotkey dispatch for Auto mode will be handled later (requires new `HotkeyId.RefineAuto` enum + settings page changes)

---

## Files to Modify/Create/Delete

| File | Action |
|------|--------|
| `src/DiktaMe.Core/Config/AppSettings.cs` | Add `AskOutputMode` enum + `GeneralSettings.AskOutput` |
| `src/DiktaMe.Core/Config/PromptDefaults.cs` | Add `RefineAuto` and `RefineInstruction` prompts |
| `src/DiktaMe.Core/Config/DictationModeDefaults.cs` | Add `refine_auto` PipelineConfig to `CreateBuiltInUtilityPipelines()` |
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Add Ask output routing, add TextInjector dependency |
| `src/DiktaMe.App/ViewModels/Settings/ModesSettingsViewModel.cs` | **Replace** — new unified ViewModel |
| `src/DiktaMe.App/Views/Settings/ModesSettingsPage.xaml` | **Replace** — unified 6-mode layout (Ask, Refine Auto, Refine Verbal, Translate, Notes, Chat) |
| `src/DiktaMe.App/Views/Settings/ModesSettingsPage.xaml.cs` | Update ViewModel type |
| `src/DiktaMe.App/Views/SettingsWindow.xaml` | Remove Notes/Chat nav items |
| `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` | Remove `"notes"`/`"chat"` switch cases |
| `src/DiktaMe.App/App.xaml.cs` | Update DI registrations |
| `src/DiktaMe.App/Views/Settings/NotesSettingsPage.xaml` + `.cs` | **Delete** |
| `src/DiktaMe.App/Views/Settings/ChatSettingsPage.xaml` + `.cs` | **Delete** (also fixes `Run Text x:Bind` crash on line 41) |
| `src/DiktaMe.App/ViewModels/Settings/NotesSettingsViewModel.cs` | **Delete** |
| `src/DiktaMe.App/ViewModels/Settings/ChatSettingsViewModel.cs` | **Delete** |

---

## Step 0: Add Refine prompts to PromptDefaults

**File:** `src/DiktaMe.Core/Config/PromptDefaults.cs`

Add two new prompts after the existing `Refine` const:

```csharp
/// <summary>
/// Refine Auto mode — captures selection, cleans it up with LLM, replaces in-place.
/// No audio recording, no {instruction} placeholder.
/// </summary>
public const string RefineAuto = """
    Fix grammar, improve clarity, preserve meaning. Return only refined text.
    """;

/// <summary>
/// Refine Instruction mode — captures selection, applies spoken instruction to it.
/// Uses {instruction} placeholder for the transcribed verbal command.
/// </summary>
public const string RefineInstruction = """
    Apply this instruction to the selected text: {instruction}
    Return only the result.
    """;
```

Update `GetDefault()` switch (keep existing `"refine"` for backward compat, maps to RefineInstruction):
```csharp
"refine" => RefineInstruction,  // legacy fallback
"refine_auto" => RefineAuto,
"refine_instruction" => RefineInstruction,
```

---

## Step 1: Add refine_auto PipelineConfig to defaults

**File:** `src/DiktaMe.Core/Config/DictationModeDefaults.cs`

In `CreateBuiltInUtilityPipelines()`, add a second Refine entry **before** the existing `"refine"` entry:

```csharp
new PipelineConfig
{
    PipelineType = "refine_auto",
    Hotkey = null, // No hotkey yet (to be added in future)
    CloudProfile = new UtilityProfile
    {
        SystemPrompt = PromptDefaults.RefineAuto,
        ModelName = "gpt-4o-mini",
    },
    LocalProfile = new UtilityProfile
    {
        SystemPrompt = PromptDefaults.RefineAuto,
        ModelName = null,
    },
},
```

Update the existing `"refine"` entry to use `RefineInstruction` prompt and rename `PipelineType = "refine_instruction"`:

```csharp
new PipelineConfig
{
    PipelineType = "refine_instruction",  // renamed from "refine"
    Hotkey = "Ctrl+Alt+F",
    CloudProfile = new UtilityProfile
    {
        SystemPrompt = PromptDefaults.RefineInstruction,  // changed from Refine
        ModelName = "gpt-4o-mini",
    },
    LocalProfile = new UtilityProfile
    {
        SystemPrompt = PromptDefaults.RefineInstruction,
        ModelName = null,
    },
},
```

**IMPORTANT:** Also add a migration entry for existing users — add a **third** `"refine"` legacy entry with a comment marking it deprecated, OR add migration logic in SettingsManager to rename old `"refine"` → `"refine_instruction"` on first load.

---

## Step 2: Add `AskOutputMode` to AppSettings

**File:** `src/DiktaMe.Core/Config/AppSettings.cs`

```csharp
public enum AskOutputMode
{
    ToastOnly = 0,
    ClipboardOnly = 1,
    InjectOnly = 2,
    ClipboardAndToast = 3,
}
```

Add to `GeneralSettings`:
```csharp
public AskOutputMode AskOutput { get; init; } = AskOutputMode.ClipboardAndToast;
```

Add `[JsonSerializable(typeof(AskOutputMode))]` to `AppSettingsContext`.

---

## Step 3: Ask output routing in LoadingViewModel

**File:** `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

- Add `TextInjector` field + constructor dependency (add to DI in App.xaml.cs if not already registered for LoadingViewModel)
- Update `RunAskPipelineAsync()` to change the pipeline type lookup from `"refine"` → `"refine_instruction"` (line 419)
- Replace success block (line ~498-502) with a switch on `_settings.Current.General.AskOutput`:
  - `ToastOnly` → `ShowToast("Answer", result.Text, NotificationType.Success);`
  - `ClipboardOnly` → `ClipboardManager.SetText(result.Text);`
  - `InjectOnly` → `_textInjector.InjectText(result.Text, _settings.Current.General.TrailingSpace, _settings.Current.General.AdditionalKey);`
  - `ClipboardAndToast` → `ClipboardManager.SetText(result.Text); ShowToast("Answer (copied)", result.Text, NotificationType.Success);`

Also update `RunRefinePipelineAsync()` line 419 to use `"refine_instruction"` instead of `"refine"`:
```csharp
UtilityProfile profile = _pipelines.GetActiveProfile("refine_instruction");
```

---

## Step 4: Rewrite ModesSettingsViewModel

**File:** `src/DiktaMe.App/ViewModels/Settings/ModesSettingsViewModel.cs` (replace entire content)

### Dependencies (constructor)
- `PipelineConfigManager` (replaces legacy `ProfileManager`)
- `SettingsManager`
- `ModelListService` (reuse pattern from `DictationModesSettingsViewModel`)

### Left sidebar — 6 fixed items
Create 6 `ModeListItem` entries (reuse class from `DictationModesSettingsViewModel.cs`):
1. **Ask** — `PipelineType = "ask"`, `Title = "Ask"`
2. **Refine (Auto)** — `PipelineType = "refine_auto"`, `Title = "Refine (Auto)"`, `Subtitle = "No audio"`
3. **Refine (Verbal)** — `PipelineType = "refine_instruction"`, `Title = "Refine (Verbal)"`, `Subtitle = "With instruction"`
4. **Translate** — `PipelineType = "translate"`, `Title = "Translate"`
5. **Notes** — `PipelineType = "note"`, `Title = "Notes"`
6. **Chat** — `PipelineType = "chat"`, `Title = "Chat"`

### Two model lists from single API call
Reuse `ModelListService.GetAvailableModelsAsync()`:
- **`CloudModelNames`** — filter out `Provider == "Ollama (Local)"`, add "(Default)" first entry
- **`LocalModelNames`** — filter to ONLY `Provider == "Ollama (Local)"`, add "(Default)" first entry

### Shared properties (all 6 modes have these)
- `CloudSystemPrompt`, `LocalSystemPrompt` (string, multi-line)
- `SelectedCloudModelIndex` / `CloudModelNames` (ObservableCollection<string>)
- `SelectedLocalModelIndex` / `LocalModelNames` (ObservableCollection<string>)

### Conditional properties (visibility-bound)
- **Ask:** `SelectedAskOutputIndex`, `AskOutputOptions[]`, `IsAskSelected`
- **Notes:** `NoteFilePath`, `NoteUseLlmProcessing`, `NoteTimestampFormat`, `NotePreviewText`, `IsNoteSelected`, `BrowseNoteFilePathCommand`
- **Chat:** `ChatFontSize`, `ChatWindowOpacity`, `ChatSelectedThemeIndex`, `ChatThemeOptions[]`, `ChatForgetOnClose`, `ChatMaxHistoryMessages`, `ChatShowTimestamps`, `ChatEnableMarkdown`, `IsChatSelected`

### Load on selection change
`OnSelectedIndexChanged()` → `LoadDetail()` → read `PipelineConfigManager.GetPipelineByType(pipelineType)` for system prompts + model names. Also read mode-specific settings from `SettingsManager` (NoteSettings, ChatSettings, GeneralSettings.AskOutput).

Map model names to combo indices using `FindModelIndex()` helper (same pattern as DictationModesSettingsViewModel).

### Save (explicit button)
`SaveCommand` → `SaveAsync()`:
- Always: `PipelineConfigManager.UpdatePipelineAsync(type, cloudProfile, localProfile)`
- Ask: also save `GeneralSettings with { AskOutput = ... }`
- Notes: also save `NoteSettings with { FilePath = ..., TimestampFormat = ..., ... }`
- Chat: also save `ChatSettings with { FontSize = ..., WindowOpacity = ..., ... }`

### Reset (Notes/Chat only)
`ResetCommand` → `ResetAsync()` (visibility bound to `IsNoteSelected || IsChatSelected`):
- Notes: reset to `PromptDefaults.Note`/`RefineAuto`, default file path, timestamp format
- Chat: reset to `PromptDefaults.Chat`, default UI settings

---

## Step 5: Rewrite ModesSettingsPage XAML

**File:** `src/DiktaMe.App/Views/Settings/ModesSettingsPage.xaml` (replace entire content)

Two-column Grid layout:

**Left (200px):**
- Title "Pipelines" (FontSize 24, FontWeight SemiBold)
- ListView with `ItemsSource="{x:Bind ViewModel.ModeItems}"`, `SelectedIndex="{x:Bind ViewModel.SelectedIndex, Mode=TwoWay}"`
- ListView.ItemTemplate: Display `Title` + `Subtitle` (Subtitle in smaller, faded text)

**Right (ScrollViewer, MaxWidth 600):**

1. **Common section** (always visible when `HasSelection` is true):
   - Horizontal divider
   - **Cloud Model** — ComboBox (`CloudModelNames`) + Refresh button (horizontal layout)
   - **Cloud System Prompt** — TextBox (AcceptsReturn, TextWrapping, MinHeight 150, MaxHeight 250)
   - Horizontal divider
   - **Local Model** — ComboBox (`LocalModelNames`) + Refresh button (horizontal layout)
   - **Local System Prompt** — TextBox (AcceptsReturn, TextWrapping, MinHeight 150, MaxHeight 250)
   - InfoBar (Informational, non-closable): "Local profile uses the global Ollama model configured in the AI Engine tab."

2. **Ask section** (`Visibility` bound to `IsAskSelected` via `BoolToVis` converter):
   - Horizontal divider
   - "Output Mode" — ComboBox (`AskOutputOptions`, `SelectedAskOutputIndex`)

3. **Notes section** (`Visibility` bound to `IsNoteSelected`):
   - Horizontal divider
   - "Note File Path" — TextBox + Browse button (Grid, 2 columns)
   - "LLM Processing" — ToggleSwitch (`NoteUseLlmProcessing`)
   - "Timestamp Format" — TextBox (`NoteTimestampFormat`, FontFamily Consolas)
   - "Live Preview" — Border with TextBlock showing `NotePreviewText`

4. **Chat section** (`Visibility` bound to `IsChatSelected`):
   - Horizontal divider
   - "Font Size" — Slider (10-24) + TextBlock showing value + "pt" label (horizontal StackPanel, NOT `Run Text x:Bind`)
   - "Window Opacity" — Slider (0.5-1.0) + TextBlock showing value (horizontal StackPanel)
   - "Theme" — ComboBox (`ChatThemeOptions`, `ChatSelectedThemeIndex`)
   - "Forget on Close" — ToggleSwitch (`ChatForgetOnClose`)
   - "Max History Messages" — NumberBox (`ChatMaxHistoryMessages`, 0-500)
   - "Show Timestamps" — ToggleSwitch (`ChatShowTimestamps`)
   - "Enable Markdown" — ToggleSwitch (`ChatEnableMarkdown`)

5. **Bottom (horizontal, right-aligned):**
   - "Reset to Defaults" button (visibility bound to `IsNoteSelected || IsChatSelected` via OR multi-binding, Command=`{x:Bind ViewModel.ResetCommand}`)
   - "Save" button (AccentButtonStyle, Command=`{x:Bind ViewModel.SaveCommand}`)

**Note:** Reuse converters from `SharedResources.xaml` — `BoolToVis`, `InverseBoolToVis`, etc.

---

## Step 6: Navigation cleanup

**SettingsWindow.xaml (lines 40-49):** Remove the two NavigationViewItems:
```xml
<muxc:NavigationViewItem Content="Notes" Tag="notes">...</muxc:NavigationViewItem>
<muxc:NavigationViewItem Content="Chat" Tag="chat">...</muxc:NavigationViewItem>
```

**SettingsWindow.xaml.cs (lines 38-52):** Remove `"notes"` and `"chat"` from switch statement in `NavView_SelectionChanged()`.

---

## Step 7: DI cleanup

**App.xaml.cs:**
- Remove `NotesSettingsViewModel`, `ChatSettingsViewModel` singleton registrations
- Update `ModesSettingsViewModel` registration (new constructor needs: `PipelineConfigManager`, `SettingsManager`, `ModelListService`)
- Keep `ProfileManager` registration (still used by PipelineFactory and WizardViewModel for now)

---

## Step 8: Delete obsolete files

Use Bash to delete:
```bash
rm src/DiktaMe.App/Views/Settings/NotesSettingsPage.xaml
rm src/DiktaMe.App/Views/Settings/NotesSettingsPage.xaml.cs
rm src/DiktaMe.App/Views/Settings/ChatSettingsPage.xaml
rm src/DiktaMe.App/Views/Settings/ChatSettingsPage.xaml.cs
rm src/DiktaMe.App/ViewModels/Settings/NotesSettingsViewModel.cs
rm src/DiktaMe.App/ViewModels/Settings/ChatSettingsViewModel.cs
```

---

## Step 9: Migration for existing users

**File:** `src/DiktaMe.Core/Config/SettingsManager.cs` (or `DictationModeDefaults.cs`)

Add migration logic to rename old `"refine"` PipelineConfig → `"refine_instruction"` when loading settings from disk. This ensures existing users' refine prompts/models are preserved.

**Option A (SettingsManager.LoadAsync):**
After deserializing `settings.json`, check if `UtilityPipelines` contains a `"refine"` entry. If yes, rename it to `"refine_instruction"`, add a new `"refine_auto"` entry with defaults, and save.

**Option B (simpler — keep legacy "refine" as alias):**
In `DictationModeDefaults.CreateBuiltInUtilityPipelines()`, include **both** `"refine"` (legacy) and `"refine_instruction"` (new) entries pointing to the same prompt. The UI only shows `"refine_auto"` and `"refine_instruction"`, but the backend accepts `"refine"` for backward compat.

**Recommendation:** Use Option B (simpler, no migration code needed).

---

## Verification

```bash
dotnet build src/DiktaMe.App/DiktaMe.App.csproj -c Release "-p:Platform=x64"
```

Manual checks:
- Settings → Modes shows **6 items** in left list: Ask, Refine (Auto), Refine (Verbal), Translate, Notes, Chat
- No "Active Profile" selector anywhere on Modes page
- Cloud Model ComboBox populates (no Ollama models)
- Local Model ComboBox shows only Ollama models
- **Refine (Auto)** — System Prompt is multi-line TextBox, no `{instruction}` placeholder
- **Refine (Verbal)** — System Prompt is multi-line TextBox with `{instruction}` placeholder
- Ask: output mode ComboBox appears and persists across restarts
- Notes: file path + Browse, LLM toggle, timestamp format + preview all work
- Chat: all UI settings work (no `Run Text x:Bind` crash on opacity slider)
- Save button persists all changes to `settings.json`
- Reset button (Notes/Chat only) resets to defaults
- Notes/Chat **removed** from navigation sidebar (no longer top-level items)

End-to-end pipeline test:
- `Ctrl+Alt+R` hotkey should still trigger Refine Instruction mode (audio recording)
- Verify `RunRefinePipelineAsync()` uses `"refine_instruction"` profile
- Verify existing users' settings migrate correctly (or legacy `"refine"` still works)
