# Critical Bugs Found During Manual Testing

**Session**: 2026-02-22
**Test Phase**: First-run wizard walkthrough

---

## Bug 1: Wizard Test Page - "Recording completed but no audio file was produced"

### Symptoms
- User clicks "Record" button on Wizard Test Page
- After 3 seconds, shows error: "Recording completed but no audio file was produced"
- No microphone selection available before test

### Root Cause
**AudioRecorder is registered as Singleton but needs to be Transient**

**File**: `src/DiktaMe.App/App.xaml.cs` line 139
```csharp
services.AddSingleton<AudioRecorder>();  // ❌ WRONG - can't be reused after Dispose()
```

**Problem**:
1. AudioRecorder implements IDisposable and is disposed after first use
2. WizardTestPage gets the same disposed instance on second click
3. Disposed AudioRecorder returns null from StopRecordingAsync()

**Evidence**:
- `AudioRecorder.Dispose()` sets `_disposed = true` and nulls out `_waveIn` and `_writer`
- `StopRecordingAsync()` checks `if (!IsRecording) return null;` (line 119-121)
- After disposal, IsRecording is false, so it always returns null

### Fix Required
1. Change DI registration to Transient:
   ```csharp
   services.AddTransient<AudioRecorder>();  // ✅ New instance per use
   ```

2. Update WizardTestPage to create new instance per test OR keep instance alive between tests

### Additional Issue: No Mic Selection
Wizard should allow user to select microphone device before running test, especially if default device is not configured or incorrect.

**Suggested Enhancement**:
- Add microphone dropdown to WizardTestPage
- Populate from AudioDeviceManager.GetInputDevices()
- Pass selected device to AudioRecorder.StartRecording(deviceId: ...)

---

## Bug 2: API Keys Not Validated Before Saving

### Symptoms
- Wizard API Keys page accepts any text input
- No validation that keys are actually valid
- User completes wizard with invalid keys
- Fails silently later when trying to use providers

### Root Cause
**No validation logic in WizardApiKeysPage or WizardViewModel**

**Files**:
- `src/DiktaMe.App/Views/Wizard/WizardApiKeysPage.xaml.cs` - just updates ViewModel properties
- `src/DiktaMe.App/ViewModels/WizardViewModel.cs` line 112-121 - saves keys without validation

**Current Flow**:
1. User enters any string (even gibberish)
2. PasswordBox_PasswordChanged updates ViewModel.DeepgramApiKey
3. CompleteWizardAsync() saves to SecureStorage without checking
4. User proceeds to Quick Test → fails
5. User completes wizard → keys don't work later

### Fix Required
**Option A (RECOMMENDED)**: Test-before-save approach
1. Add "Test" button next to each API key input
2. Make simple test API call (e.g., Deepgram: GET /v1/projects, Gemini: HEAD request with key header)
3. Show ✅ or ❌ indicator
4. Only allow "Next" if at least one provider tested successfully

**Option B**: Validate format only
1. Check key format patterns (Deepgram: alphanumeric, Gemini: starts with "AIza", etc.)
2. Warn if format looks wrong
3. Still allow proceeding (less safe)

**Option C**: Defer to Quick Test
1. Skip validation in API Keys page
2. Make Quick Test page actually use the entered keys
3. Show specific error if API keys are invalid
4. Allow going back to fix keys

### Recommendation
Use **Option A** with fallback to skip:
- "Test Connection" button for each key
- Visual feedback (spinner → checkmark/error)
- Can skip with warning: "API keys not tested. Quick Test will be skipped."

---

## Bug 3: Hotkeys Don't Trigger Anything

### Symptoms
- User sets hotkeys in Settings → Hotkeys page
- Hotkeys save successfully
- Pressing hotkey combinations does nothing
- No visible response, no dictation triggered

### Root Cause
**HotkeyManager is registered in DI but never started**

**Evidence**:
1. `src/DiktaMe.App/App.xaml.cs` line 148: `services.AddSingleton<HotkeyManager>();`
2. Search for initialization: **ZERO results** for:
   - `HotkeyManager.*Start`
   - `HotkeyManager.*RegisterAll`
   - `HotkeyManager.*Initialize`

**HotkeyManager.cs** requires explicit initialization:
- Line 46: `public void Start()` - Starts background message pump thread
- Line 78: `public bool Register(HotkeyId id, string hotkeyString)` - Registers each hotkey with Win32 RegisterHotKey
- Line 81-84: Throws exception if Start() not called: `"Call Start() before registering hotkeys."`

**What's Missing**:
1. No call to `HotkeyManager.Start()` on app startup
2. No call to `HotkeyManager.Register()` for each configured hotkey
3. No event handler for `HotkeyManager.HotkeyPressed` to trigger actions

### Fix Required

**1. Start HotkeyManager on App Init**

File: `src/DiktaMe.App/App.xaml.cs` (or `LoadingViewModel.cs`)

```csharp
// After DI container is built, in OnLaunched or LoadingViewModel.InitializeAsync
var hotkeyManager = Services.GetRequiredService<HotkeyManager>();
hotkeyManager.Start();

// Subscribe to events
hotkeyManager.HotkeyPressed += OnHotkeyPressed;
hotkeyManager.RegistrationFailed += OnHotkeyRegistrationFailed;
```

**2. Register Hotkeys from Settings**

```csharp
var settings = Services.GetRequiredService<SettingsManager>();
var hotkeys = settings.Current.HotkeySettings;

hotkeyManager.Register(HotkeyId.Dictate, hotkeys.Dictate);
hotkeyManager.Register(HotkeyId.Refine, hotkeys.Refine);
hotkeyManager.Register(HotkeyId.Ask, hotkeys.Ask);
hotkeyManager.Register(HotkeyId.Translate, hotkeys.Translate);
hotkeyManager.Register(HotkeyId.Oops, hotkeys.Oops);
hotkeyManager.Register(HotkeyId.Note, hotkeys.Note);
hotkeyManager.Register(HotkeyId.Chat, hotkeys.Chat);
```

**3. Wire Up Actions**

```csharp
private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
{
    Log.Information("Hotkey pressed: {Id}", e.Id);

    // TODO: Trigger corresponding pipeline action
    // This will need PipelineOrchestrator integration
    switch (e.Id)
    {
        case HotkeyId.Dictate:
            // Start dictate mode recording
            break;
        case HotkeyId.Chat:
            // Open Quick Chat window
            var chatWindow = Services.GetRequiredService<QuickChatWindow>();
            chatWindow.Activate();
            break;
        // ... etc
    }
}

private void OnHotkeyRegistrationFailed(object? sender, HotkeyRegistrationFailedEventArgs e)
{
    Log.Warning("Hotkey registration failed: {Id} = '{HotkeyString}' - {Reason}",
        e.Id, e.HotkeyString, e.Reason);

    // TODO: Show notification to user that hotkey is already taken
}
```

**4. Re-register on Settings Change**

When user changes hotkeys in Settings UI, need to re-register:

```csharp
// In HotkeysSettingsViewModel or when settings change
settingsManager.SettingsChanged += (_, newSettings) =>
{
    var hotkeys = newSettings.HotkeySettings;
    hotkeyManager.Register(HotkeyId.Dictate, hotkeys.Dictate);
    // ... etc for all 7 hotkeys
};
```

### Notes
- HotkeyManager already has all the Win32 plumbing implemented correctly
- Message pump runs on background thread (no UI dispatcher needed)
- Debouncing already implemented (500ms)
- Just needs to be **wired into app lifecycle**

---

## Priority

1. ✅ **Bug 3 - Hotkeys** (CRITICAL) - **FIXED**
2. ✅ **Bug 2 - API Validation** (HIGH) - **FIXED**
3. ✅ **Bug 1 - Audio Recorder** (MEDIUM) - **FIXED**

---

## ALL BUGS FIXED ✅

**Session**: 2026-02-22 (continued)

### Fixes Implemented:

**Bug 3 - Hotkeys (FIXED)**:
- Added HotkeyManager initialization to LoadingViewModel.InitializeAsync()
- Wired up HotkeyPressed and RegistrationFailed event handlers
- Registers all 7 hotkeys from settings on app startup
- Re-registers hotkeys when settings change
- Chat hotkey opens QuickChatWindow (working)
- Other 6 hotkeys log warning (pending PipelineOrchestrator integration)
- **Files modified**: LoadingViewModel.cs

**Bug 1 - AudioRecorder (FIXED)**:
- Changed DI registration from Singleton to Transient in App.xaml.cs
- Each usage gets fresh AudioRecorder instance that can be disposed
- AudioDucker changed to Singleton (no longer auto-attached)
- Added microphone selection ComboBox to WizardTestPage
- Loads all available input devices from AudioDeviceManager
- Passes selected device index to AudioRecorder.StartRecording()
- **Files modified**: App.xaml.cs, WizardTestPage.xaml, WizardTestPage.xaml.cs

**Bug 2 - API Key Validation (FIXED)**:
- Added "Test" buttons next to each API key input field
- Test buttons enabled when key is entered
- Deepgram test: GET /v1/projects with Authorization header
- Gemini test: GET /v1beta/models?key={apiKey}
- Visual feedback: ✓ green (valid), ✗ red (invalid), spinner while testing
- Status persists across wizard navigation
- Non-blocking: user can skip testing if desired
- **Files modified**: WizardApiKeysPage.xaml, WizardApiKeysPage.xaml.cs

**Build Status**: 0 warnings, 0 errors

---

## Impact on Manual Testing

All three blocking bugs are now **RESOLVED**:
- ✅ Can complete wizard with API key validation
- ✅ Can test Quick Test page (audio recorder works multiple times)
- ✅ Can test hotkeys (Chat hotkey functional, others log correctly)

**Ready to resume Journey 1 manual testing.**
