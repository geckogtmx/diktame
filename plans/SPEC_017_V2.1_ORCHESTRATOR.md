# SPEC_017: Voice Orchestrator — "Chaviz"

> **Status:** IDEATION  
> **Date:** 2026-03-23  
> **Goal:** Add a conversational voice agent to dIKta.me — **Chaviz** (rhymes with Jarvis; nod to Chávez, the quintessential bureaucrat). A session-based orchestrator that can answer questions, query system data, trigger tools, and hold multi-turn voice conversations with personality.  
> **Architecture:** Plugin Module (`DiktaMe.Plugin.Orchestrator`) — hot-pluggable, developed independently, coordinates with other plugins via `PipelineEventBus`.  
> **Related Specs:**  
> - [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) — Plugin architecture framework (Plugin 4: Orchestrator)  
> - [`SPEC_015__MODELS_LFM2.5.md`](SPEC_015__MODELS_LFM2.5.md) — LFM 2.5 Audio as the voice engine  
> - [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](SPEC_013_CONNECTORS_IMPLEMENTATION.md) — Connectors plugin provides callable tools  
> - [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md) — Memory plugin provides contextual recall  
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — Meetings plugin provides session control tools  

---

## 1. Vision

dIKta.me today is a **transactional tool** — press hotkey, speak, get text. The orchestrator transforms it into a **conversational agent** — press hotkey, have a back-and-forth dialogue where Dikta can understand intent, execute actions, answer questions about your data, and respond with voice.

Think Jarvis, but scoped and bilingual: **Chaviz** isn't trying to be a general-purpose AGI. It's a **system-aware assistant** that knows what dIKta.me can do and acts as a natural language interface to its capabilities. The name works in both languages — Jarvis for the English ear, Chávez for the Spanish ear.

### What It Is
- A voice-in, voice-out conversational interface
- A tool-calling orchestrator that dispatches to existing services
- Push-to-talk activated (hotkey), not always-listening
- Multi-turn: maintains conversation context within a session
- Personality-aware: configurable persona and voice

### What It Isn't
- Not a replacement for existing pipelines (Dictate, Refine, Ask, etc.)
- Not always-listening (no wake-word, no ambient mic)
- Not a general-purpose chatbot — it's system-focused
- Not cloud-dependent — should work fully local
- Not tightly coupled to other modules — coordinates via `PipelineEventBus`

### Plugin Module Identity

Chaviz is **Plugin 4** in the dIKta.me module architecture (alongside Connectors, Meetings, and Memory). It follows the exact same patterns established in `SPEC_015_MODULES_SPRINT.md`:

```
Project: DiktaMe.Plugin.Orchestrator
Type: Class library (net8.0-windows10.0.19041.0, UseWinUI=true)
References: DiktaMe.Plugin.Abstractions, DiktaMe.Core
Output: plugins/Orchestrator/DiktaMe.Plugin.Orchestrator.dll
Settings file: %APPDATA%/DiktaMe/plugins/orchestrator-settings.json
Tests: DiktaMe.Plugin.Orchestrator.Tests
```

**Critical rule** (from SPEC_015): Plugins NEVER depend on each other. All cross-plugin flows go through `PipelineEventBus`:

```
┌───────────────────────────────────────────────────────────────────────────┐
│                        dIKta.me Core App                                 │
│  Pipeline, LLM, STT, TTS, Audio, Settings, History, Security            │
│  PipelineEventBus  │  PluginManager  │  PluginUIRegistry                │
└──────┬─────────────┴──────┬──────────┴──────┬──────────────┬────────────┘
       │                    │                 │              │
       ▼                    ▼                 ▼              ▼
┌──────────────┐  ┌────────────────┐  ┌─────────────┐  ┌───────────────────┐
│ CONNECTORS   │  │ MEETINGS       │  │ MEMORY      │  │ ORCHESTRATOR      │
│ Plugin 1     │  │ Plugin 2       │  │ Plugin 3    │  │ Plugin 4 (Chaviz) │
│              │  │                │  │             │  │                   │
│ IConnector   │  │ SessionManager │  │ IMemoryLayer│  │ OrchestratorSvc   │
│ Presets      │  │ ScribeWindow   │  │ Embeddings  │  │ IOrchestratorLLM  │
│ Destinations │  │ Transcriber    │  │ SQLite+VSS  │  │ Tool Registry     │
│              │  │                │  │             │  │ AudioSession      │
│ Hooks:       │  │ Hooks:         │  │ Hooks:      │  │ Persona Config    │
│ • OnCompleted│  │ • OnState      │  │ • OnComplete│  │                   │
│   (dispatch) │  │   Changed      │  │   (store)   │  │ Hooks:            │
│              │  │                │  │ • BeforeLlm │  │ • OnCompleted     │
│              │  │                │  │   (inject)  │  │   (log sessions)  │
└──────────────┘  └────────────────┘  └─────────────┘  └───────────────────┘
       │                    │                 │              │
       └────────────────────┴─────────────────┴──────────────┘
                         Zero direct dependencies
                      All flows via PipelineEventBus
```

**How Chaviz coordinates with other plugins (all via Core, never direct):**

| Interaction | Mechanism | Direct Dependency? |
|---|---|---|
| Chaviz calls `check_email` tool | Tool resolves `IConnector` from Core DI (if Connectors plugin is enabled) | ❌ No — tool gracefully fails if Connectors plugin absent |
| Chaviz calls `recall` tool | Tool resolves `IMemoryLayer` from Core DI (if Memory plugin is enabled) | ❌ No — tool returns "memory not available" if absent |
| Chaviz says "start recording" | Tool resolves `SessionManager` from Core DI (if Meetings plugin is enabled) | ❌ No — tool returns "meetings not available" if absent |
| Memory enriches Chaviz conversations | Memory subscribes to `OnBeforeLlmProcessing` on event bus — injects context | ❌ No — Memory doesn't know Chaviz exists |
| Chaviz conversations stored in memory | Chaviz publishes `PipelineResult` to event bus — Memory stores embedding | ❌ No — Chaviz doesn't know Memory exists |

---

## 2. Interaction Model

### 2.1 The Conversation Loop

```
                    ┌─────────────────────────┐
                    │    IDLE (Dormant)        │
                    │    Waiting for hotkey    │
                    └────────┬────────────────┘
                             │ [Orchestrator Hotkey]
                             ▼
                    ┌─────────────────────────┐
                    │    LISTENING             │
                    │    Mic open, capturing   │
                    │    user utterance        │
                    └────────┬────────────────┘
                             │ [Pause detected / silence threshold]
                             ▼
                    ┌─────────────────────────┐
                    │    THINKING             │
                    │    STT → LLM (w/ tools) │
                    │    → tool execution     │
                    └────────┬────────────────┘
                             │ [Response ready]
                             ▼
                    ┌─────────────────────────┐
                    │    SPEAKING              │
                    │    TTS response playback │
                    │    (can be interrupted)  │
                    └────────┬────────────────┘
                             │
                    ┌────────┴────────────────┐
                    │                         │
                    ▼                         ▼
           [Hotkey again]              [Timeout / Dismiss]
           → LISTENING                 → IDLE
           (multi-turn)               (session ends)
```

### 2.2 Activation — Session-Based Open Mic

The orchestrator uses a **session toggle** model — not per-utterance activation:

1. **Press hotkey** → Session opens. Mic is continuously live.
2. **Speak naturally** → VAD detects speech, captures utterance, detects pause → processes.
3. **Chaviz responds** via TTS → Mic stays open, listening for the next utterance.
4. **Continue talking** → Next utterance captured automatically. No re-pressing needed.
5. **End session**: Press hotkey again, say "that's all", or let it timeout.

| Activation | Behavior |
|------------|----------|
| **Hotkey (toggle)** | First press opens session. Second press closes it. |
| **UI Button** | Click mic button in orchestrator panel — same toggle behavior. |
| **Timeout** | Session auto-closes after N seconds of silence (default: 30s, configurable). |
| **Verbal dismiss** | "That's all" / "Thanks Chaviz" / "Goodbye" → session closes gracefully. |

> **Not always-on**: The mic is only active during a user-initiated session. When the session is closed, zero audio capture. This preserves privacy and resources — the user explicitly starts and stops the conversation.

### 2.3 End-of-Utterance Detection (VAD)

After the user activates the mic, the system needs to detect when they've finished speaking:

| Strategy | Latency | Complexity | Recommendation |
|----------|---------|------------|----------------|
| **Silence threshold** | ~1-1.5s after last speech | Low | ✅ Start here |
| **Silero VAD** (ONNX) | Real-time | Medium | Phase 2 upgrade |
| **LFM 2.5 native turn-taking** | Minimal | Model-dependent | Phase 3 if using LFM |

**Phase 1**: Simple energy-based silence detection — if audio energy drops below threshold for 1.2 seconds after speech was detected, utterance is complete. This reuses logic similar to what `AudioRecorder` already does for max-silence cutoff.

### 2.4 Multi-Turn Conversation

Because the mic stays open for the entire session, multi-turn conversation is natural:

```
[User presses Ctrl+Alt+J — session opens]

User: "How many dictations did I do today?"
  → [VAD: speech detected → 1.2s silence → utterance complete]
  → [STT → LLM (tool call) → TTS response]
Chaviz: "You've done 14 dictations today, totaling about 2,300 words."
  → [Mic stays open, listening...]

User: "What about this week?"
  → [VAD captures next utterance automatically]
Chaviz: "This week you've done 67 dictations — that's up 15% from last week."

User: "Switch me to Professional mode."
Chaviz: "Done. You're now in Professional mode with cloud STT."

[30 seconds of silence → session auto-closes]
— OR —
[User presses Ctrl+Alt+J again → session closes immediately]
```

During a session, the LLM maintains conversation history (in-memory `List<ChatMessage>`). The `OrchestratorAudioSession` manages the continuous mic stream and VAD segmentation.

---

## 3. Tool System

The orchestrator's power comes from **function calling** — the LLM can invoke tools to interact with dIKta.me's internals and connected services.

### 3.1 Tool Manifest

Tools are registered with the orchestrator and described to the LLM as callable functions:

```csharp
public interface IOrchestratorTool
{
    /// <summary>Unique tool identifier for the LLM function schema.</summary>
    string Name { get; }

    /// <summary>Human-readable description for the LLM to understand when to use this tool.</summary>
    string Description { get; }

    /// <summary>JSON Schema for the tool's parameters.</summary>
    string ParameterSchema { get; }

    /// <summary>Execute the tool with the given parameters.</summary>
    Task<ToolResult> ExecuteAsync(
        JsonElement parameters,
        CancellationToken ct = default);
}

public record ToolResult(
    bool IsSuccess,
    string Output,         // Text response to feed back to LLM
    string? ErrorMessage = null);
```

### 3.2 Built-in Tools (Phase 1)

| Tool | Description | Backed By |
|------|-------------|-----------|
| `get_dictation_stats` | Query dictation history — count, word totals, by mode, by date range | `HistoryManager` |
| `get_system_status` | Current mode, active STT/LLM providers, connected devices | `SettingsManager`, `AudioDeviceManager` |
| `switch_mode` | Change active dictation mode | `DictationModeManager` |
| `list_modes` | List available dictation modes | `DictationModeManager` |
| `get_settings` | Read current settings values | `SettingsManager` |
| `set_setting` | Change a setting | `SettingsManager` |
| `search_history` | Search past dictations by keyword or date | `HistoryManager` |
| `take_note` | Save a voice note to the notes file | `NoteWriter` |

### 3.3 Connector Tools (Phase 2 — requires SPEC_013)

| Tool | Description | Backed By |
|------|-------------|-----------|
| `check_email` | Query recent emails | `GmailConnector.GetContextAsync()` |
| `draft_email` | Create an email draft | `GmailConnector.SendAsync()` |
| `check_calendar` | Query upcoming events | `GoogleCalendarConnector.GetContextAsync()` |
| `save_to_obsidian` | Write content to Obsidian vault | `ObsidianConnector.SendAsync()` |
| `trigger_webhook` | Fire a webhook with custom payload | `WebhookConnector.SendAsync()` |
| `stream_command` | Send a command to Streamer.bot | `StreamerBotConnector.SendAsync()` |

### 3.4 Memory Tools (Phase 3 — requires SPEC_014)

| Tool | Description | Backed By |
|------|-------------|-----------|
| `recall` | Semantically search past interactions | `IMemoryLayer.SearchAsync()` |
| `remember` | Explicitly store a fact or preference | `IMemoryLayer.StoreAsync()` |

### 3.5 Tool Execution Flow

```
User utterance → STT → text
                        ↓
              LLM (with tool schemas in system prompt)
                        ↓
              LLM decides: direct answer OR tool call
                        ↓
        ┌───────────────┴───────────────┐
        ▼                               ▼
  Direct answer                   Tool call(s)
  → TTS → speak                  → Execute tool(s)
                                  → Feed results back to LLM
                                  → LLM formulates response
                                  → TTS → speak
```

**Parallel tool calls**: If the LLM requests multiple tools simultaneously (e.g., "check my email and calendar"), execute them in parallel via `Task.WhenAll`.

---

## 4. Voice & Personality

### 4.1 Persona Configuration

The orchestrator's personality is defined by a **system prompt + voice selection**:

```csharp
public record OrchestratorPersona
{
    public required string Name { get; init; }          // "Dikta", "Jarvis", custom
    public required string SystemPrompt { get; init; }  // Personality instructions
    public required string VoiceId { get; init; }       // TTS voice selection
    public string? WakePhrase { get; init; }            // Optional flavor text (not a trigger)
    public ResponseStyle Style { get; init; }           // Concise, Detailed, Casual
}

public enum ResponseStyle
{
    Concise,      // Short, direct answers
    Detailed,     // Thorough explanations
    Casual        // Friendly, conversational
}
```

### 4.2 Built-in Personas

| Persona | Style | Voice | Flavor |
|---------|-------|-------|--------|
| **Chaviz** (default) | Concise | Kokoro "af_heart" or Deepgram "aura-asteria" | Professional, efficient, slightly warm. Bilingual personality. |
| **Formal** | Detailed | Deepgram "aura-arcas" (British male) | Formal, witty, dry humor |
| **Custom** | User-defined | User-selected | Fully customizable system prompt |

### 4.3 Example System Prompt (Chaviz persona)

```
You are Chaviz, the voice assistant for dIKta.me — a desktop voice productivity app.
You are concise, helpful, and slightly warm. You speak in short sentences.
You have access to tools that let you query the user's dictation history, change settings,
check email, and manage their workflow.

When the user asks something you can answer from tools, use them. Don't guess.
When you don't have a tool for something, say so honestly.
Keep responses under 2-3 sentences unless the user asks for detail.
Always confirm when you've completed an action.
```

---

## 5. Audio Architecture

### 5.1 Two Implementation Paths

The orchestrator can work with **two audio architectures**, chosen based on available hardware and user preference:

#### Path A: Pipeline (STT → LLM → TTS) — Ship First

Uses existing infrastructure:
```
Mic → AudioRecorder → STT (Deepgram/Whisper) → LLM (Gemini/Ollama) → TTS (Kokoro/Deepgram) → Speaker
```

**Pros**: Works today with existing providers, proven pipeline, cloud or local  
**Cons**: Cumulative latency (~2-4s total), three separate hops  
**Best for**: Cloud mode (Deepgram STT + Gemini LLM + Deepgram TTS ≈ 1.5s total)

#### Path B: Unified Audio Model (LFM 2.5) — Future

Single model handles everything:
```
Mic → AudioRecorder → LFM 2.5 Audio (speech-in → speech-out) → Speaker
```

**Pros**: Minimal latency, runs fully local, native turn-taking  
**Cons**: Limited reasoning for complex tool use, 1.5B model limitations  
**Best for**: Simple queries, conversational flow, local-only operation

#### Path C: Hybrid (Recommended Long-Term)

```
Mic → LFM 2.5 (STT + VAD) → Cloud/Local LLM (reasoning + tools) → LFM 2.5 (TTS)
```

LFM 2.5 handles the voice interface (fast STT + natural TTS), while a more capable LLM handles reasoning and tool execution. Best of both worlds.

### 5.2 Audio Session Management

The orchestrator needs a **persistent audio session** that differs from current pipeline behavior:

| Current Pipelines | Orchestrator |
|---|---|
| Start recording on hotkey down | Start recording on hotkey press (toggle) |
| Stop on hotkey up | Stop on VAD silence detection |
| Single-shot: record → process → done | Session-based: record → process → respond → keep listening |
| Dispose audio resources after each use | Keep audio session alive during conversation |

This requires a new `OrchestratorAudioSession` that wraps `AudioRecorder` with:
- Persistent mic access during conversation
- VAD-based utterance segmentation
- Interruptible TTS playback (press hotkey to cut off Chaviz and speak)

---

## 6. LLM Requirements

### 6.1 Function Calling Support

The orchestrator LLM **must support function/tool calling**. This narrows the provider options:

| Provider | Function Calling | Local? | Recommendation |
|----------|-----------------|--------|----------------|
| **Gemini** (1.5 Flash/Pro) | ✅ Native | No | ✅ Best cloud option |
| **OpenAI** (GPT-4o/Mini) | ✅ Native | No | ✅ Good fallback |
| **Anthropic** (Claude) | ✅ Native | No | ✅ Good fallback |
| **Ollama** (Qwen2.5, Llama3.1+) | ✅ Via grammar | Yes | ✅ Best local option |
| **LFM 2.5 Instruct** | ❓ Limited | Yes | ⚠️ May need structured output parsing |

### 6.2 Provider Abstraction

The orchestrator doesn't call `ILLMProvider` directly — it uses a specialized interface that supports multi-turn + tool calling:

```csharp
public interface IOrchestratorLLM
{
    /// <summary>
    /// Send a message in an ongoing conversation, with tool schemas available.
    /// Returns either a text response or tool call request(s).
    /// </summary>
    Task<OrchestratorResponse> ChatAsync(
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<ToolSchema> tools,
        CancellationToken ct = default);
}

public record OrchestratorResponse
{
    public string? TextResponse { get; init; }
    public IReadOnlyList<ToolCallRequest>? ToolCalls { get; init; }
    public bool IsToolCall => ToolCalls is { Count: > 0 };
}

public record ToolCallRequest(
    string ToolName,
    JsonElement Arguments,
    string CallId);

public record ChatMessage(
    ChatRole Role,       // System, User, Assistant, Tool
    string Content,
    string? ToolCallId = null);
```

---

## 7. UI Concept

### 7.1 Orchestrator Panel

The orchestrator gets its own UI — either a floating overlay (like QuickChat) or an integrated panel:

```
┌──────────────────────────────────────────┐
│  🎙️ Chaviz                   ⚙️  ✕      │
├──────────────────────────────────────────┤
│                                          │
│  🗣️ "Check if I have new emails"         │
│                                          │
│  🤖 "You have 5 new emails. The most     │
│      recent is from Sarah about the      │
│      Q3 budget review. Want me to        │
│      summarize them?"                    │
│                                          │
│  🗣️ "Yes, give me a quick summary"       │
│                                          │
│  🤖 "Here's the rundown:                 │
│      1. Sarah — Q3 budget review (...)   │
│      2. Dev team — CI pipeline fix (...)  │
│      ..."                                │
│                                          │
├──────────────────────────────────────────┤
│  [🎤 Press to speak]    ● Listening...   │
│                                          │
│  Status: Connected · Cloud · 14 tools    │
└──────────────────────────────────────────┘
```

### 7.2 Visual Feedback States

| State | Visual | Audio |
|-------|--------|-------|
| **Idle** | Subtle pulsing mic icon | — |
| **Listening** | Animated waveform / glowing ring | Soft chime on activate |
| **Thinking** | Spinning/morphing animation | — |
| **Speaking** | Waveform visualization of TTS output | Voice response |
| **Error** | Red flash | Error tone |
| **Session active (idle)** | Subtle breathing animation | — |

### 7.3 Tray Integration

The orchestrator state could be reflected in the tray icon:
- Idle: Normal icon
- Active conversation: Pulsing/colored indicator
- Quick-launch: Right-click tray → "Talk to Chaviz"

---

## 8. Settings

Following the SPEC_015 plugin pattern, Chaviz settings live in **plugin-local JSON** — NOT in `AppSettings`:

```
%APPDATA%/DiktaMe/plugins/orchestrator-settings.json

OrchestratorPluginSettings
├── Enabled: bool (default: false — opt-in)
├── Hotkey: string (default: "Ctrl+Alt+J")
├── SilenceThresholdMs: int (default: 1200)
├── SessionTimeoutSeconds: int (default: 30)
├── PersonaName: string (default: "Chaviz")
├── CustomSystemPrompt: string? (overrides built-in persona prompt)
├── VoiceId: string (default: from TTS settings)
├── ResponseStyle: Concise | Detailed | Casual (default: Concise)
├── LlmProvider: string (default: inherit from global)
├── LlmModel: string (default: inherit from global)
├── SttProvider: string (default: inherit from global)
├── TtsProvider: string (default: inherit from global)
├── EnabledTools: List<string> (default: all built-in tools)
├── PlaySounds: bool (default: true — activation/deactivation chimes)
└── ShowTranscript: bool (default: true — show user speech as text in panel)
```

Loaded/saved via `IPluginSettingsStore` (same as Connectors, Meetings, Memory plugins).

---

## 9. Plugin Entry Class

```csharp
[PluginEntry("orchestrator", "Chaviz", "1.0.0")]
public class OrchestratorPlugin : IPlugin
{
    public string Id => "orchestrator";
    public string DisplayName => "Chaviz";
    public PluginState State { get; private set; }

    public async Task InitializeAsync(IPluginContext context) { /* resolve services */ }

    public async Task EnableAsync()
    {
        // Subscribe to PipelineEventBus.OnCompleted (log orchestrator sessions)
        // Register settings page via IPluginUIRegistry.AddSettingsPage()
        // Register tray item "Talk to Chaviz" via IPluginUIRegistry.AddTrayMenuItems()
        // Register hotkey
    }

    public async Task DisableAsync()
    {
        // Dispose event bus subscriptions
        // Remove UI contributions
        // Unregister hotkey
    }
}
```

**Key principle**: The orchestrator is **additive and isolated**. It lives alongside existing pipelines as a plugin DLL, sharing core services via `IPluginContext.Services`. It does not replace or modify any existing pipeline behavior. If the plugin DLL is removed from the `plugins/` folder, zero impact on the rest of the app.

---

## 10. Example Conversations

### System Query
```
User: "Hey Chaviz, how productive was I today?"
Chaviz: [calls get_dictation_stats(range="today")]
Chaviz: "You've done 14 dictations today — about 2,300 words across 4 modes.
        Your most used mode was Professional with 8 sessions."
```

### Tool Execution
```
User: "Switch me to RAW mode."
Chaviz: [calls switch_mode(mode="raw")]
Chaviz: "Done. You're now in RAW mode — no LLM processing, pure transcription."
```

### Email Integration (Phase 2)
```
User: "Hey Chaviz, check if I have new emails in my inbox."
Chaviz: [calls check_email(filter="unread", limit=5)]
Chaviz: "Yes, you have 5 new emails. The most recent is from Sarah about
         the Q3 budget review. Want me to summarize them?"
User: "Yes please."
Chaviz: [calls check_email(filter="unread", limit=5, include_body=true)]
Chaviz: "Here's the rundown:
         1. Sarah — Q3 budget review: she needs your sign-off by Friday...
         2. Dev team — CI pipeline is green again after the fix...
         3. ..."
User: "Draft a reply to Sarah saying I'll review it tomorrow."
Chaviz: [calls draft_email(to="sarah", re="Q3 budget", body="...")]
Chaviz: "Draft created. It's in your Gmail drafts whenever you're ready to send."
```

### Memory Recall (Phase 3)
```
User: "What did I say about the API redesign last week?"
Chaviz: [calls recall(query="API redesign", timeframe="last week")]
Chaviz: "Last Tuesday you dictated a note about moving to REST from GraphQL.
         You mentioned three concerns: migration cost, client compatibility,
         and the team's GraphQL expertise. Want me to pull up the full note?"
```

---

## 11. Implementation Phases

> **Depends on:** SPEC_015 Phase 0B (Plugin Infrastructure). Chaviz can be developed in parallel with Connectors/Meetings/Memory plugins.

### Phase R: Foundation (MVP) [SPEC_017-R]
> Plugin entry, state machine, basic tool-calling, session audio.

- [ ] Create `DiktaMe.Plugin.Orchestrator` project (same pattern as Connectors/Meetings/Memory)
- [ ] `OrchestratorPlugin : IPlugin` with `[PluginEntry]` — enable/disable, settings page, tray item
- [ ] `OrchestratorService` — state machine (Idle → Listening → Thinking → Speaking)
- [ ] `IOrchestratorLLM` — adapter wrapping existing `ILLMProvider` with tool-calling support
- [ ] `IOrchestratorTool` interface + tool registry
- [ ] Built-in tools: `get_dictation_stats`, `get_system_status`, `switch_mode`, `list_modes`
- [ ] `OrchestratorAudioSession` — VAD-based utterance detection using silence threshold
- [ ] Multi-turn conversation state (in-memory)
- [ ] Basic UI panel (contributed via `IPluginUIRegistry`)
- [ ] Settings: `OrchestratorPluginSettings` via `IPluginSettingsStore`
- [ ] Hotkey registration
- [ ] Publish orchestrator results to `PipelineEventBus.PublishCompleted()` (so Memory/Connectors can subscribe)

### Phase S: Tools & Personality [SPEC_017-S]
> Richer tool set, persona system, cross-plugin tool discovery.

- [ ] Additional tools: `search_history`, `get_settings`, `set_setting`, `take_note`
- [ ] Cross-plugin tools (optional, graceful degradation if plugin absent):
  - Connector tools: `check_email`, `check_calendar`, `save_to_obsidian` (resolves via DI)
  - Memory tools: `recall`, `remember` (resolves `IMemoryLayer` via DI)
  - Meeting tools: `start_recording`, `get_meetings` (resolves `SessionManager` via DI)
- [ ] Persona system: built-in personas, custom persona editor
- [ ] Response style configuration
- [ ] Improved VAD (Silero ONNX)
- [ ] Interruptible TTS (press hotkey to cut off response and speak)
- [ ] Conversation persistence (save/resume conversations)

### Phase T: LFM 2.5 Integration [SPEC_017-T]
> Unified audio model for lower latency and local-first operation.

- [ ] `LfmAudioProvider` implementing `IOrchestratorLLM` with speech-in/speech-out
- [ ] Hybrid mode: LFM 2.5 for voice + cloud LLM for reasoning
- [ ] Native turn-taking (replace VAD with model-native detection)
- [ ] Streaming response (start speaking before full response is generated)

### Phase U: Intelligence & Polish [SPEC_017-U]
> Context-aware conversations, proactive behavior.

- [ ] Automatic context injection from Memory into conversation (via `OnBeforeLlmProcessing` — Memory plugin handles this transparently)
- [ ] User preference learning (Memory stores interaction patterns)
- [ ] Proactive suggestions ("You usually check email around this time...")
- [ ] Conversation analytics (stored in plugin's own SQLite or via event bus → Memory)

---

## 12. Open Questions

1. **Hotkey**: `Ctrl+Alt+J` — does this conflict with anything? Should it be configurable from the start?
2. **QuickChat overlap**: The existing QuickChat (`Ctrl+Alt+C`) is text-based chat with optional voice. Should the orchestrator **replace** QuickChat or coexist? They serve similar but distinct purposes.
3. **Tool safety**: Should some tools require confirmation before execution? (e.g., `set_setting` could break things, `draft_email` involves external systems)
4. **Conversation storage**: Should orchestrator conversations be stored in `ConversationManager` (existing) or a separate store? They have tool call metadata that normal chat doesn't.
5. **Concurrent audio**: If the user is mid-dictation and triggers the orchestrator, what happens? Mutual exclusion via `AudioRecorder`?
6. **LFM 2.5 readiness**: Is the GGUF/ONNX runtime mature enough for production use in a .NET desktop app today?

---

## 13. Cross-Reference

| Spec | Relationship | Dependency Type |
|------|-------------|----------------|
| **SPEC_015** (Modules Sprint) | Chaviz is Plugin 4 in the module architecture. Reuses `IPlugin`, `PipelineEventBus`, `PluginManager`, `IPluginUIRegistry`, `IPluginSettingsStore`. | **Hard** — requires Phase 0B |
| **SPEC_013** (Connectors) | Connectors plugin provides tools Chaviz can call (`check_email`, `save_to_obsidian`, etc.) via DI resolution. Graceful fallback if absent. | **Soft** — optional, enhances capabilities |
| **SPEC_014** (Memory) | Memory plugin provides `recall`/`remember` tools AND transparently enriches Chaviz conversations via `OnBeforeLlmProcessing` hook. | **Soft** — optional, enhances intelligence |
| **SPEC_015 LFM 2.5** (Models) | LFM 2.5 Audio is the eventual voice engine for low-latency local speech-to-speech. | **Soft** — Phase T enhancement |
| **SPEC_001** (Meetings) | Meetings plugin provides tools Chaviz can call (`start_recording`, `get_meetings`). | **Soft** — optional |
| **SPEC_002** (Vision) | Vision (Core) could be an orchestrator tool: "What's on my screen?" | **Soft** — optional |

---

*This spec positions dIKta.me to evolve from a voice productivity tool into a voice-first desktop assistant — your personal Chaviz that knows your tools, your data, and your preferences. As a plugin module, it can be installed, enabled, or removed independently — and it grows more powerful as other plugins (Memory, Connectors, Meetings) come online.*
