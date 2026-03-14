# SPEC_013: Connectors & App Integrations — Implementation Plan

> **Status:** APPROVED → **Merged into** [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) (Module 1, Phases A–E + J)
> **Date:** 2026-03-14
> **Supersedes:** `SPEC_013_CONNECTORS.md` (draft, archived)
> **Goal:** Create a self-contained **Connector Module** for dIKta.me — an independent add-on that lets users create **Connector Presets**: composable mini-pipelines that route voice, text selection, or (future) screenshots through any STT + LLM combination to external destinations (files, webhooks, APIs), with notification and inbox logging. The module is architecturally isolated from the core dictation system — one-way dependency, single hook point, opt-in activation.
> **Related Specs:**
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — Scribe outputs feed into Connectors for distribution (cross-module bridge, Phase J)
> - [`SPEC_002_VISION.md`](SPEC_002_VISION.md) — Vision outputs feed into Connectors; `ConnectorInputType.Screenshot` flag
> - [`SPEC_013_USE_CASES.md`](SPEC_013_USE_CASES.md) — 218 use cases across 10 sections, companion research document
> - [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md) — Memory can enrich connector preset LLM prompts with past context
> - [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) — **Implementation sprint** (this spec is the design reference; SPEC_015 is the build plan)

---

## 1. Executive Summary

dIKta.me's value grows exponentially when pipeline outputs can flow beyond the clipboard. This spec defines a **self-contained Connector Module** — an independent add-on with its own presets, settings, and UI — organized by user segment:

| Tier | Audience | Connectors | Auth Complexity |
|------|----------|-----------|----------------|
| **Casual** | Everyone with a Google account | Google Calendar, Gmail | OAuth 2.0 (guided) |
| **Mid-tier** | Knowledge workers, note-takers | Obsidian, Notion, Custom Folder | None / OAuth |
| **Advanced** | Power users, developers, streamers | Webhook, Discord, Streamer.bot, Zapier/n8n triggers | None (paste URL/local) |

### Module Architecture Principles

1. **Isolated module** — Connectors live in `DiktaMe.Core/Connectors/`, have their own settings sub-object, their own UI, and their own test suite. A connector bug can never break core dictation.
2. **One-way dependency** — Connectors depend on `PipelineResult` (stable, existing). The pipeline never depends on connectors. Single hook point: one line in `OnPipelineCompleted()`.
3. **Connector Presets** — Independent from Dictation Presets. A Connector Preset is a full mini-pipeline: Input → STT → LLM (specific model + prompt) → Connector destinations → Notification → Inbox log.
4. **Opt-in activation** — Master toggle `ConnectorSettings.Enabled`. When off, zero overhead. No connector UI visible until the user opts in.
5. **Independent release cycle** — Connector features can ship, update, or be fixed without touching the core dictation pipeline.

The implementation is phased so that **zero-auth local connectors ship first** (Phases A-E), followed by Google OAuth (Phase F), then cloud destinations (Phase G+). This gives us shippable value at every milestone while building toward the full vision.

---

## 2. Market Research & Strategic Positioning

### Competitor Analysis (7 tools surveyed)

| Tool | Price Tier | Top Integrations | Local-First? |
|------|-----------|-----------------|-------------|
| **Granola** | $18/mo | Notion, Slack, HubSpot, Salesforce | No |
| **Otter.ai** | $16.99/mo | Salesforce, HubSpot, Slack, Zoom | No |
| **Fireflies.ai** | $18/mo | Slack, Notion, HubSpot, Asana, Jira | No |
| **Fellow** | $10/mo | Slack, Jira, Asana, HubSpot, Zapier | No |
| **Krisp** | $12/mo | Slack, Notion, HubSpot | No |
| **Tactiq** | $12/mo | Notion, Google Docs, Quip, ChatGPT | No |
| **Supernormal** | $19/mo | Salesforce, HubSpot, Slack, Notion | No |

### Key Insights

1. **CRM integrations (Salesforce/HubSpot) are THE revenue driver** — every competitor gates these to $29-40/mo tiers
2. **Obsidian is the #1 underserved opportunity** — high demand, near-zero competition (cloud tools can't do it)
3. **Webhook connectors give "100+ integrations" for free** — outbound POST to Zapier/Make/n8n
4. **Notion is the most requested productivity destination** across all user segments
5. **Local-first connectors are our unique differentiator** — no competitor can match direct filesystem writes
6. **Google ecosystem is universal** — everyone has a Google account; Calendar + Gmail unlock the most everyday value for casual users

### Strategic Advantages

- **Obsidian + Folder**: Zero competition. No cloud tool can write directly to local filesystems.
- **Webhook + Zapier/n8n**: One connector = 1000+ downstream integrations. Outperforms building them individually.
- **Google + local LLM**: Unique angle — Ollama coordinates calendar queries and email drafts without any cloud LLM dependency. Privacy-first Google integration.
- **Streamer.bot**: Opens the entire local automation market (streamers, home lab enthusiasts, accessibility users).

---

## 3. User Segments & Connector Tiers

### Tier 1: Casual Users — Google Ecosystem

> *"I just want to ask 'What's my next meeting?' and draft emails by voice."*

Everyone has a Google account. These connectors unlock the most everyday value with minimal cognitive overhead.

| Connector | Direction | Auth | What it Does |
|-----------|-----------|------|-------------|
| **Google Calendar** | Pull (context) | OAuth 2.0 + PKCE | Answers "What's my next meeting?", provides meeting context to Scribe |
| **Gmail** | Push + Pull | OAuth 2.0 + PKCE | Drafts emails by voice, reads recent threads for context |

**Google OAuth Technical Details:**
- Desktop apps use **loopback redirect** (`http://127.0.0.1:{port}`) + **PKCE** — no browser extension needed
- NuGet: `Google.Apis.Calendar.v3`, `Google.Apis.Gmail.v1`, `MimeKit` (for email composition)
- Custom `IDataStore` wrapping existing `SecureStorage` (DPAPI) for token persistence
- Scopes: `calendar.readonly` (read events + Meet links), `gmail.compose` (create drafts, not send)
- Testing mode: 100 test users, 7-day refresh token expiry. Production requires Google verification
- Add `"google_calendar"`, `"gmail"` to `SecureStorage.ValidProviders` when implementing

**Implementation: Phase F** (after core connectors ship)

### Tier 2: Mid-tier — Knowledge Workers

> *"Save my dictations to Obsidian with proper frontmatter. Export meeting summaries to Notion."*

These users have intentional note-taking systems. They want structured output in their tools.

| Connector | Direction | Auth | What it Does |
|-----------|-----------|------|-------------|
| **Obsidian Vault** | Push (file write) | None | Writes `.md` files with YAML frontmatter to vault directory |
| **Custom Folder** | Push (file write) | None | Writes to any configured directory (Logseq, Bear, plain markdown) |
| **Notion** | Push (API) | OAuth 2.0 | Appends to pages, creates database entries |

**Implementation: Obsidian + Folder in Phase B-C** (first connectors to ship). Notion in Phase G (Release 2).

### Tier 3: Advanced — Power Users & Developers

> *"Trigger my n8n workflow when I dictate. Control my stream setup by voice. POST to my custom API."*

These users build their own pipelines. We give them the building blocks.

| Connector | Direction | Auth | What it Does |
|-----------|-----------|------|-------------|
| **Generic Webhook** | Push (HTTP POST) | None (paste URL) | POSTs structured JSON to any endpoint — Zapier, n8n, Make, custom |
| **Discord Webhook** | Push (HTTP POST) | None (paste URL) | Posts formatted messages to Discord channels |
| **Streamer.bot** | Push (WebSocket) | None (local) | Triggers Streamer.bot actions — OBS, lights, MIDI, audio, smart home |

**Implementation: Phase C** (ships with Obsidian + Folder).

### Deferred (not this roadmap)

| Connector | Reason |
|-----------|--------|
| Outlook | Microsoft Graph OAuth is complex, overlaps with Gmail |
| WhatsApp | TOS ban risk, no clean path (see SPEC_013 Section 3.6) |
| Workday/BambooHR | Enterprise-only, niche |
| Salesforce/HubSpot | Revenue tier — Phase G+, gated to paid plans |
| Jira/Linear | Phase G+, API token auth |

---

## 4. Use Cases & Workflow Synergies

### Existing Workflows Enhanced

| Workflow | Before | After (with Connectors) |
|----------|--------|------------------------|
| **Dictation** | Text injected into active window | Also saved to Obsidian vault as timestamped note |
| **Note pipeline** | Appends to single `diktame-notes.md` | Also writes to Obsidian vault with frontmatter + tags |
| **Ask pipeline** | Answer shown in toast/clipboard | Also POSTed to Discord channel or webhook |
| **Chat export** | Manual copy-paste from QuickChat | One-click "Send to Obsidian" or "Post to webhook" |
| **Translate** | Injected into active window | Also saved to translation log in vault |

### New Workflows: Casual (Google)

1. **Voice → Calendar Query**: "What's my next meeting?" → local LLM routes to `GoogleCalendarConnector.GetUpcomingEvents()` → LLM formats: "You have a sync with Bob at 2 PM about Q3 budget." Zero cloud LLM — Ollama handles routing + formatting.

2. **Voice → Email Draft**: "Draft a reply to Bob's last email saying I'll be 10 minutes late" → local LLM calls `GmailConnector.GetRecentThread("Bob")` for context → generates reply → `GmailConnector.CreateDraft(to, subject, body)`. User reviews draft in Gmail. Local LLM writes, Google only stores.

3. **Meeting Prep**: Before a meeting, ask "Brief me on my 2 PM meeting" → Calendar connector fetches event description, agenda, attendees → LLM summarizes. Eventually enriched with Scribe context from previous meetings with same participants.

### New Workflows: Mid-tier (Obsidian / Notion)

4. **Voice-to-Obsidian Daily Note**: Dictate → AI cleans up → appends to today's daily note (`YYYY-MM-DD.md`) with YAML frontmatter tags. Killer feature for r/ObsidianMD.

5. **Quick Thought Capture**: Walking, thinking out loud → dictation saves to Obsidian inbox folder. No app switching, no typing.

6. **Dev Log**: Dictate progress notes during coding → saved to `devlog/` folder in project repo with timestamps. Git-friendly markdown.

7. **Broadcast Mode**: One dictation → inject into active window + save to Obsidian + POST to Discord webhook. All in parallel.

### New Workflows: Advanced (Webhook / Automation)

8. **Automation Trigger**: Dictation completes → webhook POSTs structured JSON to Zapier Catch Hook or local n8n instance → triggers arbitrary downstream workflow (Slack notification, Trello card, Google Sheet row, CRM update). One webhook = 1000+ integrations.

9. **Local n8n Orchestrator**: Fully local loop — dIKta.me dictation → webhook to `localhost:5678` n8n → n8n processes/routes/transforms → n8n writes to Obsidian vault or calls other local services. Zero cloud dependency.

10. **Meeting Summary Distribution**: Future Scribe output → webhook to n8n → fan out to Slack channel + Notion page + email. Users build their own pipeline.

11. **Streamer.bot Bridge**: Dictate "switch to focus mode" → `StreamerBotConnector` sends `DoAction` via WebSocket → Streamer.bot executes C# action (lights, OBS, audio, MIDI). Bidirectional: subscribe to Streamer.bot events.

12. **Discord Community Updates**: Dictate a voice note → Note pipeline cleans it up → Discord webhook auto-posts formatted message to announcement channel. Instant community engagement by voice.

### Future Vision: Composable Presets

13. **Presets = Pipeline + LLM + Prompt + Output Destinations**: A "Meeting Notes" preset combines Note pipeline + Ollama gemma3 + "Format as meeting minutes" prompt + auto-fire to Obsidian daily note + webhook to Slack channel. User creates presets like building blocks.

---

## 5. Architecture

### 5.1 Two Patterns: Push vs. Pull

**Phases A-E implement Push only.** Phase F adds Pull via LLM tool-use.

| Pattern | Direction | When | Example | Phase |
|---------|-----------|------|---------|-------|
| **Push** | App → External | After pipeline completion | Save to Obsidian, POST to webhook, trigger SB | A-E |
| **Pull** | External → App | During LLM processing | "What's my next meeting?" queries Calendar | F |

Push uses `IConnector.SendAsync()`. Pull uses `IConnector.GetContextAsync()` — connectors register as LLM tools in the `LLMRouter`, and the local LLM decides when to call them.

### 5.2 Core Interface

```csharp
public interface IConnector
{
    string Id { get; }                    // Matches ConnectorConfig.Type: "obsidian", "webhook", etc.
    string DisplayName { get; }           // "Obsidian Vault", "Generic Webhook"
    ConnectorType Type { get; }           // File, Webhook, WebSocket, Cloud
    bool IsConfigured(ConnectorConfig config);  // Validates config has required settings

    // Push: distribute pipeline output to destination (Release 1)
    Task<ConnectorResult> SendAsync(
        ConnectorPayload payload,
        ConnectorConfig config,
        CancellationToken ct = default);

    // Pull: retrieve context for LLM grounding (Release 2, Phase F)
    // Default implementation returns null (not supported).
    Task<string?> GetContextAsync(
        string query,
        ConnectorConfig config,
        CancellationToken ct = default) => Task.FromResult<string?>(null);
}

public enum ConnectorType { File, Webhook, WebSocket, Cloud }
```

### 5.3 Payload & Result Records

```csharp
public sealed record ConnectorPayload
{
    public required string Text { get; init; }
    public string? RawTranscript { get; init; }
    public required string Mode { get; init; }        // "dictate", "note", "ask", "translate", "chat", etc.
    public required DateTimeOffset Timestamp { get; init; }
    public int WordCount { get; init; }
    public int CharCount { get; init; }
    public long RecordingMs { get; init; }
    public long TranscriptionMs { get; init; }
    public long ProcessingMs { get; init; }
    public long InjectionMs { get; init; }
    public long TotalMs { get; init; }
    public long TtsPlayedMs { get; init; }
    public string? SttProvider { get; init; }
    public string? LlmProvider { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public double? TokensPerSec { get; init; }

    // Factory: build from PipelineResult
    public static ConnectorPayload FromPipelineResult(PipelineResult result) => new()
    {
        Text = result.Text,
        RawTranscript = result.RawTranscript,
        Mode = result.Mode,
        Timestamp = DateTimeOffset.UtcNow,
        WordCount = result.WordCount,
        CharCount = result.CharCount,
        RecordingMs = result.RecordingMs,
        TranscriptionMs = result.TranscriptionMs,
        ProcessingMs = result.ProcessingMs,
        InjectionMs = result.InjectionMs,
        TotalMs = result.TotalMs,
        TtsPlayedMs = result.TtsPlayedMs,
        SttProvider = result.SttProvider,
        LlmProvider = result.LlmProvider,
        InputTokens = result.InputTokens,
        OutputTokens = result.OutputTokens,
        TokensPerSec = result.TokensPerSec,
    };
}

public sealed record ConnectorResult
{
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public required string ConnectorId { get; init; }

    public static ConnectorResult Success(string id) => new() { IsSuccess = true, ConnectorId = id };
    public static ConnectorResult Failure(string id, string error) => new()
        { IsSuccess = false, ConnectorId = id, ErrorMessage = error };
}
```

### 5.4 Trigger Model (Preset-Based)

Connectors no longer fire individually — they fire as part of **Connector Presets**:

- **Active preset** (pill toggled ON in Control Panel): Fires after every pipeline completion while active. Stays active across multiple dictations until toggled off.
- **Multiple presets active**: Supported. "Meeting Debrief" + "Time Tracker" can both be active simultaneously.
- **Hotkey-triggered**: A preset with a dedicated hotkey can fire standalone (its own STT + LLM + destinations) without using the main dictation pipeline.
- **Preset deactivation**: Click the pill again to toggle off. Or it can auto-deactivate after N fires (future, not v1).

### 5.5 Integration Point (Single Hook)

**The entire connector module connects to the existing codebase via ONE line of code.**

The hook is `ControlPanelViewModel.OnPipelineCompleted()` (`src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs:738`). Right alongside `_metrics.RecordAsync(result)`, add:

```csharp
_ = _connectorManager.DispatchPresetsAsync(result, _activeConnectorPresetIds);
```

This is fire-and-forget (no `await`). Connector execution runs in the background — it never delays the user's dictation flow. The `ConnectorManager` handles everything internally: preset resolution, optional LLM re-processing, fan-out to destinations, notification, inbox logging, error isolation.

If `ConnectorSettings.Enabled` is `false` or `_activeConnectorPresetIds` is empty, `DispatchPresetsAsync` is a no-op (early return, zero overhead).

The `_activeConnectorPresetIds` list is maintained by the Control Panel's Connector Presets row — toggling a pill on/off adds/removes its ID.

### 5.6 Settings Model

New `ConnectorSettings` sub-object in `src/DiktaMe.Core/Config/AppSettings.cs`, following exact pattern of `TtsSettings`, `ChatSettings`:

```
AppSettings (line 387)
├── ... (existing 11 sub-objects)
└── Connectors: ConnectorSettings (NEW)
    ├── Enabled: bool (master toggle, default: false — opt-in)
    ├── InboxRetentionDays: int (default: 30)
    ├── Destinations: List<ConnectorConfig>          ← the "building blocks"
    │   ├── Id: string (GUID — stable, referenced by presets)
    │   ├── Type: string ("obsidian", "folder", "webhook", "discord", "streamerbot")
    │   ├── DisplayName: string (user-editable)
    │   ├── Enabled: bool
    │   └── Settings: Dictionary<string, string> (type-specific key-value pairs)
    └── Presets: List<ConnectorPreset>               ← the "mini-pipelines"
        ├── Id: string (GUID)
        ├── Title: string ("Meeting Debrief")
        ├── Icon: string? (Segoe Fluent glyph)
        ├── Color: string? (hex)
        ├── InputType: ConnectorInputType (Voice | Selection | Both)
        ├── SttProvider: string? ("local" | "deepgram" | null = inherit)
        ├── LlmProviderType: string? ("ollama" | "openai" | null = no LLM)
        ├── LlmModel: string? ("gemma3:4b" | "gpt-4o")
        ├── SystemPrompt: string? (custom instruction)
        ├── OutputConnectorIds: List<string> (GUIDs → Destinations[].Id)
        ├── InjectIntoActiveWindow: bool (default: false)
        ├── NotifyMode: ConnectorNotifyMode (Toast | Tts | Silent)
        ├── LogToInbox: bool (default: true)
        ├── Hotkey: string? (optional dedicated hotkey)
        ├── SortOrder: int
        └── Enabled: bool
```

**Key distinction**: `Destinations` are the configured connector endpoints (where). `Presets` are the composable pipelines that reference them (how + where). A single Obsidian destination can be used by 5 different presets with different LLM prompts.

Per-connector `Settings` dictionary:

| Type | Keys | Defaults |
|------|------|---------|
| **obsidian** | `VaultPath`, `SubFolder`, `NoteStrategy` (`daily`\|`standalone`), `FileNameTemplate`, `FrontmatterTags`, `DailyNoteFormat` | `daily`, `yyyy-MM-dd` |
| **folder** | `OutputPath`, `FileNameTemplate` | — |
| **webhook** | `Url`, `Method`, `ContentType`, `IncludeRawTranscript`, `CustomHeaders`, `SigningSecret` | `POST`, `application/json`, `true` |
| **discord** | `WebhookUrl`, `Username`, `AvatarUrl` | `dIKta.me` |
| **streamerbot** | `Host`, `Port`, `Endpoint`, `DefaultAction` | `127.0.0.1`, `8080`, `/`, — |

**SanitizeNulls**: Add `Connectors = s.Connectors ?? new()` to `src/DiktaMe.Core/Config/SettingsManager.cs:191` alongside the existing 11 sub-objects.

### 5.7 Security

- **Release 1**: Webhook URLs stored in `ConnectorConfig.Settings` (not SecureStorage — they're just URLs, not API keys)
- **Release 2**: OAuth tokens (Google, Notion, Slack) go in `SecureStorage` via custom `IDataStore` bridge. Add new entries to `ValidProviders`.
- **File connectors**: Validate paths — reject UNC, system paths. Allow any user-chosen local directory.

### 5.8 Privacy Gating

Connectors respect `AppSettings.Privacy.Level` (`src/DiktaMe.Core/Config/AppSettings.cs:162`):

| Level | Behavior |
|-------|---------|
| **Ghost** | All connectors disabled (no data leaves app) |
| **Stats** | Only metadata sent: mode, timestamp, word count — no text |
| **Balanced** | Text sent but PII scrubbed (future) |
| **Full** | Verbatim text sent |

### 5.9 Webhook Payload Schema (Zapier / n8n / Make compatible)

Structured for auto-field-mapping in Zapier and n8n. All fields available for routing/filtering.

```json
{
  "event": "pipeline.completed",
  "version": "1.0",
  "timestamp": "2026-03-14T10:30:00Z",
  "dictation": {
    "text": "The processed output text",
    "rawTranscript": "the raw transcript before llm",
    "mode": "dictate",
    "wordCount": 42,
    "charCount": 215
  },
  "timing": {
    "recordingMs": 3200,
    "transcriptionMs": 450,
    "processingMs": 800,
    "injectionMs": 50,
    "totalMs": 1300,
    "ttsPlayedMs": 0
  },
  "providers": {
    "stt": "deepgram",
    "llm": "ollama/gemma3:4b",
    "inputTokens": 120,
    "outputTokens": 85,
    "tokensPerSec": 42.5
  },
  "app": {
    "name": "dIKta.me",
    "version": "2.0.0"
  }
}
```

**Zapier specifics**: Fields auto-appear in the Zap editor after the first test POST to a Catch Hook URL. `dictation.mode` is ideal for Zapier's built-in filter step. Available on all Zapier tiers including free.

**n8n specifics**: Full JSON available for routing in `IF` / `Switch` nodes. n8n can be fully local (`npx n8n` on `localhost:5678`). Bidirectional — n8n webhooks can return response data (unlike Zapier). Ideal for local-first automation loops.

**HMAC signing** (optional): When `SigningSecret` is set, the connector computes `HMAC-SHA256(secret, requestBody)` and sends it as `X-DiktaMe-Signature: sha256={hex}`. n8n and custom receivers can verify authenticity.

**Privacy gating**: When privacy is `Stats`, `dictation.text` and `dictation.rawTranscript` are `"[redacted]"`. All timing/metadata always sent.

### 5.10 Connector Presets

> **Core concept**: Connector Presets are independent from Dictation Presets. They are self-contained mini-pipelines that compose from the same building blocks (STT, LLM, prompts) but route output to connector destinations instead of (or in addition to) the active window.

**Mental model:**
```
Dictation Preset = WHAT happens to your voice (pipeline + LLM + prompt → active window)
Connector Preset = WHERE the result goes (input → STT → LLM → connector destinations → notification → inbox log)
```

These are **two independent rows** in the Control Panel. The user can activate a Connector Preset alongside any Dictation Preset, or use a Connector Preset standalone.

#### 5.10.1 ConnectorPreset Model

```csharp
public sealed record ConnectorPreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string Title { get; init; }          // "Meeting Debrief", "Blog Draft", "Quick CRM Note"
    public string? Icon { get; init; }                   // Segoe Fluent icon glyph
    public string? Color { get; init; }                  // Accent color hex for pill UI

    // --- Input ---
    public ConnectorInputType InputType { get; init; } = ConnectorInputType.Voice;
    // Voice = hold hotkey, speak, release
    // Selection = select text + hotkey
    // Both = accepts either
    // Screenshot = future VISION spec (multimodal LLM input)

    // --- Processing ---
    public string? SttProvider { get; init; }            // "local" | "deepgram" | null (inherit from global)
    public string? LlmProviderType { get; init; }        // "ollama" | "openai" | "anthropic" | null (no LLM)
    public string? LlmModel { get; init; }               // "gemma3:4b" | "gpt-4o" | null
    public string? SystemPrompt { get; init; }           // Custom instruction for this preset's LLM pass

    // --- Output ---
    public List<string> OutputConnectorIds { get; init; } = [];  // GUIDs referencing ConnectorConfig.Id
    public bool InjectIntoActiveWindow { get; init; }    // Also inject result into focused app? (default: false)

    // --- Notification ---
    public ConnectorNotifyMode NotifyMode { get; init; } = ConnectorNotifyMode.Toast;
    public bool LogToInbox { get; init; } = true;        // Save result + metadata to Connector Inbox

    // --- UI ---
    public string? Hotkey { get; init; }                 // Optional dedicated hotkey
    public int SortOrder { get; init; }
    public bool Enabled { get; init; } = true;
}

[Flags]
public enum ConnectorInputType
{
    Voice = 1,
    Selection = 2,
    Screenshot = 4,    // Future: VISION spec
    Both = Voice | Selection,
    All = Voice | Selection | Screenshot,
}

public enum ConnectorNotifyMode
{
    Silent,     // No notification
    Toast,      // Brief toast: "Saved to Obsidian (42 words)"
    Tts,        // TTS reads confirmation aloud: "Draft posted"
}
```

#### 5.10.2 Example Connector Presets

| Preset | Input | STT | LLM | Prompt | Outputs | Notify |
|--------|-------|-----|-----|--------|---------|--------|
| **Meeting Debrief** | Voice | Deepgram (accuracy) | GPT-4o | "Extract action items and summary as meeting minutes" | Obsidian daily + Webhook→Slack | TTS: "Notes saved, team notified" |
| **Voice Blog Draft** | Voice | Local Whisper | Ollama gemma3:4b | "Refine into a blog post draft with title and sections" | Webhook→WordPress API | Toast: "Draft posted" |
| **Quick CRM Note** | Voice | Local Whisper | Ollama gemma3:4b | "Extract contact name, company, and follow-up action" | Webhook→HubSpot | Toast + Inbox |
| **Explain & File** | Selection | — (text input) | GPT-4o | "Explain this code/text clearly" | Obsidian standalone | Toast |
| **Translate & Post** | Selection | — (text input) | GPT-4o | "Translate to English, professional tone" | Discord webhook #announcements-en | TTS: "Posted" |
| **Stream Command** | Voice | Local Whisper | Ollama (fast) | "Parse as a stream control command" | Streamer.bot | Silent |
| **Inbox Capture** | Voice | Local Whisper | None (raw transcript) | — | Obsidian inbox folder | Toast |
| **Screenshot Analysis** | Screenshot | — | GPT-4o (vision) | "Describe what you see and extract key information" | Obsidian standalone | Toast |

#### 5.10.3 Connector Inbox

Every Connector Preset with `LogToInbox = true` writes an entry to the **Connector Inbox** — a local log of everything connectors have done.

```csharp
public sealed record InboxEntry
{
    public required string Id { get; init; }              // GUID
    public required DateTimeOffset Timestamp { get; init; }
    public required string PresetTitle { get; init; }     // "Meeting Debrief"
    public required string Text { get; init; }            // The processed output
    public string? RawInput { get; init; }                // Original voice/selection input
    public required string Mode { get; init; }            // Input type used
    public required List<InboxConnectorResult> Results { get; init; }  // Per-connector success/failure
    public bool IsRead { get; init; }                     // Has user reviewed this entry?
}

public sealed record InboxConnectorResult
{
    public required string ConnectorName { get; init; }   // "Obsidian Vault", "Slack Webhook"
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
```

**Storage**: SQLite table `connector_inbox` in existing `history.db` (same pattern as `dictation_history`). Entries retained for 30 days by default, configurable.

**UI**: Accessible from the Control Panel — small badge count on the Connector Presets row showing unread inbox entries. Click opens a flyout/panel listing recent connector activity. User can review, mark as read, or re-send failed entries.

#### 5.10.4 Control Panel Layout

```
┌─────────────────────────────────────────────────────────┐
│  DICTATION PRESETS (existing, unchanged)                 │
│  ┌──────┐ ┌──────────┐ ┌─────────┐ ┌──────┐            │
│  │Scribe│ │Freewriter│ │ Command │ │Custom│  + Add      │
│  └──────┘ └──────────┘ └─────────┘ └──────┘            │
├─────────────────────────────────────────────────────────┤
│  CONNECTOR PRESETS (new row, independent)         ⚙ 📥3 │
│  ┌───────────────┐ ┌──────────┐ ┌─────────────┐        │
│  │Meeting Debrief│ │Blog Draft│ │Quick CRM    │  + Add  │
│  │🎤 → 📝+💬    │ │🎤 → 📰   │ │🎤 → 📊      │        │
│  └───────────────┘ └──────────┘ └─────────────┘        │
├─────────────────────────────────────────────────────────┤
│  [Record Button]                                        │
│  Status / Telemetry                                     │
└─────────────────────────────────────────────────────────┘

⚙ = Opens Connector Settings (separate window/page)
📥3 = Inbox badge (3 unread entries)
```

The Connector Presets row:
- **Visually distinct** from Dictation Presets (different section, separator, possibly different pill color/style)
- **Toggle-on/toggle-off** behavior — click to activate, click again to deactivate. Stays active across multiple dictations until toggled off.
- **Multiple can be active** simultaneously (e.g., "Meeting Debrief" + "Time Tracker" both on)
- **Visual indicator** when active — highlighted border, glow, or filled pill state
- Each pill shows the input type icon(s) and output destination icon(s)
- **"+ Add"** opens the Connector Preset editor (in the separate settings window)

#### 5.10.5 Connector Preset Execution Flow

```
User activates "Meeting Debrief" connector preset (click pill → highlighted)
User dictates normally (with any Dictation Preset, or standalone)
    ↓
Pipeline completes → PipelineResult
    ↓
Existing flow: text injection, TTS, telemetry (unchanged)
    ↓
ConnectorManager.DispatchPresetsAsync(result, activePresetIds)
    ↓
For each active Connector Preset:
  1. Check InputType matches (Voice? Selection? Screenshot?)
  2. If preset has its own LLM config: re-process result.Text through preset's LLM + prompt
     (e.g., "Extract action items" pass on the already-dictated text)
  3. Build ConnectorPayload from (re-)processed result
  4. Fire all OutputConnectorIds in parallel (Task.WhenAll)
  5. Collect ConnectorResults
  6. Send notification (Toast / TTS / Silent)
  7. Write InboxEntry if LogToInbox = true
```

**Key detail**: Step 2 means a Connector Preset can optionally **re-process** the pipeline output through its own LLM pass. The dictation pipeline produces the base text, then each connector preset can apply its own transformation. This is what makes "Meeting Debrief" work — the raw dictation comes out of the standard pipeline, then the connector preset's LLM reshapes it into meeting minutes before sending to Obsidian + Slack.

If the preset has no LLM config (`LlmProviderType = null`), it passes the pipeline output through as-is (raw forwarding).

#### 5.10.6 Connector Settings — Separate Window

Connector configuration gets its **own settings window** (or a dedicated full-page section), not crammed into the existing Settings tabs. This reinforces the "module" mental model.

**Connector Settings Window contents:**
1. **Master toggle**: Enable/Disable all connectors
2. **Configured Connectors** list: Add/Edit/Remove connector destinations (Obsidian vault, webhook URLs, Discord, Streamer.bot). These are the "building blocks" that presets reference.
3. **Connector Presets** list: Add/Edit/Remove presets. Each preset's editor shows:
   - Title, icon, color
   - Input type checkboxes (Voice / Selection / Screenshot)
   - STT provider picker (Local / Cloud / Inherit)
   - LLM provider + model picker + system prompt textarea
   - Output connector multi-select (checkboxes from configured connectors list)
   - Notification mode picker
   - Log to Inbox toggle
   - Optional hotkey binding
   - **Test button** — fires a synthetic payload through the full preset pipeline
4. **Inbox**: View/manage recent connector activity log

This keeps all connector complexity self-contained and out of the main Settings window.

---

## 6. Files to Create/Modify

### New Files (DiktaMe.Core)

| File | Purpose |
|------|---------|
| `Connectors/IConnector.cs` | Interface + base class |
| `Connectors/ConnectorPayload.cs` | Payload record with `FromPipelineResult()` factory |
| `Connectors/ConnectorResult.cs` | Result record with `Success()`/`Failure()` factories |
| `Connectors/ConnectorType.cs` | Enum: File, Webhook, WebSocket, Cloud |
| `Connectors/ConnectorInputType.cs` | Flags enum: Voice, Selection, Screenshot |
| `Connectors/ConnectorNotifyMode.cs` | Enum: Silent, Toast, Tts |
| `Connectors/ConnectorManager.cs` | Orchestrator: dispatch, preset execution, connector resolution |
| `Connectors/ConnectorPresetRunner.cs` | Executes a single preset: optional LLM re-process → fan-out → notify → inbox |
| `Connectors/ObsidianConnector.cs` | Filesystem: `.md` with YAML frontmatter |
| `Connectors/FolderConnector.cs` | Generic filesystem connector |
| `Connectors/WebhookConnector.cs` | Outbound HTTP POST with structured JSON |
| `Connectors/DiscordWebhookConnector.cs` | Discord-specific formatting + embeds |
| `Connectors/StreamerBotConnector.cs` | WebSocket client for Streamer.bot |
| `Config/ConnectorSettings.cs` | `ConnectorSettings`, `ConnectorConfig`, `ConnectorPreset` records |
| `Data/ConnectorInboxManager.cs` | SQLite CRUD for `connector_inbox` table (InboxEntry records) |

### New Files (DiktaMe.App)

| File | Purpose |
|------|---------|
| `Views/ConnectorSettingsWindow.xaml` + `.cs` | Separate settings window for connectors module |
| `ViewModels/ConnectorSettingsViewModel.cs` | Connector + Preset CRUD ViewModel |
| `Views/ConnectorInboxPanel.xaml` + `.cs` | Inbox flyout/panel for Control Panel |
| `ViewModels/ConnectorInboxViewModel.cs` | Inbox list ViewModel |

### New Files (DiktaMe.Core.Tests)

| File | Purpose |
|------|---------|
| `Connectors/ConnectorManagerTests.cs` | Dispatch, preset execution, privacy gating |
| `Connectors/ConnectorPresetRunnerTests.cs` | LLM re-processing, fan-out, notification, inbox write |
| `Connectors/ObsidianConnectorTests.cs` | File creation, daily append, frontmatter |
| `Connectors/FolderConnectorTests.cs` | File write, path validation |
| `Connectors/WebhookConnectorTests.cs` | HTTP POST, HMAC, retry, payload format |
| `Connectors/DiscordWebhookConnectorTests.cs` | Discord formatting, embed structure |
| `Connectors/StreamerBotConnectorTests.cs` | WebSocket messaging, reconnect logic |
| `Data/ConnectorInboxManagerTests.cs` | Inbox CRUD, retention, mark-as-read |

### Modified Files

| File | Change |
|------|--------|
| `src/DiktaMe.Core/Config/AppSettings.cs` `:387` | Add `ConnectorSettings Connectors` property (default: `Enabled = false`) |
| `src/DiktaMe.Core/Config/SettingsManager.cs` `:191` | Add `Connectors` to `SanitizeNulls()` |
| `src/DiktaMe.App/App.xaml.cs` | Register `ConnectorManager`, `ConnectorInboxManager`, `ConnectorSettingsViewModel`, `ConnectorInboxViewModel` in DI |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` `:738` | Add `_connectorManager.DispatchPresetsAsync(result, activePresetIds)` in `OnPipelineCompleted` — **single line addition** |
| `src/DiktaMe.App/Views/ControlPanel.xaml` | Add Connector Presets row widget below Dictation Presets row + Inbox badge |
| `src/DiktaMe.Core/Data/HistoryManager.cs` | Add `connector_inbox` table creation in DB migration |

**NOT modified**: `SettingsWindow.xaml` — Connectors have their own separate window, not a tab in existing Settings.

### Existing Patterns to Reuse

| Pattern | Source | Reuse For |
|---------|--------|-----------|
| `NoteWriter.ValidateFilePath()` | `src/DiktaMe.Core/Data/NoteWriter.cs:55` | File path validation (adapt: allow any user-chosen dir) |
| `NoteWriter.AppendAsync()` | `src/DiktaMe.Core/Data/NoteWriter.cs:23` | File write pattern (create dirs, append, log) |
| `LLMProviderFactory` cache | `ConcurrentDictionary<string, T>` with `GetOrAdd()` | HttpClient caching for webhook connector |
| `TtsSettings` record pattern | `src/DiktaMe.Core/Config/AppSettings.cs:341` | Model for `ConnectorSettings` sub-object |
| `SanitizeNulls()` | `src/DiktaMe.Core/Config/SettingsManager.cs:191` | Null-safe deserialization for `Connectors` |
| `SecureStorage` DPAPI | `src/DiktaMe.Core/Security/SecureStorage.cs:15` | Future OAuth token storage (Phase F) |

---

## 7. Implementation Phases (Task Logs)

### Phase A: Core Framework [SPEC_013-A]

> Foundation: `IConnector` interface, `ConnectorPayload`, `ConnectorResult`, `ConnectorManager`, settings model.
> **Session scope**: Can be completed in a single session.

| Task | Description | Files |
|------|-------------|-------|
| A.1 | Create `IConnector` interface, `ConnectorPayload`, `ConnectorResult`, `ConnectorType` | `Connectors/IConnector.cs`, `ConnectorPayload.cs`, `ConnectorResult.cs`, `ConnectorType.cs` |
| A.2 | Create `ConnectorSettings` + `ConnectorConfig` sealed records | `Config/ConnectorSettings.cs` |
| A.3 | Add `ConnectorSettings Connectors` to `AppSettings`, add to `SanitizeNulls()` | `AppSettings.cs`, `SettingsManager.cs` |
| A.4 | Create `ConnectorManager` — resolve connector type → `IConnector` instance, dispatch loop (parallel `Task.WhenAll`), mode filtering, privacy gating, logging | `Connectors/ConnectorManager.cs` |
| A.5 | Wire `ConnectorManager` as singleton in DI, inject into `ControlPanelViewModel`, call `DispatchAutoAsync(result)` in `OnPipelineCompleted` | `App.xaml.cs`, `ControlPanelViewModel.cs` |
| A.6 | Unit tests: dispatch with 0 connectors (no-op), mode filtering, privacy gating (Ghost blocks all), AutoFire vs Manual filtering | `ConnectorManagerTests.cs` |

**Success criteria**: `ConnectorManager.DispatchAutoAsync(result)` is called on every pipeline completion. No connectors registered yet — it's a no-op. All 950+ existing tests pass. Build clean.

**Commit**: `feat: add IConnector framework and ConnectorManager [SPEC_013-A]`

---

### Phase B: Obsidian Connector [SPEC_013-B]

> Highest-value, lowest-effort connector. Direct filesystem write to Obsidian vault.
> **Session scope**: Can be completed in a single session.

| Task | Description | Files |
|------|-------------|-------|
| B.1 | Implement `ObsidianConnector : IConnector` — reads `VaultPath`, `SubFolder`, `NoteStrategy` from `ConnectorConfig.Settings` | `Connectors/ObsidianConnector.cs` |
| B.2 | **Daily note strategy** (default): append to `{VaultPath}/{SubFolder}/{DailyNoteFormat}.md`. Create file with YAML frontmatter header on first entry of the day, append subsequent entries with `---` separator + timestamp. | Same |
| B.3 | **Standalone strategy**: create new `.md` per dictation at `{VaultPath}/{SubFolder}/{FileNameTemplate}.md` with full frontmatter. | Same |
| B.4 | YAML frontmatter format: `date`, `time`, `tags` (from `FrontmatterTags`), `mode`, `wordCount`, `sttProvider`, `llmProvider` | Same |
| B.5 | File name template tokens: `{date}`, `{time}`, `{mode}`, `{title}` (first 5 words, slugified) | Same |
| B.6 | Path validation: reject UNC, require absolute path, require directory exists or can be created. (Adapt `NoteWriter.ValidateFilePath` pattern but allow any user-chosen local dir.) | Same |
| B.7 | Unit tests: file creation (daily + standalone), daily note append (multiple entries same day), frontmatter format, path validation (UNC rejected, valid paths accepted), template expansion | `ObsidianConnectorTests.cs` |

**Obsidian daily note example output:**
```markdown
---
date: 2026-03-14
tags: [diktame, dictate]
---

## 10:30

Quick thought about the project roadmap. We need to prioritize the connector framework.

---

## 14:15

Meeting with Bob went well. Action items: update the spec, schedule follow-up for next week.
```

**Success criteria**: Dictating with an Obsidian connector config creates properly formatted `.md` in the vault. Daily append adds entries with `---` separators. Frontmatter is valid YAML. All tests pass.

**Commit**: `feat: add Obsidian vault connector [SPEC_013-B]`

---

### Phase C: Folder, Webhook, Discord, Streamer.bot [SPEC_013-C]

> Generic file export, outbound HTTP, Discord formatting, Streamer.bot WebSocket.
> **Session scope**: May span 2 sessions. Split point: C.1-C.5 (file + HTTP) then C.6-C.10 (WebSocket).

| Task | Description | Files |
|------|-------------|-------|
| C.1 | Implement `FolderConnector` — write `.md` files to `OutputPath` with optional `FileNameTemplate` | `Connectors/FolderConnector.cs` |
| C.2 | Implement `WebhookConnector` — HTTP POST with structured JSON payload (Section 5.9 schema) | `Connectors/WebhookConnector.cs` |
| C.3 | Webhook: HMAC-SHA256 signing — when `SigningSecret` set, add `X-DiktaMe-Signature: sha256={hex}` header | Same |
| C.4 | Webhook: error handling — 15s timeout, retry once on 5xx, log all failures with response status | Same |
| C.5 | Webhook: privacy gating — replace `dictation.text`/`rawTranscript` with `"[redacted]"` when privacy is `Stats` | Same |
| C.6 | Implement `DiscordWebhookConnector` — Discord webhook format with `content`, optional `embeds[]`, `username`, `avatar_url` | `Connectors/DiscordWebhookConnector.cs` |
| C.7 | Implement `StreamerBotConnector` — `ClientWebSocket` to `ws://{Host}:{Port}{Endpoint}` | `Connectors/StreamerBotConnector.cs` |
| C.8 | Streamer.bot: `DoAction` request with `action.name` + `args` containing dictation text, mode, raw transcript | Same |
| C.9 | Streamer.bot: connection management — lazy connect on first `SendAsync`, auto-reconnect on disconnect, graceful `DisposeAsync` | Same |
| C.10 | Unit tests for all four connectors — mock `HttpClient` via `HttpMessageHandler` for webhooks, mock `WebSocket` for Streamer.bot | Test files |

**Streamer.bot WebSocket protocol** (from docs):
```json
{
  "request": "DoAction",
  "action": { "name": "DiktaMeInput" },
  "args": { "text": "...", "mode": "dictate", "rawTranscript": "..." },
  "id": "unique-guid"
}
```

**Discord embed format:**
```json
{
  "username": "dIKta.me",
  "avatar_url": "...",
  "embeds": [{
    "title": "Dictation",
    "description": "The dictated text here...",
    "color": 5814783,
    "footer": { "text": "via dIKta.me | 42 words | 1.3s" },
    "timestamp": "2026-03-14T10:30:00Z"
  }]
}
```

**Success criteria**: FolderConnector writes files. WebhookConnector POSTs valid JSON (test with webhook.site). Discord posts formatted embeds. Streamer.bot triggers actions. All tests pass.

**Commit**: `feat: add Folder, Webhook, Discord, and Streamer.bot connectors [SPEC_013-C]`

---

### Phase D: Settings UI [SPEC_013-D]

> Connectors settings page in SettingsWindow — CRUD, per-type config, test button.
> **Session scope**: Single session, but XAML-heavy.

| Task | Description | Files |
|------|-------------|-------|
| D.1 | Create `ConnectorsSettingsViewModel` — `ObservableCollection<ConnectorConfig>`, Add/Edit/Remove/Toggle commands, save to `SettingsManager` | `ViewModels/Settings/ConnectorsSettingsViewModel.cs` |
| D.2 | Create `ConnectorsSettingsPage.xaml` — master toggle, list of configured connectors with enable/disable toggles, "Add Connector" button | `Views/Settings/ConnectorsSettingsPage.xaml` + `.cs` |
| D.3 | Add connector type picker — ComboBox or segmented control: Obsidian / Folder / Webhook / Discord / Streamer.bot | Same |
| D.4 | Per-type settings section — dynamically shown based on selected type: folder picker for Obsidian/Folder, URL input for Webhook/Discord, host:port for Streamer.bot | Same |
| D.5 | Mode filter UI — CheckBoxes for which pipeline modes trigger this connector (dictate, note, ask, translate, chat, refine, readselection) | Same |
| D.6 | Auto/Manual toggle with tooltip explaining the difference | Same |
| D.7 | **"Test" button** — fires a synthetic test payload and shows success/failure toast | Same |
| D.8 | Register page in SettingsWindow navigation + DI container | `SettingsWindow.xaml`, `App.xaml.cs` |
| D.9 | Manual trigger: "Send to..." buttons in result notification for non-auto connectors | `ControlPanelViewModel.cs` |

**Success criteria**: User can add an Obsidian connector, browse to vault path, select modes, toggle auto/manual, test it, and see a `.md` appear. Same for webhook with webhook.site URL. Manual connectors show "Send to..." in result notification.

**Commit**: `feat: add Connectors settings page [SPEC_013-D]`

---

### Phase E: Notification Integration + Polish [SPEC_013-E]

> Toast notifications, telemetry, validation, edge cases.
> **Session scope**: Single session.

| Task | Description | Files |
|------|-------------|-------|
| E.1 | Success/failure toasts via `NotificationService.ShowToast()` after connector dispatch — "Saved to Obsidian (42 words)" or "Webhook failed: 401 Unauthorized" | `ConnectorManager.cs` |
| E.2 | Connector dispatch telemetry — add `connector_exports` column or separate table in `HistoryManager` SQLite DB (connector_id, success, timestamp) | `HistoryManager.cs` |
| E.3 | Settings validation on save: valid URL for webhooks (HTTPS preferred, HTTP allowed for localhost), valid directory path for file connectors, valid host:port for Streamer.bot | ViewModel |
| E.4 | Edge case handling: vault path deleted → clear error toast, webhook returns 401/403 → suggest checking URL, disk full → graceful failure | Connector classes |
| E.5 | Documentation: update `DEVELOPMENT_ROADMAP.md` with SPEC_013 completion | Docs |

**Success criteria**: After every pipeline completion with connectors enabled, user sees brief toast confirming success or detailing error. No silent failures. All tests pass (target: 950+ existing + 40+ new = 990+).

**Commit**: `feat: add connector notifications and polish [SPEC_013-E]`

---

### Phase F: Google Ecosystem [SPEC_013-F] (Release 2)

> Google Calendar (read-only) + Gmail (compose). Pull pattern for LLM tool-use.
> **Session scope**: 2-3 sessions. Requires Google Cloud Console project setup.

| Task | Description | Files |
|------|-------------|-------|
| F.1 | Add NuGet packages: `Google.Apis.Calendar.v3`, `Google.Apis.Gmail.v1`, `Google.Apis.Auth`, `MimeKit` | `.csproj` |
| F.2 | Create `DpApiDataStore : IDataStore` — bridges Google's token persistence with our `SecureStorage` (DPAPI) | `Security/DpApiDataStore.cs` |
| F.3 | Add `"google_calendar"`, `"gmail"` to `SecureStorage.ValidProviders` | `SecureStorage.cs` |
| F.4 | Create `GoogleAuthHelper` — loopback OAuth 2.0 + PKCE flow, opens browser, captures redirect, exchanges code | `Connectors/GoogleAuthHelper.cs` |
| F.5 | Implement `GoogleCalendarConnector` — `GetContextAsync("next meeting")` queries `events.list` with `timeMin=now`, formats as natural language | `Connectors/GoogleCalendarConnector.cs` |
| F.6 | Implement `GmailConnector` — `SendAsync()` creates draft via `drafts.create`, `GetContextAsync("Bob's email")` reads recent threads | `Connectors/GmailConnector.cs` |
| F.7 | Register Google connectors as LLM tools in `LLMRouter` — function-calling schema for "get_calendar_events", "create_email_draft", "get_email_thread" | `LLMRouter.cs` or new `ConnectorToolRegistry.cs` |
| F.8 | Settings UI: Google section with "Sign in with Google" button, scope consent display, sign-out button | `ConnectorsSettingsPage.xaml` |
| F.9 | Unit tests: mock Google API responses, OAuth flow state machine, draft creation, calendar query parsing | Test files |

**Success criteria**: "What's my next meeting?" in Chat mode returns calendar data via Ollama. "Draft a reply to Bob" creates a Gmail draft. OAuth flow works with loopback redirect.

**Commit**: `feat: add Google Calendar and Gmail connectors [SPEC_013-F]`

---

### Phase G+: Cloud Destinations (Release 2-3)

> Notion, Slack, CRM — future phases, not detailed here.

| Phase | Connector | Priority |
|-------|-----------|---------|
| G.1 | Notion (OAuth 2.0, page append + DB entry) | Release 2 |
| G.2 | Slack (Incoming Webhook or OAuth) | Release 2 |
| G.3 | Todoist/TickTick (API Key) | Release 2 |
| G.4 | HubSpot CRM (OAuth 2.0) | Release 3, paid tier |
| G.5 | Jira/Linear (API Token) | Release 3 |
| G.6 | Salesforce (OAuth 2.0) | Release 3, enterprise |

---

## 8. Verification Plan

### Unit Tests (target: 40+ new)

| Test Area | Count | What's Covered |
|-----------|-------|---------------|
| `ConnectorManagerTests` | 8-10 | Dispatch with 0/1/N connectors, mode filtering, privacy gating (Ghost/Stats/Full), AutoFire vs Manual, parallel dispatch, error isolation |
| `ObsidianConnectorTests` | 8-10 | Daily note create + append, standalone create, frontmatter format, path validation (UNC rejected), template expansion, empty text skipped |
| `FolderConnectorTests` | 4-5 | File write, path validation, template expansion, directory creation |
| `WebhookConnectorTests` | 6-8 | JSON payload format, HMAC signing, retry on 5xx, timeout handling, privacy redaction, custom headers |
| `DiscordWebhookConnectorTests` | 4-5 | Embed format, username/avatar, long text truncation, error handling |
| `StreamerBotConnectorTests` | 6-8 | DoAction message format, args passing, reconnect logic, connection failure handling |

### Manual E2E Checklist

- [ ] Dictate with Obsidian connector (daily note) → verify append to `YYYY-MM-DD.md` with frontmatter
- [ ] Dictate twice → verify both entries in same daily note with `---` separator
- [ ] Dictate with Obsidian connector (standalone) → verify new `.md` per dictation
- [ ] Dictate with webhook → verify JSON payload at webhook.site matches schema
- [ ] Dictate with HMAC-signed webhook → verify `X-DiktaMe-Signature` header present
- [ ] Dictate with Discord webhook → verify formatted embed in Discord channel
- [ ] Dictate with Streamer.bot connector → verify action triggered in Streamer.bot
- [ ] Test auto-fire vs manual: auto fires silently, manual shows "Send to..." button
- [ ] Test mode filtering: connector enabled only for "note" → verify no fire on "dictate"
- [ ] Test privacy: Ghost mode → verify no connector fires
- [ ] Test privacy: Stats mode → verify text is `[redacted]` in webhook payload
- [ ] Test multiple connectors: Obsidian + webhook + Discord all enabled → all fire in parallel
- [ ] Test "Test" button in settings → verify success toast for valid config, error for invalid
- [ ] Test error handling: invalid webhook URL → verify error toast, no crash

### Build Verification

```bash
dotnet build DiktaMe.sln -c Release    # 0 warnings, 0 errors
dotnet test DiktaMe.sln                # all tests pass (990+)
publish-release.cmd                    # succeeds, no trim warnings from new code
```

---

## 9. Multi-Session Instructions

This spec is designed to be implemented across multiple coding sessions. Each phase is self-contained with a clean commit boundary.

### Session Workflow

1. **Start of session**: Read this spec. Check git log for last `[SPEC_013-*]` commit to know where you left off.
2. **Pick the next uncompleted phase**: Phases are sequential (A → B → C → D → E → F).
3. **Implement all tasks in the phase**: Follow the task table row by row.
4. **Run tests**: `dotnet test DiktaMe.sln` — all tests must pass before committing.
5. **Build check**: `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors.
6. **Commit**: Use the commit message from the phase section.
7. **Update this table**: Mark the phase as complete.

### Progress Tracker

| Phase | Status | Commit | Tests |
|-------|--------|--------|-------|
| A: Core Framework | `PENDING` | — | — |
| B: Obsidian Connector | `PENDING` | — | — |
| C: Folder, Webhook, Discord, SB | `PENDING` | — | — |
| D: Settings UI | `PENDING` | — | — |
| E: Notifications + Polish | `PENDING` | — | — |
| F: Google Ecosystem | `PENDING` | — | — |

### Key Patterns to Follow

- **Settings records**: Follow `TtsSettings` pattern — sealed record, `= new()` defaults, add to `SanitizeNulls()`
- **DI registration**: Singleton for `ConnectorManager` (like `SettingsManager`), Transient for ViewModels
- **HTTP clients**: Use `IHttpClientFactory` or cached `HttpClient` with `ConnectionClose = false` (like `LLMProviderFactory` pattern)
- **File I/O**: Async (`File.AppendAllTextAsync`), create directories with `Directory.CreateDirectory`, validate paths before write
- **Logging**: `Log.Information("ConnectorManager: dispatched {Count} connectors for {Mode}", ...)` — structured Serilog
- **Error handling**: Never let a connector failure crash the pipeline. Catch all exceptions in `ConnectorManager.DispatchAutoAsync`, log + toast, continue.
- **Test mocking**: Use Moq. For `HttpClient`, mock `HttpMessageHandler`. For `WebSocket`, create `IWebSocketClient` abstraction.

### Critical Gotchas (from project memory)

- `SanitizeNulls()` — MUST add `Connectors = s.Connectors ?? new()` or JSON `"Connectors":null` will crash at runtime
- Cross-thread `ObservableCollection` updates — use `DispatcherQueue.TryEnqueue()` when updating UI-bound collections from background connector dispatch
- Moq optional params (CS0854) — always pass `It.IsAny<CancellationToken>()` explicitly
- `x:Bind` converters in `Window` — use computed ViewModel properties instead
- Namespace: never use `DiktaMe.Core.Connectors.System` — shadows BCL

---

## 10. Commit Strategy

Trunk-based development, one commit per phase:

```
feat: add IConnector framework and ConnectorManager [SPEC_013-A]
feat: add Obsidian vault connector [SPEC_013-B]
feat: add Folder, Webhook, Discord, and Streamer.bot connectors [SPEC_013-C]
feat: add Connectors settings page [SPEC_013-D]
feat: add connector notifications and polish [SPEC_013-E]
feat: add Google Calendar and Gmail connectors [SPEC_013-F]
```
