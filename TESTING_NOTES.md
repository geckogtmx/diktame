### Section 1: First-Run Experience (Wizard Flow)

- EGT: No brand icons or logo on UI, We need a "Branding" run.
- EGT: Only Deepgram is shown on STT options, it should just say "Cloud" and a text explaining provider selection is next step
- EGT: Wizard asks for the Deepgram and Gemini options but did not request any API keys, not sure if this are required for the "Quick Test" screen as it mentions "chosen providers are working" - If we want this to "Just Work" we need to ask them for keys at that point making sure that is 100% safe.
- EGT: The "You´re All Set" Page has some overlapping text at the bottom that cant be read without adjusting the window manually, make sure all windows show all the text within the designated space

----

- EGT: There is no System Tray loading. Control Panel and Settings should be independent of the App running. It should always be present as a tray icon, double click opens Control panel, which can be minimized and closed without affecting the app. Tray Icon has menu for opening Settings, Control panel , Updates, close, etc....
- EGT: There is no Hotkey Setting anywhere, this is CRITICAL.

---

## INVESTIGATION RESULTS (Claude)

### Issue 1: No Branding/Logo ✅ ASSETS FOUND
**Status**: Assets exist at `E:\git\diktate\assets` (V1 repo)
**Current**: Assets folder empty, TrayIconView uses runtime-generated "D" icon

**Fix**:
- [ ] Copy assets from `E:\git\diktate\assets` to `E:\git\diktame\src\DiktaMe.App\Assets\`
- [ ] Update TrayIconView.xaml to use real .ico file (line 14-22)
- [ ] Add logo to: LoadingWindow, WizardWelcomePage, AboutPage

---

### Issue 2: STT Wizard Text Unclear ✅ CONFIRMED
**Files**: WizardSttPage.xaml line 13, WizardLlmPage.xaml line 13
**Current**: Shows "Cloud (Deepgram)" - confusing, looks like no other options

**Fix**:
- [ ] Change to generic labels: "Cloud STT" / "Local STT"
- [ ] Add subtitle: "High accuracy, requires API key" / "Runs offline, no API key"

---

### Issue 3: No API Key Input in Wizard ✅ CONFIRMED
**Current flow**: 5 steps (Welcome → STT → LLM → Quick Test → Ready)
**Problem**: User chooses cloud providers but can't test without API keys

**Fix Options**:
- **Option A** (RECOMMENDED): Add Step 3.5 "API Keys" between LLM and Quick Test
  - Only show if cloud providers selected
  - Quick entry: Deepgram + Gemini/OpenAI/Anthropic
  - Makes Quick Test functional
- **Option B**: Skip Quick Test if no keys, show warning
- **Option C**: Fallback to local providers if no keys

**Decision needed**: Which option?

---

### Issue 4: Overlapping Text "You're All Set" ✅ CONFIRMED
**File**: WizardReadyPage.xaml lines 18-19
**Problem**: No `TextWrapping="Wrap"` on dynamic summary text

**Fix** (EASY):
- [ ] Add `TextWrapping="Wrap"` to summary TextBlocks in WizardReadyPage.xaml

---

### Issue 5: System Tray Icon ✅ FIXED & TESTED
**Root cause**: XAML-based `TaskbarIcon` (H.NotifyIcon.WinUI) fails silently when created outside a visual tree — `GeneratedIconSource` needs XAML rendering infrastructure, and `x:Bind` commands don't resolve for the native PopupMenu conversion.

**Fix implemented**: Rewrote to use `H.NotifyIcon.Core.TrayIcon` (low-level Win32 `Shell_NotifyIcon` wrapper):
- [x] Real `.ico` file loaded from disk (generated from icon.png)
- [x] `PopupMenu` built via `H.NotifyIcon.Core.PopupMenu` (native `TrackPopupMenuEx`)
- [x] Left-click opens Control Panel, right-click shows context menu
- [x] 5 menu items: Open Control Panel, Quick Chat, Settings, separator, Quit
- [x] Window close now hides instead of exiting (tray keeps app alive)
- [x] Tooltip shows "dIKta.me — Idle"
- [x] Icon visible in system tray with proper logo
- [x] Committed: `748aea7`

---

### Issue 6: CRITICAL - No Hotkey Settings UI ✅ FIXED & TESTED
**Backend**: HotkeySettings exists in AppSettings.cs (lines 114-123)
**Defaults**: Dictate (Ctrl+Alt+D), Refine (Ctrl+Alt+R), Ask (Ctrl+Alt+A), Translate (Ctrl+Alt+T), Oops (Ctrl+Alt+V), Note (Ctrl+Alt+N), Chat (Ctrl+Alt+C)

**Problem**: NO UI to configure hotkeys anywhere in Settings window
**Impact**: Users stuck with hardcoded defaults, can't customize

**Testing Status**: ✅ PASS - Keys capture and save correctly

**Fix Implemented**:
- [x] Create `Views/Settings/HotkeysSettingsPage.xaml` + code-behind with keyboard capture
- [x] Create `ViewModels/Settings/HotkeysSettingsViewModel.cs` with RelayCommands for reset
- [x] Add "Hotkeys" NavigationViewItem to SettingsWindow.xaml (2nd position, after General)
- [x] Register HotkeysSettingsViewModel in DI container (App.xaml.cs)
- [x] Add `Chat = 7` to HotkeyId enum (was missing)
- [x] Build verified (0 errors, 0 warnings)
- [x] Fixed crash on page load (lazy-init _normalBorderBrush)
- [x] Manually tested - keys capture and save ✅

**UI Features** (UPDATED):
- **Record button** for each hotkey - captures actual keypresses (not manual text entry)
- **Visual feedback** - Orange border (3px) when recording active
- **Reset button** for each hotkey - restores default value
- **Read-only TextBox** - prevents manual editing, only Record button changes value
- **Modifier detection** - Ctrl, Alt, Shift, Win keys detected via `GetKeyStateForCurrentThread`
- **Key mapping** - A-Z, 0-9, F1-F12, Numpad, special keys (Space, Enter, etc.)
- Auto-save on capture (follows existing settings pattern)
- InfoBar with updated instructions: "Click Record, then press your desired key combination"
- Restart required notice for new hotkeys to take effect

**Files Modified**:
- NEW: `src/DiktaMe.App/Views/Settings/HotkeysSettingsPage.xaml`
- NEW: `src/DiktaMe.App/Views/Settings/HotkeysSettingsPage.xaml.cs`
- NEW: `src/DiktaMe.App/ViewModels/Settings/HotkeysSettingsViewModel.cs`
- MODIFIED: `src/DiktaMe.App/Views/SettingsWindow.xaml` (added Hotkeys tab)
- MODIFIED: `src/DiktaMe.App/Views/SettingsWindow.xaml.cs` (added routing)
- MODIFIED: `src/DiktaMe.App/App.xaml.cs` (DI registration)
- MODIFIED: `src/DiktaMe.Core/Input/HotkeyManager.cs` (added Chat to HotkeyId enum)

---

## FIX PRIORITY

1. ✅ **Issue 6 - Hotkeys UI** (CRITICAL) - **FIXED & TESTED**
2. ✅ **Issue 5 - Tray Icon** (MEDIUM) - **FIXED & TESTED** (commit 748aea7)
3. ⚠️ **Issue 3 - API Keys Wizard** (HIGH) - Wizard incomplete, Quick Test fails — **NEXT**
4. ✅ **Issue 4 - Text Overflow** (EASY) - 1-line fix
5. ✅ **Issue 2 - Wizard Labels** (EASY) - Text change
6. 🎨 **Issue 1 - Branding** (LOW) - Copy assets, update refs

---

## ACTIONS REQUIRED

**Immediate**:
- [ ] Copy logo/icons from `E:\git\diktate\assets` → `E:\git\diktame\src\DiktaMe.App\Assets\`
- [x] ~~Create implementation plan for Issue 6 (Hotkeys Settings UI)~~ **DONE** ✅
- [ ] Decide on Issue 3 fix approach (Option A vs B vs C)

**Testing**:
- Paused Journey 1 at step 1.1.6
- ✅ Issue 6 tested and working (Hotkeys capture and save)
- **Next priority**: Issue 3 - API Keys Wizard step (HIGH)
- Then: Continue manual testing of wizard flow

---

## NOTES

- HotkeyManager backend is fully implemented (HotkeyManager.cs, HotkeyParser.cs, HotkeyParserTests)
- HotkeySettings in AppSettings.cs has all 7 hotkeys defined
- Just missing the UI layer to expose configuration to users
- V1 assets location confirmed: `E:\git\diktate\assets`
