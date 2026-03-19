# SPEC_016: V2.1 Refinemmarly — Grammarly-like Grammar Check

> **Status:** DRAFT
> **Date:** 2026-03-19
> **Codename:** Refinemmarly (Refine + Grammarly)
> **Prerequisite:** SPEC_015 (all modules sprint complete)
> **Architecture:** Enhances existing Refine Auto pipeline with structured grammar analysis, floating popup UI, and passive clipboard monitoring.
> **Goal:** Ship a Grammarly-like grammar checker that works system-wide via hotkey + passive clipboard detection.

---

## 1. Executive Summary

dIKta.me V2 already has a "Refine Auto" feature (Ctrl+Alt+R) that captures selected text, sends it to an LLM, and auto-replaces it with a cleaned version. This works but is a blunt instrument — the user has no visibility into what changed or why.

**Refinemmarly** upgrades this into a Grammarly-like experience:

1. **Structured corrections** — The LLM returns individual corrections categorized as Spelling, Grammar, or Style (not just a blob of "fixed" text)
2. **Inline diff popup** — A floating window shows strikethrough (old) + green (new) per word/phrase. The user reviews and accepts/rejects each correction independently
3. **Passive clipboard monitoring** — When the user copies text (Ctrl+C) in any app, dIKta.me silently analyzes it and notifies about grammar issues
4. **Configurable actions** — Both the hotkey and clipboard paths support three actions: **Suggest** (popup), **Toast** (notification only), or **Inject** (auto-replace)

### Why This Matters

- **Grammarly charges $30/month** for premium grammar checking. dIKta.me does it for free using the user's existing LLM (local Ollama or cloud Gemini)
- **Works everywhere** — Unlike Grammarly (limited to ~60% of apps via UI Automation), dIKta.me uses clipboard-based capture which works in 100% of Windows apps
- **Per-correction control** — Users fix typos without accepting unwanted style changes
- **No new hotkey needed** — Piggybacks on existing Refine Auto (Ctrl+Alt+R)

---

## 2. Interaction Model

### Two-Path Design

Both paths share the same LLM grammar-check backend and the same three configurable actions:

| Action | What it does |
|--------|-------------|
| **Suggest** | Opens Grammar Popup with inline diff. User reviews and accepts/rejects per correction. |
| **Toast** | Shows notification "3 issues found". Awareness only, no auto-fix. |
| **Inject** | Auto-replaces text immediately (current Refine Auto behavior). No popup shown. |

Each path has its own setting — user configures independently:

| Trigger | Path | Setting | Default |
|---------|------|---------|---------|
| **Ctrl+Alt+R** (Refine Auto) | Active | `Grammar.HotkeyAction` | **Suggest** |
| **Ctrl+C** (copy in any app) | Passive | `Grammar.ClipboardAction` | **Toast** |

### User Personas

| Persona | Hotkey Action | Clipboard Action | Rationale |
|---------|---------------|------------------|-----------|
| Power user | Inject | Toast | Fast auto-fix on demand, passive awareness when copying |
| Careful user | Suggest | Suggest | Review everything, even copied text |
| Minimal user | Suggest | Disabled | Only grammar-check when explicitly asked |
| Writer | Suggest | Toast | Review own text carefully, get notified about copied quotes |

### Why Two Paths?

The user raised a valid question: "Isn't Ctrl+C + wait for notification the same as select + Ctrl+Alt+R?" The key differences:

- **Ctrl+Alt+R** = intentional: "I want to fix this text NOW"
- **Ctrl+C** = incidental: user was copying for another reason (email, paste elsewhere, sharing). The grammar notification is a bonus — "hey, that text you just copied has 3 errors"
- The clipboard path is most useful for **discovering errors you didn't know about** (someone else's text, your own text you thought was fine)

---

## 3. Technical Research Findings

### Windows Right-Click Context Menu

The initial idea was to inject items into Windows' system right-click context menu (the one with Cut/Copy/Paste/Spelling/Rewrite/Summarize). Research findings:

- **NOT POSSIBLE for third-party apps.** Those "Rewrite"/"Summarize" items in Windows 11 are Microsoft proprietary features built into Edge and Notepad via Copilot/Phi Silica. There is no extensible API.
- The File Explorer context menu CAN be extended via shell extensions (IContextMenu, COM registration), but that's for files, not text fields.
- Text Services Framework (TSF) is for input processing (IMEs, dictation), not UI extensibility.

**Conclusion:** The right approach is Grammarly's approach — hotkey + floating popup + clipboard detection.

### How Grammarly Actually Works

- Uses Windows **UI Automation TextPattern** to read text from other apps' text fields
- Draws a **floating overlay widget** near text fields (not injected into the context menu)
- Requires **Accessibility permissions** in Windows Settings
- Uses **keyboard hooks** (WH_KEYBOARD_LL) to detect when user stops typing
- Text modification is still done via clipboard + keyboard simulation (TextPattern is read-only on most controls)

### Why dIKta.me's Approach Is Better

| Aspect | Grammarly | dIKta.me Refinemmarly |
|--------|-----------|----------------------|
| Text capture | UI Automation TextPattern (~60% app coverage) | Clipboard-based (100% app coverage) |
| Permissions | Requires Accessibility settings | None needed |
| Text injection | Clipboard + keyboard simulation | Same (proven TextInjector) |
| LLM | Grammarly's proprietary servers ($30/mo) | User's own LLM (local Ollama = free, or cloud Gemini) |
| Privacy | Text sent to Grammarly servers | Local mode = never leaves machine |
| Trigger | Auto-detect (polling, CPU-heavy) | Hotkey (explicit) + clipboard monitor (event-based, lightweight) |

### Passive Monitoring: UI Automation vs Clipboard

| Approach | Feasibility | Coverage | Complexity | Permissions |
|----------|------------|----------|------------|-------------|
| **Clipboard monitor (WM_CLIPBOARDUPDATE)** | Easy | 100% of apps | Low (~200 LOC) | None |
| **UI Automation TextPattern** | Hard | ~60% of apps | High (~1500 LOC) | Accessibility permission |

**Decision:** Implement clipboard monitor (Phase 1B). Document UI Automation as future Phase 2.

---

## 4. Implementation Phases

### Phase 1A: Grammar Check Popup (Active Path)

**Scope:** Enhance `RunRefineAutoAsync()` to show a grammar review popup instead of auto-injecting.

**Flow:**
1. User selects text in any app → presses Ctrl+Alt+R (Refine, mode=Auto)
2. `LoadingViewModel.RunRefineAutoAsync()` captures selection via `TextInjector.CaptureSelection()`
3. Calls LLM with structured JSON prompt → returns individual corrections
4. Parses JSON into `GrammarResult` model
5. Routes based on `Grammar.HotkeyAction` setting:
   - **Suggest**: Opens `GrammarPopupWindow` near cursor with inline diff + correction cards
   - **Toast**: Shows notification "X issues found"
   - **Inject**: Auto-replaces text (current Refine Auto behavior, backward compatible)
6. In Suggest mode: user accepts individual changes or all → text injected back via `TextInjector.InjectText()`

### Phase 1B: Clipboard Monitor (Passive Path)

**Scope:** Background `WM_CLIPBOARDUPDATE` listener that silently analyzes copied text.

**Flow:**
1. `ClipboardMonitorService` registers as clipboard format listener via `AddClipboardFormatListener(hwnd)`
2. When `WM_CLIPBOARDUPDATE` fires, reads clipboard text + captures foreground HWND
3. Debounces (500ms), filters text-only, skips self-injection
4. Sends text to LLM with grammar check prompt (same as Phase 1A)
5. Parses result into `GrammarResult`
6. Routes based on `Grammar.ClipboardAction` setting:
   - **Suggest**: Opens Grammar Popup with results (user can accept/reject + inject back)
   - **Toast**: Shows "Grammar: X issues found"
   - **Inject**: Auto-replaces in source window

### Phase 2: Full UI Automation Passive (FUTURE — NOT IN THIS SPEC)

**Scope:** Background thread monitors active text field via Windows UI Automation `TextPattern`.

**Status:** Documented for future reference. Not implemented in this spec because:
- Adds NuGet dependency (`Interop.UIAutomationClient`) and COM interop complexity
- Requires Windows Accessibility permission setup (user friction)
- Only works in ~60% of apps (TextPattern support varies)
- Clipboard monitor (Phase 1B) covers 80% of the use case with 20% of the complexity

**Architecture sketch (for future implementer):**
```
TextMonitoringService : IDisposable
  - Background thread with COM apartment (STA required for UIA events)
  - AddAutomationFocusChangedEventHandler → OnFocusChanged
  - OnFocusChanged:
    - Check if element supports TextPattern
    - If yes: read text via TextPattern.DocumentRange.GetText(-1)
    - Queue grammar check
  - Debounce: 2-second timer after focus change before reading
  - Performance: GetText(-1) is one cross-process call, ~5-50ms
  - Must handle: timeout, access denied, element disposed
  - UIPI constraint: cannot read from elevated (admin) processes
```

**App compatibility for TextPattern:**

| App | Support | Notes |
|-----|---------|-------|
| Edge, Chrome | Yes | Chrome needs `--force-renderer-accessibility` |
| Notepad | Yes | Full support |
| Word, Excel | Yes | Full support |
| VS Code | Yes | Electron with accessibility |
| Notepad++, Discord | No | Custom controls |
| Elevated apps | Blocked | UIPI prevents cross-privilege reads |

### Future Phase 3: Tone Variants (NOT IN THIS SPEC)

After grammar corrections are shown, offer tone rewriting (Professional, Formal, Casual). Each tone variant re-calls LLM with a tone-specific prompt. Deferred because grammar checking is the core value; tones are a polish feature.

---

## 5. Data Models

### `src/DiktaMe.Core/Pipeline/GrammarResult.cs` — NEW

```csharp
public enum CorrectionCategory
{
    Spelling,
    Grammar,
    Style,
}

public sealed record GrammarCorrection
{
    public required string Original { get; init; }
    public required string Corrected { get; init; }
    public required CorrectionCategory Category { get; init; }
    public string? Reason { get; init; }
    public int Offset { get; init; } // zero-based char position in original text
}

public sealed record GrammarResult
{
    public required string OriginalText { get; init; }
    public required string CorrectedText { get; init; }
    public required IReadOnlyList<GrammarCorrection> Corrections { get; init; }
}
```

Internal DTOs for JSON deserialization (source-generated, trim-safe):
```csharp
internal sealed record GrammarCorrectionDto
{
    public string Original { get; init; } = "";
    public string Corrected { get; init; } = "";
    public string Category { get; init; } = "grammar";
    public string? Reason { get; init; }
}

internal sealed record GrammarLlmResponse
{
    public List<GrammarCorrectionDto> Corrections { get; init; } = [];
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GrammarLlmResponse))]
[JsonSerializable(typeof(List<GrammarCorrectionDto>))]
internal partial class GrammarResultContext : JsonSerializerContext { }
```

---

## 6. Grammar Result Parser

### `src/DiktaMe.Core/Pipeline/GrammarResultParser.cs` — NEW

Static class: `GrammarResultParser.Parse(string originalText, string llmResponse) → GrammarResult`

**Responsibilities:**
1. Strip markdown fences (`` ```json `` / `` ``` ``) from LLM output before parsing
2. Deserialize via source-generated `GrammarResultContext`
3. Map string category → `CorrectionCategory` enum (unknown → Grammar)
4. Compute offsets by scanning original text for each fragment (track consumed ranges for duplicates)
5. Build `CorrectedText` by applying all corrections to original
6. **Fallback**: If JSON parse fails, return single "Style" correction treating entire LLM output as replacement

---

## 7. LLM Prompt

### `src/DiktaMe.Core/Config/PromptDefaults.cs` — MODIFY (add after `RefineAuto` at line 64)

```csharp
public const string GrammarCheck = """
    You are a grammar checker. Analyze the text and return a JSON object with corrections.
    Rules:
    1. Identify spelling errors, grammar mistakes, and style improvements.
    2. Each correction specifies the exact original fragment and its replacement.
    3. Categorize as "spelling", "grammar", or "style".
    4. Provide a brief reason for each.
    5. If no errors, return {"corrections": []}.
    6. Return ONLY the JSON object. No markdown fences, no explanations.
    Format: {"corrections": [{"original": "teh", "corrected": "the", "category": "spelling", "reason": "Misspelled"}]}
    """;
```

Add to `GetDefault()` switch: `"grammar_check" => GrammarCheck,`

### Pipeline config: `src/DiktaMe.Core/Config/DictationModeDefaults.cs` — MODIFY

Add `grammar_check` to `CreateBuiltInUtilityPipelines()` (after `refine_auto`):
```csharp
new PipelineConfig
{
    PipelineType = "grammar_check",
    Hotkey = null, // Uses existing Refine hotkey with Auto mode
    CloudProfile = new UtilityProfile { SystemPrompt = PromptDefaults.GrammarCheck, ModelName = "gemini-2.5-flash" },
    LocalProfile = new UtilityProfile { SystemPrompt = PromptDefaults.GrammarCheck, ModelName = null },
},
```

---

## 8. Settings

### `src/DiktaMe.Core/Config/AppSettings.cs` — MODIFY

```csharp
/// <summary>Action to take when grammar issues are found.</summary>
public enum GrammarAction
{
    /// <summary>Open Grammar Popup with inline diff and per-correction accept/reject.</summary>
    Suggest = 0,
    /// <summary>Show a toast notification with issue count only.</summary>
    Toast = 1,
    /// <summary>Auto-replace text immediately (no UI). Same as old Refine Auto behavior.</summary>
    Inject = 2,
}

public sealed record GrammarSettings
{
    /// <summary>Action when user presses Ctrl+Alt+R (Refine Auto hotkey).</summary>
    public GrammarAction HotkeyAction { get; init; } = GrammarAction.Suggest;
    /// <summary>Enable passive clipboard grammar monitoring (Ctrl+C triggers analysis).</summary>
    public bool ClipboardMonitorEnabled { get; init; } = true;
    /// <summary>Action when clipboard monitor detects grammar issues.</summary>
    public GrammarAction ClipboardAction { get; init; } = GrammarAction.Toast;
    /// <summary>Minimum text length to trigger grammar check.</summary>
    public int MinTextLength { get; init; } = 10;
    /// <summary>Maximum text length for grammar check.</summary>
    public int MaxTextLength { get; init; } = 5000;
}
```

Add to `AppSettings`: `public GrammarSettings Grammar { get; init; } = new();`
Add to `SanitizeNulls()`.
Add `GrammarAction` to `AppSettingsContext` `[JsonSerializable]` attributes.

---

## 9. Grammar Popup UI

### `src/DiktaMe.App/Views/GrammarPopupWindow.xaml` + `.xaml.cs` — NEW

Follows `QuickChatWindow` pattern (standalone WinUI 3 Window).

**XAML Layout:**
```
Window
  ContentControl FontFamily="{StaticResource AppFont}"  ← REQUIRED (FontFamily on Grid = crash)
    Grid (3 rows)
      Row 0: Header — "Grammar Check" + category badges ("2 Spelling · 1 Grammar · 3 Style")
      Row 1: ScrollViewer
        StackPanel
          RichTextBlock x:Name="DiffDisplay"  ← inline diff built in code-behind
          ListView of GrammarCorrectionItem cards with CheckBox per item
      Row 2: Action bar — "Accept All" (AccentButton), "Accept Selected", "Dismiss"
```

**Inline Diff (code-behind, NOT XAML-bound):**

Built programmatically because `x:Bind` on `Run.Text` crashes the WinUI 3 XAML compiler (documented in MEMORY.md). The `BuildInlineDiff()` method:
1. Sorts corrections by `Offset` ascending
2. Walks original text, inserting `Run` elements into a `Paragraph`:
   - Unchanged text: default foreground
   - Deleted text: `TextDecorations.Strikethrough` + red (`#FF6B6B`)
   - Inserted text: green (`#4ADE80`)

**Correction card ItemTemplate:**
```
Grid: [CategoryIcon] [Original→Corrected + Reason + CategoryLabel] [CheckBox]
  - Original: TextBlock with Strikethrough + red
  - "→" separator
  - Corrected: TextBlock with green
  - Reason: small, dimmed TextBlock
  - Category badge: Border with label
  - CheckBox: bound to IsAccepted (TwoWay)
```

**Window behavior:**
- 500×600px, always-on-top (`OverlappedPresenter.IsAlwaysOnTop = true`), resizable
- Positioned near cursor via Win32 `GetCursorPos()`, clamped to display work area
- Escape = Dismiss, Enter = Accept All (via KeyboardAccelerator)

### `src/DiktaMe.App/ViewModels/GrammarPopupViewModel.cs` — NEW

Follows `QuickChatViewModel` pattern (ObservableObject, [ObservableProperty], [RelayCommand]).

```
Properties:
  - Corrections: ObservableCollection<GrammarCorrectionItem>
  - SpellingCount, GrammarCount, StyleCount (computed)
  - HasCorrections: bool

GrammarCorrectionItem (ObservableObject):
  - Correction: GrammarCorrection
  - IsAccepted: bool (default true)
  - CategoryLabel, CategoryIcon (computed — avoids x:Bind converters in Window)

Commands:
  - AcceptAll → BuildAcceptedText(all) → InjectAndClose()
  - AcceptSelected → BuildAcceptedText(accepted only) → InjectAndClose()
  - Dismiss → close, no injection

BuildAcceptedText():
  Sort accepted corrections by offset descending
  Apply replacements to original text from end to start (avoids index shifting)

InjectAndClose():
  Close popup → RestoreFocus(sourceWindow) → TextInjector.InjectText(result)
```

---

## 10. Clipboard Monitor Service

### `src/DiktaMe.Core/Input/ClipboardMonitorService.cs` — NEW

Follows `HotkeyManager` pattern: dedicated message-pump background thread with message-only HWND.

```
ClipboardMonitorService : IDisposable
  - Start() — creates background thread, message-only HWND, calls AddClipboardFormatListener
  - Stop() — removes listener, destroys HWND, joins thread
  - Event: ClipboardTextChanged(string text, IntPtr sourceWindow)
  - Message pump processes WM_CLIPBOARDUPDATE
  - On WM_CLIPBOARDUPDATE: capture foreground HWND via GetForegroundWindow()
  - Debounce: ignore changes within 500ms of last change
  - Filter: only fire for text content (CF_UNICODETEXT)
  - Ignore self: check TextInjector.IsInjecting static flag
```

Win32 APIs (P/Invoke patterns already exist in ClipboardManager.cs + HotkeyManager.cs):
- `AddClipboardFormatListener(hwnd)` / `RemoveClipboardFormatListener(hwnd)`
- `CreateWindowEx` for message-only window
- `GetForegroundWindow()` (already in TextInjector)

### `src/DiktaMe.Core/Input/GrammarNotifier.cs` — NEW

Orchestrates background grammar analysis for the clipboard path.

```
GrammarNotifier
  - Constructor(ClipboardMonitorService, PipelineFactory, PipelineConfigManager, SettingsManager, INotificationService)
  - OnClipboardTextChanged(text, sourceWindow):
    - Skip if text < MinTextLength or > MaxTextLength
    - Skip if ClipboardMonitorEnabled == false
    - Cancel any pending analysis (CancellationTokenSource swap — only latest wins)
    - Call LLM with GrammarCheck prompt on ThreadPool
    - Parse result via GrammarResultParser
    - If 0 corrections: no action
    - If corrections found, route based on Grammar.ClipboardAction:
      - Suggest: dispatch to UI thread → ShowGrammarPopup(result, sourceWindow, options)
      - Toast: ShowToast("Grammar", "{count} issues found")
      - Inject: dispatch to UI thread → RestoreFocus(sourceWindow) → TextInjector.InjectText(correctedText)
  - Stores last GrammarResult for re-use if user then hits Ctrl+Alt+R
```

### `src/DiktaMe.Core/Input/TextInjector.cs` — MODIFY

Add self-injection suppression flag:
```csharp
public static bool IsInjecting { get; private set; }
```
Set `true` at start of `InjectText()` and `CaptureSelection()`, `false` in finally blocks.
`ClipboardMonitorService` checks this flag and skips `WM_CLIPBOARDUPDATE` when true.

---

## 11. Integration Points

### `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` — MODIFY

Modify `RunRefineAutoAsync()` (line 1002-1063):
- Change pipeline type from `"refine_auto"` to `"grammar_check"`
- Get LLM via `_pipelineFactory.GetLlmProvider()` (new public method)
- Call LLM directly with grammar prompt (skip full RefinePipeline — no auto-injection needed)
- Parse response via `GrammarResultParser.Parse()`
- If 0 corrections → show toast "No corrections needed!"
- If corrections found, route based on `Grammar.HotkeyAction`:
  - **Suggest**: `App.Current.ShowGrammarPopup(result, sourceWindow, injectionOptions)`
  - **Toast**: `ShowToast("Grammar", "{count} issues found")`
  - **Inject**: `TextInjector.InjectText(result.CorrectedText)` (backward compatible)

### `src/DiktaMe.Core/Config/PipelineFactory.cs` — MODIFY

Add public method to get LLM provider without full pipeline:
```csharp
public ILLMProvider? GetLlmProvider(string modeOverride = "refine")
{
    var (_, llm) = GetProviders(modeOverride);
    return llm;
}
```

### `src/DiktaMe.App/App.xaml.cs` — MODIFY

- Add `_grammarPopupWindow` field
- Add `ShowGrammarPopup(GrammarResult, IntPtr sourceWindow, PipelineInjectionOptions)` (follows `ToggleQuickChat()` pattern at line 407)
- Register `GrammarPopupViewModel` as Transient in DI
- Initialize `ClipboardMonitorService` + `GrammarNotifier` in `OnLaunched()`, dispose on exit

### `src/DiktaMe.Core/Config/SettingsManager.cs` — MODIFY

Add migration: if `grammar_check` pipeline is missing from loaded settings, inject default (same pattern as refine_auto migration at lines 268-282).

---

## 12. WinUI 3 Gotchas (Must Follow)

These are hard-won lessons from previous development. Violating any of these causes silent crashes:

| Gotcha | Fix |
|--------|-----|
| `FontFamily` on `Grid`/`Panel` = exit code 1 crash | Wrap in `ContentControl` with FontFamily |
| `x:Bind` converters in `Window` context = CS1503 | Use computed ViewModel properties instead |
| `x:Bind` on `Run.Text` = silent XAML compiler crash | Build RichTextBlock programmatically in code-behind |
| `ThemeDictionaries` in merged XAML = exit code 1 | Place in `App.xaml` root ResourceDictionary only |
| NullReferenceException in UI thread = exit 127 | Guard ALL bindings against null |
| ObservableCollection from background thread | Must use `DispatcherQueue.TryEnqueue()` |

---

## 13. Test Plan

### Unit Tests

#### `src/DiktaMe.Core.Tests/Pipeline/GrammarResultParserTests.cs` — NEW

| Test | Asserts |
|------|---------|
| `Parse_ValidJson_ReturnsCorrections` | Correct count, categories, offsets |
| `Parse_EmptyCorrections_ReturnsEmptyList` | 0 corrections, original = corrected |
| `Parse_MalformedJson_ReturnsFallbackCorrection` | Single Style correction with LLM output |
| `Parse_DuplicateFragments_DistinctOffsets` | Each occurrence gets unique offset |
| `Parse_UnknownCategory_DefaultsToGrammar` | "foobar" → Grammar |
| `Parse_MarkdownFences_StrippedBeforeParsing` | ```json wrapper removed |
| `Parse_CorrectOffsetsFromOriginalText` | Offsets match actual positions |

#### `src/DiktaMe.Core.Tests/Pipeline/GrammarResultAcceptanceTests.cs` — NEW

| Test | Asserts |
|------|---------|
| `BuildAcceptedText_AllAccepted_ReturnsFullyCorrected` | All corrections applied |
| `BuildAcceptedText_NoneAccepted_ReturnsOriginal` | Original text unchanged |
| `BuildAcceptedText_PartialAcceptance` | Only selected corrections applied |
| `BuildAcceptedText_OverlappingOffsets_HandledCorrectly` | No index corruption |

#### `src/DiktaMe.Core.Tests/Input/ClipboardMonitorServiceTests.cs` — NEW

| Test | Asserts |
|------|---------|
| `Debounce_RapidEvents_OnlyOneCallback` | Single event after 500ms settle |
| `SelfInjection_Suppressed` | No callback when IsInjecting=true |
| `SourceWindow_CapturedOnChange` | HWND passed to event args |

#### `src/DiktaMe.Core.Tests/Input/GrammarNotifierTests.cs` — NEW

| Test | Asserts |
|------|---------|
| `MinLength_FilteredOut` | No LLM call for < 10 chars |
| `MaxLength_FilteredOut` | No LLM call for > 5000 chars |
| `RapidChanges_OnlyLatestAnalyzed` | Previous CTS cancelled |
| `Toast_WhenClipboardActionIsToast` | Toast shown with count |
| `Popup_WhenClipboardActionIsSuggest` | ShowGrammarPopup called |
| `NoAction_WhenZeroCorrections` | Neither toast nor popup |
| `Disabled_WhenClipboardMonitorOff` | No analysis at all |

### Manual E2E Tests

1. **Phase 1A — Hotkey Suggest mode:**
   - Open Notepad, type "Teh cat sitted on teh mat verry quikly"
   - Select all, press Ctrl+Alt+R
   - Verify popup appears near cursor
   - Verify inline diff: ~~Teh~~ → The, ~~sitted~~ → sat, etc.
   - Verify categories: Spelling for "Teh"/"teh"/"verry"/"quikly", Grammar for "sitted"
   - Accept "Teh"→"The" only, click "Accept Selected"
   - Verify only that one change is injected back

2. **Phase 1A — Hotkey Toast mode:**
   - Change Grammar.HotkeyAction to Toast in settings
   - Select text, press Ctrl+Alt+R
   - Verify toast appears with count, no popup

3. **Phase 1A — Hotkey Inject mode:**
   - Change Grammar.HotkeyAction to Inject
   - Select text, press Ctrl+Alt+R
   - Verify text auto-replaced immediately (old Refine Auto behavior)

4. **Phase 1B — Clipboard monitor:**
   - Copy text with errors in any app
   - Verify toast notification appears (default ClipboardAction=Toast)
   - Change ClipboardAction to Suggest, copy again
   - Verify popup auto-opens with analysis

5. **Self-injection test:**
   - Dictate text via Ctrl+Alt+D
   - Verify clipboard monitor does NOT fire during injection (no spurious toast)

---

## 14. Dependency Order

| Step | File | Action | Phase |
|------|------|--------|-------|
| 1 | `DiktaMe.Core/Pipeline/GrammarResult.cs` | CREATE | 1A |
| 2 | `DiktaMe.Core/Pipeline/GrammarResultParser.cs` | CREATE | 1A |
| 3 | `DiktaMe.Core/Config/PromptDefaults.cs` | MODIFY | 1A |
| 4 | `DiktaMe.Core/Config/DictationModeDefaults.cs` | MODIFY | 1A |
| 5 | `DiktaMe.Core/Config/AppSettings.cs` | MODIFY (GrammarSettings + enum) | 1A+1B |
| 6 | `DiktaMe.Core/Config/SettingsManager.cs` | MODIFY (migration) | 1A |
| 7 | `DiktaMe.Core/Config/PipelineFactory.cs` | MODIFY (GetLlmProvider) | 1A |
| 8 | `DiktaMe.Core.Tests/Pipeline/GrammarResultParserTests.cs` | CREATE | 1A |
| 9 | `DiktaMe.App/ViewModels/GrammarPopupViewModel.cs` | CREATE | 1A |
| 10 | `DiktaMe.App/Views/GrammarPopupWindow.xaml` | CREATE | 1A |
| 11 | `DiktaMe.App/Views/GrammarPopupWindow.xaml.cs` | CREATE | 1A |
| 12 | `DiktaMe.App/App.xaml.cs` | MODIFY (DI + ShowGrammarPopup) | 1A |
| 13 | `DiktaMe.App/ViewModels/LoadingViewModel.cs` | MODIFY (RunRefineAutoAsync) | 1A |
| 14 | `DiktaMe.Core/Input/TextInjector.cs` | MODIFY (IsInjecting flag) | 1B |
| 15 | `DiktaMe.Core/Input/ClipboardMonitorService.cs` | CREATE | 1B |
| 16 | `DiktaMe.Core/Input/GrammarNotifier.cs` | CREATE | 1B |
| 17 | `DiktaMe.App/App.xaml.cs` | MODIFY (start clipboard monitor) | 1B |
| 18 | `DiktaMe.Core.Tests/Input/ClipboardMonitorServiceTests.cs` | CREATE | 1B |
| 19 | `DiktaMe.Core.Tests/Input/GrammarNotifierTests.cs` | CREATE | 1B |

**New files:** 9
**Modified files:** 8
**Estimated new tests:** ~30

---

## 15. Commit Plan

```
feat(core): add GrammarResult models and GrammarResultParser [SPEC_016.1A]
feat(core): add GrammarCheck prompt and grammar_check pipeline config [SPEC_016.1A]
feat(core): add GrammarSettings, GrammarAction enum, and settings migration [SPEC_016.1A]
feat(core): expose PipelineFactory.GetLlmProvider() for grammar check [SPEC_016.1A]
test(core): add GrammarResultParser and acceptance tests [SPEC_016.1A]
feat(ui): add GrammarPopupWindow with inline diff and correction cards [SPEC_016.1A]
feat(app): wire RunRefineAutoAsync to grammar check with Suggest/Toast/Inject routing [SPEC_016.1A]
feat(core): add ClipboardMonitorService with WM_CLIPBOARDUPDATE listener [SPEC_016.1B]
feat(core): add GrammarNotifier for passive clipboard grammar analysis [SPEC_016.1B]
feat(core): add TextInjector.IsInjecting self-injection suppression flag [SPEC_016.1B]
feat(app): wire ClipboardMonitorService and GrammarNotifier in App startup [SPEC_016.1B]
test(core): add ClipboardMonitorService and GrammarNotifier tests [SPEC_016.1B]
```

---

*End of SPEC_016*
