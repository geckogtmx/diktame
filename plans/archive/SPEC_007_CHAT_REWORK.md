# SPEC_007: Chat Feature Rework

> **Status:** DRAFT
> **Date:** 2026-03-08
> **Priority:** Medium — transforms a throwaway feature into a retention driver
> **Parent Specs:** `DEVELOPMENT_ROADMAP.md`

---

## 1. Problem Statement

The current Chat feature is a stateless text box. It has:

- A 420×340 fixed-size window with no visual distinction between user and AI messages
- A hardcoded system prompt: `"You are a helpful assistant. Answer concisely."`
- No conversation history sent to the LLM — each message is independent
- No model selection — uses whatever the global profile defaults to
- No persistence — messages lost when the window closes
- No way to act on responses (copy, inject, export)

This makes it faster to open a browser tab and use any web-based AI chat. The hotkey invocation speed is the only advantage, and it's not enough.

### What "Sticky" Means

Research across ChatGPT Desktop, Claude Desktop, Raycast AI, Cursor, Jan.ai, and Obsidian AI plugins shows three things that make desktop AI chat stick:

1. **Invocation speed** — hotkey to first keystroke in <1s. ✅ Already have this.
2. **Local context the browser can't access** — clipboard, screen, files. ❌ Missing.
3. **Action on output** — inject at cursor, copy to clipboard. ❌ Missing.

Plus the basics: multi-turn conversation, model selection, customizable system prompts, persistence.

---

## 2. What Good Looks Like

A user presses the Chat hotkey. A resizable window appears. They see their last conversation, or a fresh one. They pick a model from a dropdown. They type a question. The AI responds with proper visual formatting. They press a button to inject the response at their cursor in Word/VS Code/whatever. They close the window. Tomorrow they reopen and continue the same conversation.

That's it. No RAG, no tool use, no streaming, no voice. Just a fast, persistent, context-aware chat that acts on its output.

---

## 3. Current Architecture

| Component | File | What It Does |
|-----------|------|--------------|
| QuickChatWindow | `src/DiktaMe.App/Views/QuickChatWindow.xaml` | 420×340 always-on-top Window, ListView + TextBox |
| QuickChatWindow.cs | `src/DiktaMe.App/Views/QuickChatWindow.xaml.cs` | Code-behind: Enter=send, Escape=close, scroll-to-bottom |
| QuickChatViewModel | `src/DiktaMe.App/ViewModels/QuickChatViewModel.cs` | 84 lines. `ObservableCollection<ChatMessage>`, hardcoded prompt, calls `ChatPipeline` per message |
| ChatMessage | `src/DiktaMe.App/ViewModels/QuickChatViewModel.cs:86` | `record(string Text, bool IsUser)` — no timestamp, no metadata |
| ChatPipeline | `src/DiktaMe.Core/Pipeline/ChatPipeline.cs` | Text or voice → LLM → PipelineResult. Single-turn only. |
| ChatOptions | `src/DiktaMe.Core/Pipeline/ChatPipeline.cs` | `SystemPrompt`, `ModelName?`, `TextInput?`, `AudioFilePath?`, `Language` |
| App.ToggleQuickChat() | `src/DiktaMe.App/App.xaml.cs:358-369` | Creates/destroys window on every toggle |
| ChatSettings | `src/DiktaMe.Core/Config/AppSettings.cs:235-257` | FontSize, ForgetOnClose, MaxHistoryMessages, WindowOpacity, ShowTimestamps, EnableMarkdown, Theme — **mostly unwired** |
| ILLMProvider | `src/DiktaMe.Core/LLM/ILLMProvider.cs` | `ProcessAsync(text, systemPrompt, mode, ct)` — single text input only |
| LLMRouter | `src/DiktaMe.Core/LLM/LLMRouter.cs` | Primary/fallback/trial routing, model override via `ProcessAsync(text, prompt, modelName, mode, ct)` |
| ModelListService | `src/DiktaMe.Core/LLM/ModelListService.cs` | `GetAvailableModelsAsync()` → `List<ModelInfo>` from all providers |
| ModelInfo | `src/DiktaMe.Core/LLM/ModelInfo.cs` | `ModelId`, `DisplayName`, `Provider`, `IsAvailable`, `ContextWindow?` |

### Providers (all implement `ILLMProvider`)

| Provider | API Format | Multi-turn Support |
|----------|-----------|-------------------|
| OpenAICompatibleProvider | `{"messages":[{role,content}]}` | Native — messages array is already structured for it |
| AnthropicProvider | `{"system":"...", "messages":[...]}` | Native — same |
| GeminiProvider | `{"contents":[{role,parts}]}` | Native via multiple contents entries |
| OllamaProvider | `{"prompt":"..."}` via `/api/generate` | **Needs switch to `/api/chat`** which uses messages array |
| NullLlmProvider | No-op | N/A |
| TrialGeminiProvider | Managed proxy | Forwards to Gemini |

---

## 4. Architecture Changes

### 4.1 Multi-Turn: Extend `ILLMProvider`

Add to `ILLMProvider`:

```csharp
/// <summary>
/// Processes a multi-turn conversation through the LLM.
/// </summary>
Task<LlmResult> ProcessConversationAsync(
    IReadOnlyList<ConversationTurn> history,
    string systemPrompt,
    string mode = "chat",
    CancellationToken cancellationToken = default);
```

New record in the same file:

```csharp
/// <summary>A single turn in a conversation.</summary>
/// <param name="Role">"user" or "assistant"</param>
/// <param name="Content">The message text.</param>
public sealed record ConversationTurn(string Role, string Content);
```

**Why modify `ILLMProvider` instead of a new interface?** Cleaner — one interface, one contract. The test impact is accepted. All providers need this eventually. A default interface method returning `NotSupportedException` would add dead code; better to implement it in each provider now.

**Provider implementations:**

- **OpenAICompatibleProvider**: New `BuildConversationRequestJson` that builds `{"messages": [{"role":"system","content":"..."},{"role":"user","content":"msg1"},{"role":"assistant","content":"msg2"},...]}`. Reuses existing `ParseResponse`, `SanitizeInput`, retry logic.
- **AnthropicProvider**: `{"system":"...", "messages":[{"role":"user","content":"msg1"},{"role":"assistant","content":"msg2"},...]}`. Anthropic already separates system from messages.
- **GeminiProvider**: `{"systemInstruction":{"parts":[{"text":"..."}]}, "contents":[{"role":"user","parts":[{"text":"msg1"}]},{"role":"model","parts":[{"text":"msg2"}]},...]}`. Note: Gemini uses "model" not "assistant".
- **OllamaProvider**: Switch to `/api/chat` endpoint: `{"model":"...", "messages":[{"role":"system","content":"..."},{"role":"user","content":"msg1"},{"role":"assistant","content":"msg2"},...], "stream": false}`. Keep `/api/generate` for single-turn `ProcessAsync` (backward compatible).
- **NullLlmProvider**: Return empty `LlmResult`.
- **TrialGeminiProvider**: Forward conversation to managed proxy.

**LLMRouter**: Add `ProcessConversationAsync` with the same primary/fallback/trial routing logic as `ProcessAsync`. Add model-override overload: `ProcessConversationAsync(history, prompt, modelName, mode, ct)`.

**Extension method**: Add `ProcessConversationWithModelAsync` on `ILLMProvider` (mirrors existing `ProcessWithModelAsync` pattern).

### 4.2 Conversation Persistence: `ConversationManager`

New class in `DiktaMe.Core.Data`, using the same `%APPDATA%\DiktaMe\history.db` SQLite database (shared with `HistoryManager`).

**Schema:**

```sql
CREATE TABLE IF NOT EXISTS conversations (
    id              TEXT PRIMARY KEY,        -- GUID string
    title           TEXT,                    -- null until auto-titled
    system_prompt   TEXT NOT NULL,
    model_id        TEXT,                    -- last used model
    created_at      INTEGER NOT NULL,        -- unix timestamp
    updated_at      INTEGER NOT NULL,        -- for sorting
    message_count   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS conversation_messages (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    conversation_id TEXT NOT NULL,
    role            TEXT NOT NULL,            -- 'user' or 'assistant'
    content         TEXT NOT NULL,
    tokens          INTEGER,                 -- approximate, null if unknown
    created_at      INTEGER NOT NULL,
    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_conv_msg_conv ON conversation_messages(conversation_id);
CREATE INDEX IF NOT EXISTS idx_conv_updated ON conversations(updated_at DESC);
```

**Data records** (`ConversationRecord.cs`):

```csharp
public sealed record ConversationRecord(
    string Id,
    string? Title,
    string SystemPrompt,
    string? ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);

public sealed record ConversationMessageRecord(
    long Id,
    string ConversationId,
    string Role,
    string Content,
    int? Tokens,
    DateTimeOffset CreatedAt);
```

**Methods:**

```
InitAsync(ct)                                          -- CREATE TABLE IF NOT EXISTS
CreateAsync(systemPrompt, modelId?, ct) → ConversationRecord
GetAsync(id, ct) → ConversationRecord?
GetMessagesAsync(conversationId, ct) → List<ConversationMessageRecord>
AddMessageAsync(conversationId, role, content, tokens?, ct)
UpdateTitleAsync(conversationId, title, ct)
UpdateModelAsync(conversationId, modelId, ct)
DeleteAsync(conversationId, ct)
GetRecentAsync(limit=50, ct) → List<ConversationRecord>
DeleteAllAsync(ct)
```

**Privacy:**
- `Ghost`: Don't persist at all. In-memory only. `CreateAsync` returns a record with an ID but doesn't write to SQLite. `GetRecentAsync` returns empty.
- `Stats`: Persist metadata (title, model, timestamps, message count) but not message content. `AddMessageAsync` stores role and token count, content is empty string.
- `Balanced`: Persist with optional PII scrubbing via `PIIScrubber.Scrub()`.
- `Full`: Persist verbatim.

**Thread safety:** `SemaphoreSlim(1,1)` for async concurrency (same pattern as `HistoryManager`).

**Separate connection:** Use its own `SqliteConnection` to the same db file. SQLite handles multi-connection safely with WAL mode. This avoids coupling `ConversationManager` to `HistoryManager`.

### 4.3 Context Window Management

Simple truncation strategy:

1. Get `ContextWindow` from `ModelInfo` for the selected model (via `ModelListService`). Default to 8192 if unknown.
2. Reserve 20% for response generation → budget = `ContextWindow * 0.8`.
3. Estimate tokens: `chars / 4` (good enough for English; CJK would need `chars / 1.5` but that's a future refinement).
4. Always keep the system prompt.
5. Keep messages from newest to oldest until the budget is exhausted. Drop oldest messages first.
6. No tiktoken dependency. No external tokenizer.

### 4.4 ChatPipeline Multi-Turn

Add an overload to `ChatPipeline`:

```csharp
public Task<PipelineResult> RunAsync(
    ChatOptions options,
    IReadOnlyList<ConversationTurn> history,
    CancellationToken cancellationToken = default)
```

This overload calls `_llm.ProcessConversationWithModelAsync(history, options.SystemPrompt, options.ModelName, "chat", ct)` instead of the single-turn `ProcessWithModelAsync`.

The existing single-turn `RunAsync(ChatOptions, ct)` remains unchanged for backward compatibility.

### 4.5 Model Selection

- Per-conversation, stored on the conversation record (`model_id` column).
- Global default from new `ChatSettings.DefaultModelId` property (null = use profile default).
- New conversation inherits the global default.
- Model dropdown populated from `ModelListService.GetAvailableModelsAsync()` on window open.
- Changing model mid-conversation updates the conversation record and applies to subsequent messages only.

### 4.6 Auto-Title

After the assistant responds to the 2nd user message in a conversation:

1. Build a small prompt: `"Generate a 3-5 word title for this conversation. Output only the title, nothing else."`
2. Include the first 2 user messages and first 2 assistant responses as context.
3. Fire-and-forget LLM call using the same model.
4. On success, `ConversationManager.UpdateTitleAsync(conversationId, title)`.
5. On failure, leave title null (UI shows "Untitled conversation").

Don't re-title if a title already exists (user might have edited the initial auto-title).

### 4.7 ChatSettings Extension

Add to existing `ChatSettings` record in `AppSettings.cs`:

```csharp
/// <summary>Default model for new conversations (null = use profile default).</summary>
public string? DefaultModelId { get; init; }

/// <summary>Last window width in pixels.</summary>
public double WindowWidth { get; init; } = 600;

/// <summary>Last window height in pixels.</summary>
public double WindowHeight { get; init; } = 500;

/// <summary>Whether the chat window stays on top of other windows.</summary>
public bool AlwaysOnTop { get; init; } = true;
```

Existing properties (`FontSize`, `ForgetOnClose`, `MaxHistoryMessages`, `WindowOpacity`, `ShowTimestamps`, `EnableMarkdown`, `Theme`) remain and will be wired up.

---

## 5. UI Design

### 5.1 Window Layout

```
+----------------------------------------------------------+
| [Model ▾]  "Conversation Title"              [+ New] [⚙] |  ← toolbar
+----------------------------------------------------------+
|                                                            |
|                    Hey, can you help me     12:04 PM  ←user|  ← right-aligned, accent bg
|                    with a regex?                           |
|                                                            |
| Of course! What pattern are you trying to     ←assistant   |  ← left-aligned, surface bg
| match? Here's a quick example:                [📋] [↗]    |
|                                                            |
| ```                                                        |
| \d{3}-\d{4}                                                |
| ```                                                        |
|                                                            |
|  ⟳ Thinking...                                             |  ← typing indicator
|                                                            |
+----------------------------------------------------------+
| [📎]  [________________input________________]        [▶]  |  ← input row
+----------------------------------------------------------+
```

### 5.2 Message Templates

Use a `DataTemplateSelector` — two templates based on `ChatMessageViewModel.IsUser`:

**User messages:**
- Right-aligned with `HorizontalAlignment="Right"`
- Accent color background, white text
- Rounded corners (8px), max width 80% of ListView
- Optional timestamp (`ChatSettings.ShowTimestamps`)

**Assistant messages:**
- Left-aligned with `HorizontalAlignment="Left"`
- Subtle surface background (e.g., `CardBackgroundFillColorDefaultBrush`)
- Full width
- Copy button (📋) and Inject button (↗) appear on hover/focus
- Optional timestamp

**Typing indicator:**
- Left-aligned like assistant messages
- `ProgressRing` (16px) + "Thinking..." text
- Visible when `IsBusy` is true

### 5.3 Window Behavior

- **Resizable**: `MinWidth=500`, `MinHeight=400`, default 600×500
- **Persist size**: Save to `ChatSettings.WindowWidth`/`WindowHeight` on close
- **Always-on-top**: Controlled by `ChatSettings.AlwaysOnTop`, toggleable via ⚙ menu
- **Show/hide, not create/destroy**: `ToggleQuickChat()` hides/shows the window instead of creating a new one. This preserves ViewModel state and conversation context.
- **Keyboard shortcuts**:
  - `Enter`: send message
  - `Escape`: hide window
  - `Ctrl+N`: new conversation
  - `Ctrl+L`: clear current conversation
  - `Up arrow` (in empty input): recall last sent message

### 5.4 System Prompt Editor

- Small button (⚙) in the toolbar opens a collapsible TextBox below the toolbar
- Pre-populated from the Chat pipeline config system prompt
- Saved per-conversation on the conversation record
- Changes take effect on the next message (not retroactive)

### 5.5 Conversation List (Phase 2)

- Button in toolbar opens a flyout/panel showing recent conversations (50 max)
- Each entry: title (or "Untitled") + date + message count
- Click to load, swipe/button to delete
- No search/filter — keep it simple

---

## 6. Implementation Phases

### Phase 1: Core Conversation

**Task 1.1: ConversationManager + Records**
- Create `src/DiktaMe.Core/Data/ConversationManager.cs`
- Create `src/DiktaMe.Core/Data/ConversationRecord.cs`
- Follow `HistoryManager.cs` patterns
- Tests: `tests/DiktaMe.Core.Tests/Data/ConversationManagerTests.cs`

**Task 1.2: ILLMProvider Multi-Turn**
- Modify `src/DiktaMe.Core/LLM/ILLMProvider.cs` — add `ProcessConversationAsync` + `ConversationTurn`
- Modify providers: `OpenAICompatibleProvider`, `AnthropicProvider`, `GeminiProvider`, `OllamaProvider`, `NullLlmProvider`, `TrialGeminiProvider`
- Modify `LLMRouter.cs` — conversation routing
- Add `ProcessConversationWithModelAsync` extension
- Tests: `tests/DiktaMe.Core.Tests/LLM/ConversationalProviderTests.cs`

**Task 1.3: ChatPipeline Multi-Turn**
- Modify `src/DiktaMe.Core/Pipeline/ChatPipeline.cs` — multi-turn overload with context truncation
- Update `tests/DiktaMe.Core.Tests/Pipeline/ChatPipelineTests.cs`

**Task 1.4: ChatSettings Extension**
- Modify `src/DiktaMe.Core/Config/AppSettings.cs` — add `DefaultModelId`, `WindowWidth`, `WindowHeight`, `AlwaysOnTop`

**Task 1.5: ViewModel Rewrite**
- Create `src/DiktaMe.App/ViewModels/ChatMessageViewModel.cs` — `Id`, `Text`, `IsUser`, `Timestamp`, `TokenCount`
- Rewrite `src/DiktaMe.App/ViewModels/QuickChatViewModel.cs`:
  - New deps: `ConversationManager`, `ModelListService`, `SettingsManager`
  - State: `_currentConversationId`, `SelectedModelId`, `SystemPrompt`, `ConversationTitle`, `AvailableModels`
  - Commands: `SendAsync`, `NewConversationAsync`, `CopyMessage`, `LoadModelsAsync`, `LoadConversationAsync`, `DeleteConversationAsync`
  - Auto-title after 2nd exchange

**Task 1.6: XAML Rewrite**
- Rewrite `src/DiktaMe.App/Views/QuickChatWindow.xaml` — toolbar, template selector, resizable
- Modify `src/DiktaMe.App/Views/QuickChatWindow.xaml.cs` — keyboard shortcuts, size persistence

**Task 1.7: App Integration**
- Modify `src/DiktaMe.App/App.xaml.cs`:
  - Register `ConversationManager` as singleton
  - Initialize alongside `HistoryManager`
  - Change `ToggleQuickChat()` to show/hide pattern
  - Update DI registration for `QuickChatViewModel`

### Phase 2: Context & Action

**Task 2.1: Clipboard Context**
- Clipboard attach button (📎) in input row
- Reads `ClipboardManager.GetText()`, prepends as context block to next message
- Badge showing attachment status, clears after send

**Task 2.2: Inject Response**
- Inject button (↗) on assistant messages
- Calls `TextInjector.InjectText()` — already built and tested
- Only shown on assistant messages

**Task 2.3: Conversation Sidebar**
- Flyout or `SplitView` showing recent conversations
- Title + date + message count per entry
- Click to load, delete button per entry

**Task 2.4: Token Count Display**
- Status area showing approximate token usage for current conversation
- Populated from `LlmResult.InputTokens`/`OutputTokens`, fallback `chars / 4`

### Phase 3: Polish

**Task 3.1: Markdown Rendering**
- Add `CommunityToolkit.WinUI.UI.Controls.Markdown` 7.1.2 NuGet
- Replace `TextBlock` in assistant template with `MarkdownTextBlock`
- Controlled by `ChatSettings.EnableMarkdown`
- Fallback: plain `TextBlock` with monospace for ``` blocks if package causes issues

**Task 3.2: Additional Keyboard Shortcuts**
- `Ctrl+N`: new conversation
- `Ctrl+L`: clear messages
- `Up arrow` in empty input: recall last sent message

**Task 3.3: Export as Markdown**
- Export button/menu → `FileSavePicker`
- Writes `## User\n{text}\n\n## Assistant\n{text}` format

---

## 7. What Is Explicitly Cut

| Feature | Reason |
|---------|--------|
| Streaming responses | Low real-world value. "Flashy but useless." Deferred indefinitely. |
| Conversation branching | Analysis paralysis, no clear UX benefit. |
| Voice input in Chat | Ask pipeline handles voice questions. Don't duplicate. |
| RAG / knowledge base | Massive scope. Not dIKta.me's lane. |
| MCP / tool use / function calling | Claude Desktop territory. |
| Model comparison side-by-side | Niche, high UI complexity. |
| Message editing / regeneration | Introduces conversation tree complexity. |
| File/image attachments | Requires multimodal provider support. |
| New Chat Settings page | Wire settings in existing Modes page. |
| Multi-window chat | One window, switch conversations within it. |
| Screenshot context | Deferred — needs per-provider vision capability check. |

---

## 8. Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| `ILLMProvider` change breaks existing tests | Medium | Implement `ProcessConversationAsync` in all providers before changing tests. Most test mocks only use `ProcessAsync`. |
| Ollama `/api/chat` behaves differently from `/api/generate` | Low | Keep `/api/generate` for single-turn. Only use `/api/chat` for conversations. Test with multiple Ollama models. |
| Markdown package causes XAML compiler or trim issues | Medium | Phase 3, not blocking. Fall back to plain TextBlock with monospace code blocks. Test in Release build early. |
| SQLite concurrent access (HistoryManager + ConversationManager) | Low | Separate connections, WAL mode handles this. Same db file is fine. |
| `DataTemplateSelector` + `x:Bind` compatibility in WinUI 3 | Medium | Known to work. Fallback: single template with converters. |
| Token estimation `chars/4` inaccurate for CJK | Low | Add language-aware multiplier later if needed. Not critical for Phase 1. |

---

## Appendix: Key Code References

| Component | Path | Role |
|-----------|------|------|
| ILLMProvider | `src/DiktaMe.Core/LLM/ILLMProvider.cs` | Interface to extend with `ProcessConversationAsync` |
| OpenAICompatibleProvider | `src/DiktaMe.Core/LLM/OpenAICompatibleProvider.cs` | Reference implementation for multi-turn JSON |
| AnthropicProvider | `src/DiktaMe.Core/LLM/AnthropicProvider.cs` | Anthropic messages API format |
| GeminiProvider | `src/DiktaMe.Core/LLM/GeminiProvider.cs` | Gemini contents/systemInstruction format |
| OllamaProvider | `src/DiktaMe.Core/LLM/OllamaProvider.cs` | Needs `/api/generate` → `/api/chat` switch |
| LLMRouter | `src/DiktaMe.Core/LLM/LLMRouter.cs` | Routing logic to replicate for conversations |
| NullLlmProvider | `src/DiktaMe.Core/LLM/NullLlmProvider.cs` | No-op implementation |
| TrialGeminiProvider | `src/DiktaMe.Core/LLM/TrialGeminiProvider.cs` | Trial proxy forwarding |
| ModelListService | `src/DiktaMe.Core/LLM/ModelListService.cs` | `GetAvailableModelsAsync()` for model dropdown |
| ModelInfo | `src/DiktaMe.Core/LLM/ModelInfo.cs` | `ModelId`, `DisplayName`, `Provider`, `ContextWindow?` |
| ChatPipeline | `src/DiktaMe.Core/Pipeline/ChatPipeline.cs` | Pipeline to extend with conversation overload |
| ChatPipelineTests | `tests/DiktaMe.Core.Tests/Pipeline/ChatPipelineTests.cs` | Existing tests to update |
| AppSettings / ChatSettings | `src/DiktaMe.Core/Config/AppSettings.cs:235-257` | Settings to extend |
| HistoryManager | `src/DiktaMe.Core/Data/HistoryManager.cs` | SQLite pattern to follow |
| QuickChatViewModel | `src/DiktaMe.App/ViewModels/QuickChatViewModel.cs` | ViewModel to rewrite |
| QuickChatWindow | `src/DiktaMe.App/Views/QuickChatWindow.xaml` | XAML to rewrite |
| QuickChatWindow.cs | `src/DiktaMe.App/Views/QuickChatWindow.xaml.cs` | Code-behind to update |
| App.ToggleQuickChat | `src/DiktaMe.App/App.xaml.cs:358-369` | Window lifecycle to change |
| TextInjector | `src/DiktaMe.Core/Input/TextInjector.cs` | For inject-at-cursor feature |
| ClipboardManager | `src/DiktaMe.Core/Input/ClipboardManager.cs` | For clipboard context feature |
| PIIScrubber | `src/DiktaMe.Core/Security/PIIScrubber.cs` | For privacy-aware persistence |
