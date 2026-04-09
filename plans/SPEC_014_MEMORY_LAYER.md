# SPEC_014: Memory Layer — Agentic Platform Knowledge Backbone

> **Status:** DRAFT v2 (rewrite from v1 flat vector store)
> **Date:** 2026-03-24
> **Supersedes:** SPEC_014 v1 (2026-03-13, flat vector store with semantic search)
> **Merged into:** [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) (Module 4, Phases O-Q)
> **Goal:** Add a tiered memory layer to dIKta.me that serves as invisible infrastructure for the entire agentic platform — extracting observations from interactions, building a user profile over time, and providing contextual intelligence to every module (Connectors, Meetings, Chaviz, Refine, Vision).
> **Related Specs:**
> - [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](SPEC_013_CONNECTORS_IMPLEMENTATION.md) — Memory enriches connector preset LLM prompts with relevant context
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — Meeting synthesis enriched with memory ("what was discussed about this topic before")
> - [`SPEC_002_VISION.md`](SPEC_002_VISION.md) — Vision results stored as observations; project context recalled for visual queries
> - [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) — **Implementation sprint** (this spec is the design reference; SPEC_015 is the build plan)
> - [`SPEC_016_V2.1_REFINEMMARLY.md`](SPEC_016_V2.1_REFINEMMARLY.md) — Correction patterns feed into memory; style preferences consumed for smarter grammar
> - [`SPEC_017_V2.1_ORCHESTRATOR.md`](SPEC_017_V2.1_ORCHESTRATOR.md) — Primary memory consumer: `recall`/`remember` tools, conversation context, preference-aware Chaviz responses

---

## 1. Executive Summary

dIKta.me is evolving from a dictation tool into an **agentic workflow orchestration platform** with voice as the primary input. The platform spans a complexity spectrum:

- **Simple**: Single Whisper model → text injection (zero overhead, no memory needed)
- **Medium**: Dictation → LLM refinement → Obsidian/webhooks via Connectors (memory enriches)
- **Complex**: Voice → Chaviz orchestrator → tool calls → multi-destination dispatch → TTS response (memory is essential)

**Memory is the connective tissue that makes agents effective across this entire spectrum.** Without it, every session starts cold. With it, the platform develops coherence — it knows your vocabulary, your workflows, your context, and your preferences.

This spec defines a **3-tier memory model** inspired by two external architectures:

- **Honcho** (Plastic Labs) — observation extraction, pattern consolidation, novelty detection
- **LOOM Engine** — layered memory hierarchy, governance, world isolation, signal vs. noise

The Memory Layer is **invisible infrastructure**, not a user-facing search feature. Every module consumes it transparently via `IMemoryLayer` (DI) and `PipelineEventBus` hooks. Users interact with it only through a settings page where they can review, edit, or delete what memory "knows."

A **Teach-by-Correction** feature is included in this spec (Section 7) — a user-initiated hotkey flow that captures manual text corrections and stores them as persistent vocabulary/formatting memories.

### Platform Architecture: Memory at the Center

```
┌─────────────────────────────────────────────────────────────┐
│                    VOICE INPUT (primary)                      │
│         Hotkey → Mic → STT → text (existing core)            │
└──────────────────────────┬──────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────────┐
        ▼                  ▼                      ▼
  ┌───────────┐    ┌──────────────┐      ┌──────────────────┐
  │ SIMPLE    │    │ MEDIUM       │      │ COMPLEX          │
  │ Whisper → │    │ Dictation →  │      │ Chaviz →         │
  │ text      │    │ LLM refine → │      │ multi-turn →     │
  │ injection │    │ connectors   │      │ tool calls →     │
  │           │    │              │      │ multi-agent      │
  │ (no memory│    │ (memory      │      │ orchestration    │
  │  needed)  │    │  enriches)   │      │                  │
  └───────────┘    └──────┬───────┘      │ (memory is       │
                          │              │  essential)       │
                          ▼              └────────┬─────────┘
                  ┌───────────────┐               │
                  │   MEMORY      │◄──────────────┘
                  │  (SPEC_014)   │
                  │               │
                  │ The knowledge │
                  │ backbone that │
                  │ makes agents  │
                  │ effective     │
                  │ across time   │
                  └───────┬───────┘
                          │
         ┌────────────────┼────────────────────┐
         ▼                ▼                    ▼
   ┌───────────┐  ┌──────────────┐   ┌──────────────────┐
   │CONNECTORS │  │ MEETINGS     │   │ REFINEMMARLY     │
   │(SPEC_013) │  │ (SPEC_001)   │   │ (SPEC_016)       │
   │           │  │              │   │                  │
   │ MCP, APIs │  │ Scribe, AI   │   │ Grammar check,   │
   │ webhooks  │  │ synthesis,   │   │ style learning,  │
   │ websocket │  │ "ask this    │   │ correction       │
   │ filesystem│  │  meeting"    │   │ patterns         │
   └───────────┘  └──────────────┘   └──────────────────┘
```

---

## 2. Architectural Design

### 2.1 Three-Tier Memory Model

The Memory Layer replaces the v1 flat vector store with a **3-tier hierarchy** adapted from LOOM's 4-layer model and Honcho's observation levels:

#### Tier 3: User Profile

Stable knowledge about the user — persists across sessions, modes, and workflows.

| Aspect | Detail |
|--------|--------|
| **Contains** | Writing style per mode, vocabulary preferences, correction patterns, domain expertise, language preferences, project context |
| **Injected** | Always, into every LLM system prompt (via `OnBeforeLlmProcessing` hook) |
| **Updated by** | Consolidation process (promotes recurring Tier 2 patterns). User can edit/delete via Memory Settings. |
| **Lifetime** | Persistent until user edits or deletes |
| **Inspired by** | LOOM L4 (Identity) + Honcho Peer Card (dynamic behavioral summaries) |

#### Telos-Inspired Profile Structure (from LOOM Operator Telos + supermemory)

> **Research source:** [supermemory](https://github.com/supermemoryai/supermemory) (#1 on LongMemEval, LoCoMo, ConvoMem benchmarks) + LOOM Engine Operator Profile system (`/git/loom-engine/knowledge/04_Operations/Operator/`).

Tier 3 should adopt a **structured sub-section model** inspired by LOOM's 5-file Operator Profile rather than a flat `UserProfile` record:

| Section | LOOM Equivalent | Contains | Mutability |
|---------|----------------|----------|------------|
| **Identity** | `operator-telos.md` | Name, role, domain expertise, language preferences, values/constraints | **User-editable only** — never auto-updated by consolidation |
| **Work Style** | `operator-profile.md` | Writing style per mode, tone preferences, formality level, vocabulary patterns | Auto-updated by consolidation, user-reviewable |
| **Interaction Modes** | `operator-modes.md` | Per-dictation-mode preferences (Professional=formal, Casual=loose) | Maps to existing `ModePreferences` dict |
| **History** | `operator-history.md` | Observation log — dated entries of meaningful preference changes | Append-only, prevents silent drift |
| **Tools & Context** | `operator-tools-and-knowledge.md` | Known apps, workflows, project context, active integrations | Auto-enriched by Connectors plugin |

**Critical rule from LOOM:** Identity section is **immutable during execution** — only the user can change it via Memory Settings page. Consolidation can propose changes but never auto-apply to Identity.

**Supermemory pattern:** Their `profile` endpoint returns pre-computed `static` + `dynamic` fields in ~50ms. Map: Identity + Work Style = static, History + Tools & Context = dynamic. `GetProfileAsync()` should return a **pre-formatted prompt fragment** (string), not a data structure — pre-compute on consolidation, cache in memory, serve fast.

#### Tier 2: Observations

Atomic facts extracted from interactions — typed, tagged, mode-scoped.

| Aspect | Detail |
|--------|--------|
| **Contains** | Facts, preferences, instructions, and context extracted from pipeline outputs |
| **Types** | `fact` ("User works at Contoso"), `preference` ("User prefers formal tone"), `instruction` ("Always use metric units"), `context` ("Meeting with Bob covered Q3 budget") |
| **Queried** | Semantic vector search, filtered by mode scope + type + time range |
| **Updated by** | Post-pipeline observation extraction (automatic when memory is enabled) |
| **Lifetime** | Retained per `RetentionDays` setting, consolidated periodically |
| **Inspired by** | LOOM L3 (Knowledge) + Honcho Explicit/Deductive observations |

#### Tier 1: Session Context

Ephemeral buffer for current session — auto-included in next pipeline call.

| Aspect | Detail |
|--------|--------|
| **Contains** | Recent dictation topics, current Chaviz conversation thread, active connector presets context |
| **Injected** | Automatically into next pipeline call within the session |
| **Updated by** | Pipeline events in real-time |
| **Lifetime** | Cleared on app close. Never persisted. |
| **Inspired by** | LOOM L1 (Active Session) — "Execution is temporary" |

### 2.2 Observation Types

Each Tier 2 observation is categorized (adapted from SurfSense research + Honcho observation levels):

```csharp
public enum ObservationType
{
    /// <summary>Verifiable information: "User works at Contoso", "Project X uses Kubernetes"</summary>
    Fact,
    /// <summary>User preference or style signal: "Prefers formal tone in Professional mode"</summary>
    Preference,
    /// <summary>Explicit directive from user: "Always use metric units", "Never abbreviate company names"</summary>
    Instruction,
    /// <summary>Ephemeral situational context: "Currently working on Q3 budget review"</summary>
    Context,
}
```

### 2.3 Interface Definition

```csharp
public interface IMemoryLayer
{
    // --- Core CRUD (from v1, unchanged) ---
    Task<MemoryEntryId> StoreAsync(
        string content,
        MemoryMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryResult>> SearchAsync(
        string queryText,
        int limit = 10,
        double minSimilarity = 0.7,
        MemorySearchFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(MemoryEntryId id, CancellationToken cancellationToken = default);
    Task<bool> ClearAllAsync(CancellationToken cancellationToken = default);
    Task<MemoryStats> GetStatsAsync(CancellationToken cancellationToken = default);

    // --- Observation Extraction (NEW — from Honcho deriver) ---
    Task<IReadOnlyList<Observation>> ExtractObservationsAsync(
        string text,
        string sourceMode,
        string? sourcePipelineId = null,
        CancellationToken cancellationToken = default);

    // --- User Profile (NEW — Tier 3) ---
    Task<UserProfile> GetProfileAsync(CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default);

    // --- Profile as Prompt Fragment (NEW — from supermemory pattern) ---
    // Returns pre-formatted string ready for system prompt injection.
    // Pre-computed on consolidation, cached, served in <50ms.
    // Chaviz and pipeline hooks inject this directly — no serialization at query time.
    Task<string> GetProfilePromptFragmentAsync(CancellationToken cancellationToken = default);

    // --- Consolidation (NEW — from Honcho dreamer) ---
    Task<ConsolidationResult> ConsolidateAsync(CancellationToken cancellationToken = default);
}

public record MemorySearchFilter(
    string? ModeScope = null,
    ObservationType? Type = null,
    DateTimeOffset? After = null,
    DateTimeOffset? Before = null,
    int? Tier = null);

public record Observation(
    MemoryEntryId Id,
    string Content,
    ObservationType Type,
    string? ModeScope,
    double Confidence,
    bool IsNovel,
    string? SourcePipelineId,
    DateTimeOffset Timestamp,
    MemoryEntryId? SupersedesId = null,   // NEW (supermemory): links to the observation this replaces
    bool IsSuperseded = false,             // NEW (supermemory): true when a newer observation replaces this
    DateTimeOffset? ExpiresAt = null);     // NEW (supermemory): auto-forget after this timestamp (null = permanent)

public record UserProfile(
    IReadOnlyDictionary<string, string> GlobalPreferences,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ModePreferences);

public record ConsolidationResult(
    int ObservationsProcessed,
    int PatternsDetected,
    int ProfileUpdatesApplied,
    IReadOnlyList<string> PendingReviewItems);
```

### 2.4 Version Chains, Auto-Expiry & Dedup (from supermemory)

> **Research source:** supermemory uses `updatesMemoryId` + `relation` ("updates"|"extends"|"derives") for version chains, `forgetAfter` for auto-expiry, and `contentHash` for dedup.

**Version chains:** When a new observation supersedes an existing one (e.g., "I moved to SF" replaces "I live in NYC"):
1. Semantic search existing observations with similarity > 0.85
2. If match found with same `ObservationType`, set `SupersedesId` on new observation and `IsSuperseded = true` on old
3. Superseded observations excluded from search results but retained for audit trail
4. `Context`-type observations default `ExpiresAt = now + RetentionDays`; `Fact`/`Preference` default to `null` (permanent until superseded)

**Content hash dedup:** `ExtractObservationsAsync` should compute SHA-256 of input text and check against a `recent_hashes` ring buffer (last 1000 entries, 24h TTL). If match found, skip extraction entirely — saves an LLM call.

### 2.5 Per-Turn Memory Cache (from supermemory AI SDK middleware)

During a single Chaviz conversation turn, the orchestrator may invoke multiple tools (`recall`, `search_email`, `check_calendar`) that each query memory. To avoid redundant SQLite+VSS queries:

```csharp
public sealed class MemoryTurnCache : IDisposable
{
    // Created at turn start, disposed at turn end
    // Caches SearchAsync results keyed by (queryText, filter hash)
    // Caches GetProfilePromptFragmentAsync result
    // Fresh instance per new user message — stale on next turn
}
```

The orchestrator creates a `MemoryTurnCache` at the start of each user turn and passes it to tool implementations via DI scope.

### 2.6 Local Security & Authentication

- **Strictly Local**: All memory data resides encrypted on the user's device
- **Integration with SecureStorage**: Memory encryption keys stored using existing DPAPI-based SecureStorage
- **Privacy-Level Aware**: Memory respects the user's privacy level settings
- **No Cloud Sync**: Nothing leaves the user's device unless explicitly exported

---

## 3. Consumer + Producer Map

Every module in the platform has a defined relationship with memory — what it **consumes** and what it **produces**:

| Module | Consumes from Memory | Produces into Memory | Hook |
|--------|---------------------|---------------------|------|
| **Chaviz (SPEC_017)** | `recall` tool (semantic search), conversation context, preference-aware responses, tool selection hints | Conversation observations, user preference signals, command patterns | `IMemoryLayer` via DI + `OnBeforeLlmProcessing` |
| **Connectors (SPEC_013)** | Context-aware LLM re-processing in presets | Connector activity patterns | `OnBeforeLlmProcessing` in preset LLM pass |
| **Meetings (SPEC_001)** | "What was discussed about this topic before?", participant history, synthesis enrichment | Meeting observations (topics, decisions, action items, participants) | `IMemoryLayer.SearchAsync()` before synthesis |
| **Refinemmarly (SPEC_016)** | User's style preferences and correction history for smarter grammar suggestions | Correction patterns ("user always changes X to Y"), style observations per mode | `OnBeforeLlmProcessing` + post-correction extraction |
| **Vision (SPEC_002)** | Visual history search ("find that error message screenshot"), project context for visual queries, past OCR text recall | **Visual Memory entries**: structured metadata per screenshot (description, keywords, content_type, ocr_text, dominant_colors, app_name, window_title) + embedding. Background-indexed after every Save/Clipboard/OCR/Table action. | `OnCompleted` (background indexing) + `OnBeforeLlmProcessing` (recall) + `IMemoryLayer.SearchAsync()` (Chat visual search) |
| **Core Dictation** | User profile injected into LLM system prompt | Raw material for observation extraction | `OnBeforeLlmProcessing` + `OnCompleted` |
| **Future plugins** | `IMemoryLayer` from DI — same interface, zero coupling | `IMemoryLayer.StoreAsync()` | DI resolution |

---

## 4. Implementation Approach

### 4.1 Vector Storage Technology

**Recommendation: SQLite with sqlite-vss extension** (unchanged from v1)

- Single file storage (like existing history.db)
- No additional dependencies beyond SQLite
- ACID transactions, familiar SQL interface
- .NET bindings available

### 4.2 Embedding Model

**Primary**: `all-MiniLM-L6-v2` (384 dimensions) via ONNX Runtime (already a dependency for Kokoro TTS)

- Runs entirely locally — no data leaves device
- ~50ms per embedding generation
- Good quality for semantic similarity at small scale

### 4.3 Storage Schema

```sql
-- Tier 2: Observations (vector-indexed)
CREATE TABLE IF NOT EXISTS observations (
    id              TEXT PRIMARY KEY,
    content         TEXT NOT NULL,
    content_hash    BLOB NOT NULL,
    vector          BLOB NOT NULL,
    observation_type TEXT NOT NULL,    -- 'fact', 'preference', 'instruction', 'context'
    tier            INTEGER NOT NULL DEFAULT 2,
    mode_scope      TEXT,              -- NULL = global, 'professional', 'casual', etc.
    confidence      REAL NOT NULL DEFAULT 1.0,
    is_novel        INTEGER NOT NULL DEFAULT 0,
    source_ids      TEXT,              -- JSON array of parent observation IDs (for deduced observations)
    source_pipeline_id TEXT,           -- Link to HistoryManager session
    metadata        JSON NOT NULL,
    created_at      INTEGER NOT NULL,
    privacy_level   INTEGER NOT NULL
);

-- Vector similarity search index (sqlite-vss extension)
CREATE VIRTUAL TABLE IF NOT EXISTS observations_vss USING vss0(
    embedding(384)
);

-- Tier 3: User Profile (key-value with mode scope)
CREATE TABLE IF NOT EXISTS user_profile (
    key             TEXT NOT NULL,
    mode_scope      TEXT,              -- NULL = global
    value           TEXT NOT NULL,
    updated_at      INTEGER NOT NULL,
    source_observation_ids TEXT,       -- JSON array: which observations led to this entry
    PRIMARY KEY (key, mode_scope)
);

-- Consolidation log (audit trail)
CREATE TABLE IF NOT EXISTS consolidation_log (
    id              TEXT PRIMARY KEY,
    run_at          INTEGER NOT NULL,
    observations_processed INTEGER NOT NULL,
    patterns_detected INTEGER NOT NULL,
    profile_updates_applied INTEGER NOT NULL,
    details         JSON
);
```

### 4.4 Observation Extraction Pipeline

After each pipeline completion, the Memory Plugin extracts atomic observations:

```
Pipeline completes (dictation/ask/chat/meeting/vision/refine)
    │
    ├──► [1] Store raw result in HistoryManager (existing, unchanged)
    │
    ├──► [2] EXTRACT OBSERVATIONS
    │       LLM call: "Extract atomic observations from this text"
    │       Input: pipeline output text + mode + context
    │       Output: List<Observation> with type + confidence
    │       Timing: sync or async (configurable, see Open Questions)
    │
    ├──► [3] DEDUP / NOVELTY CHECK
    │       Compare new observations against existing via embedding similarity
    │       >0.95 similarity → skip (duplicate)
    │       <0.5 similarity to everything → flag as novel (high-signal)
    │
    ├──► [4] EMBED + STORE
    │       Generate embeddings → store in SQLite+VSS
    │       Tag with tier, type, mode_scope, source_pipeline_id
    │
    └──► [5] UPDATE SESSION CONTEXT (Tier 1)
            Add to current session's observation buffer
```

**Extraction timing** (decision deferred to implementation):
- **Synchronous**: LLM extracts inline, ~200-500ms added. Simplest, observations immediately available.
- **Asynchronous**: Pipeline completes normally, extraction via `Channel<T>` queue in background. Zero latency impact, but observations delayed.

### 4.5 Consolidation Process

Lightweight background process adapted from Honcho's dreamer (simplified — single pass, not separate deduction/induction specialists):

```
Trigger: App idle (5 min) | App shutdown | Timer | Manual

[1] GATHER recent Tier 2 observations (configurable window)

[2] PATTERN DETECTION (single LLM call)
    "Identify recurring patterns, preferences, or correction habits"
    Output: Candidate profile entries with confidence + source observation IDs

[3] PROFILE UPDATE
    Compare candidates against existing Tier 3 profile
    Apply new/updated entries (auto-apply or queue for user review)

[4] CLEANUP
    Merge duplicate/superseded observations
    Age out low-confidence observations beyond retention window
    Log consolidation run to audit trail
```

### 4.6 Mode-Scoped Retrieval

Adapted from LOOM's world isolation principle:

- Every observation is tagged with its source mode (`mode_scope`)
- `NULL` mode_scope = global (applies across all modes)
- Default query: current mode observations + global observations
- Some observations are inherently global (user name, company, language) — the extraction LLM categorizes this
- Professional vocabulary stays scoped to Professional mode; personal topics stay in Personal mode

### 4.7 Novelty Detection

Adapted from Honcho's surprisal scoring — simplified for desktop app scale:

- After embedding a new observation, compute cosine similarity against existing observations
- **>0.95 similarity**: Skip — this is a duplicate (e.g., "User works at Contoso" already stored)
- **<0.5 similarity to ALL existing**: Flag as `is_novel = true` — high-signal new information
- **Between 0.5-0.95**: Normal storage — related but distinct
- Prevents memory bloat from repetitive dictations while highlighting genuinely new information

---

## 5. Privacy-First Design

The Memory Layer strictly adheres to dIKta.me's privacy model:

| Level | Behavior |
|-------|----------|
| **Ghost (0)** | Memory layer disabled entirely. No storage, no extraction, no injection. |
| **Stats (1)** | Store only metadata and observation types (no retrievable content). Profile injection disabled. |
| **Balanced (2)** | Store encrypted observations and profile. Content retrievable with user consent. |
| **Full (3)** | Full storage with PII scrubbing if enabled. All features active. |

- Vectors are considered metadata (don't directly reveal content) but protected at Balanced+ levels
- Encryption at rest via DPAPI keys from existing `SecureStorage`

---

## 6. Cross-Module Memory Flows

Concrete examples of how memory creates value across the platform:

### 6.1 Correction Learning Loop (Refinemmarly → Memory → Dictation)

User corrects "teh" → "the" three times via grammar popup → Memory extracts observation: `preference: "user corrects 'teh' to 'the'"` → Future dictation LLM prompt includes this correction pattern → Fewer errors over time.

### 6.2 Meeting Continuity (Meetings → Memory → Meetings)

Meeting A discusses "Q3 budget, action: Bob sends revised numbers" → Memory stores as observations → Next meeting with Bob, synthesis prompt includes: "Previous context: Q3 budget discussion, Bob was to send revised numbers" → Richer AI output.

### 6.3 Chaviz Context Awareness (All → Memory → Chaviz)

User dictates about Kubernetes all morning → Memory has observations tagged "Professional mode, topic: Kubernetes" → User asks Chaviz "what was I working on?" → `recall` tool returns relevant observations → Chaviz: "You've been working on Kubernetes deployment configs — 6 dictations about ingress rules and service mesh."

### 6.4 Connector Intelligence (Memory → Connectors)

Connector preset "Meeting Debrief" fires after a meeting → Its LLM re-processing pass gets injected context: "This meeting was about Project X. Previous decisions: use REST over GraphQL, deadline March 30" → Better meeting minutes auto-generated for Obsidian vault.

### 6.5 Orchestrator Workflow Memory (Chaviz → Memory → Chaviz)

Chaviz session: user asks to "draft an email to Sarah about the budget" → Chaviz uses `recall` tool to find previous budget discussions → drafts email with relevant context → stores this interaction as observation → Next time user mentions "budget email", Chaviz remembers the previous draft.

### 6.6 Visual Memory (Vision → Memory → Chat/Chaviz)

Every screenshot captured through Vision (Ctrl+Alt+S) is automatically indexed into the Memory Layer as a rich, searchable entry. This transforms ephemeral screenshots into **persistent visual knowledge** — the user can later ask "what was that error I saw yesterday?" or "find the table from that dashboard" and get results.

#### Capture-Time Metadata (Free — No AI)

These fields are extracted at capture time with zero latency cost:

| Field | Source | Example |
|-------|--------|---------|
| `file_path` | Save location in `AppData/DiktaMe/vision/` | `vision_20260325_143022.png` |
| `timestamp` | `DateTime.UtcNow` | `2026-03-25T14:30:22Z` |
| `app_name` | `GetForegroundWindow()` → process name | `devenv`, `chrome`, `excel` |
| `window_title` | `GetWindowText()` on foreground HWND | `"Program.cs - DiktaMe - Visual Studio"` |
| `monitor_index` | Which display the capture came from | `0`, `1` |
| `region_type` | Full screen vs user-snipped region | `full_screen`, `snipped_region` |
| `source_action` | Which VisionAction the user chose | `Save`, `Clipboard`, `Ocr`, `Table`, `Chat`, `Note` |
| `dimensions` | Width × height of captured image | `1920x1080`, `640x480` |
| `file_size_bytes` | PNG file size on disk | `245760` |

#### AI-Indexed Metadata (Background — Post-Action)

After the user's chosen action completes, a **background indexing task** runs a single structured vision prompt on the local model (minicpm-v). This adds zero latency to the user's flow:

| Field | Source | Example |
|-------|--------|---------|
| `description` | AI one-line summary | `"Python traceback showing KeyError in data_loader.py line 42"` |
| `keywords` | AI-generated tags (array) | `["error", "python", "traceback", "KeyError", "data_loader"]` |
| `content_type` | AI classification | `screenshot`, `photo`, `document`, `table`, `diagram`, `code`, `error_message`, `chat`, `webpage` |
| `ocr_text` | Full text extraction (cached) | `"Traceback (most recent call last):\n  File..."` |
| `dominant_colors` | Pixel sampling (no AI needed) | `["#1e1e1e", "#d4d4d4", "#569cd6"]` — dark theme IDE |

#### Indexing Prompt

Single structured prompt, optimized for speed on local vision models:

```
Analyze this screenshot and return JSON only:
{
  "description": "one-line summary of what this shows (max 30 words)",
  "keywords": ["tag1", "tag2", ...],  // 3-8 descriptive keywords
  "content_type": "screenshot|document|table|diagram|code|error_message|chat|webpage|photo",
  "ocr_text": "all visible text, preserve formatting"
}
```

#### Background Indexing Flow

```
User action completes (Save/Clipboard/OCR/Table/Chat/Note)
    │
    └──► [fire-and-forget] BackgroundIndexVisionAsync(imageData, captureMetadata)
            │
            ├──► [1] STRUCTURED VISION PROMPT (local model, ~1-3s)
            │       Returns: description, keywords, content_type, ocr_text
            │
            ├──► [2] DOMINANT COLOR EXTRACTION (pixel sampling, ~5ms)
            │       Sample 9 points → quantize to nearest web colors
            │
            ├──► [3] EMBED description + keywords (MiniLM, ~50ms)
            │       Vector for semantic similarity search
            │
            ├──► [4] STORE AS TIER 2 OBSERVATION
            │       Type: context
            │       Content: description
            │       metadata: { all fields from both tables above }
            │       mode_scope: NULL (global — screenshots are cross-mode)
            │       source_pipeline_id: link to vision pipeline session
            │
            └──► [5] FTS INDEX ocr_text (for LIKE/FTS5 text search)
```

#### Storage Schema Extension

Add to the existing `observations` table (no new table needed — visual memories are standard Tier 2 observations with richer metadata):

```sql
-- Visual Memory entries use the standard observations table.
-- The `metadata` JSON column carries all vision-specific fields:
-- {
--   "vision": true,
--   "file_path": "...",
--   "app_name": "...",
--   "window_title": "...",
--   "monitor_index": 0,
--   "region_type": "snipped_region",
--   "source_action": "Ocr",
--   "dimensions": "1920x1080",
--   "file_size_bytes": 245760,
--   "content_type": "error_message",
--   "keywords": ["error", "python", "traceback"],
--   "dominant_colors": ["#1e1e1e", "#d4d4d4"],
--   "ocr_text": "full text here..."
-- }

-- FTS5 index for fast text search across OCR content
CREATE VIRTUAL TABLE IF NOT EXISTS vision_fts USING fts5(
    observation_id,
    ocr_text,
    window_title,
    keywords,
    content='',
    tokenize='porter unicode61'
);
```

#### Query Patterns

| User Query | Search Strategy | Returns |
|-----------|----------------|---------|
| "find that error message" | Embed query → vector search on description + keyword embeddings | Top-N screenshots with error_message content_type |
| "what was on the dashboard" | Embed → vector search, boost `content_type = 'table'` | Dashboard screenshots with table data |
| "show me what I captured from VS Code" | Filter `app_name = 'Code'` + time range | All VS Code screenshots |
| "find the Python code I saw yesterday" | FTS5 search `ocr_text MATCH 'python'` + date filter | Screenshots containing Python code |
| "what did I look at this morning?" | Filter `timestamp > today_start` | Chronological screenshot timeline |

#### Consumer Integration

- **Chat (QuickChat)**: User asks "find that screenshot of the error" → memory search returns matching visual entries → Chat displays description + offers to re-attach the image from `file_path`
- **Chaviz (SPEC_017)**: `recall` tool queries visual memory alongside text memory → orchestrator can reference past screenshots in multi-turn conversation
- **Vision itself**: When capturing a new screenshot, inject recent visual context → "You previously captured a similar view showing X" → more contextual AI responses

#### Design Decisions

| Decision | Rationale |
|----------|----------|
| **Background indexing (fire-and-forget)** | Zero latency impact on user's chosen action. User doesn't wait for indexing. |
| **Local model only for indexing** | Privacy-first — screenshot content never leaves the device for indexing. User's chosen action (Clipboard/OCR/Table) may use cloud, but the index always uses local. |
| **Standard Tier 2 observations** | No separate table — visual memories participate in the same search, consolidation, and retention as all other observations. The `metadata.vision = true` flag distinguishes them. |
| **FTS5 for OCR text** | Vector search is great for semantic queries ("find errors") but FTS5 is better for exact text matches ("find KeyError in data_loader.py"). Hybrid search (vector + FTS5 with RRF) is the long-term path. |
| **Dominant colors via pixel sampling** | No AI needed — sample 9 points (3×3 grid), quantize to nearest named color. Enables queries like "that dark-themed screenshot" or "the blue dashboard." |
| `mode_scope = NULL` (global) | Screenshots are inherently cross-mode — a code screenshot is useful context whether you're in Professional, Casual, or any mode. |
| **Capture-time metadata is always collected** | Even if memory is disabled or indexing fails, the free metadata (app_name, window_title, dimensions) is stored in the vision file's companion JSON sidecar for future indexing when memory is enabled. |

---

## 7. Teach-by-Correction

A user-initiated learning feature that lets users teach dIKta.me their preferred vocabulary, formatting, and spelling through natural correction — **fix it once, it remembers forever**.

### 7.1 Concept

During dictation, the pipeline may inject text that doesn't match the user's intended formatting — brand names, technical jargon, domain-specific casing, acronyms, foreign words, etc. Today the user manually corrects these every time. With Teach-by-Correction, one correction is enough:

1. User dictates → pipeline injects `"Dictame"` (wrong)
2. User manually corrects it to `"dIKta.me"` (right)
3. User selects the corrected text and presses the **Teach hotkey**
4. System retrieves the original injection from the **Oops clipboard** (already tracked)
5. LLM diffs original vs. correction → extracts the correction rule
6. Stored as a persistent `instruction` observation in the Memory Layer
7. Future pipeline LLM prompts include this rule → correct formatting from now on

### 7.2 Architecture

```
User selects corrected text → Teach Hotkey pressed
    │
    ├──► [1] CAPTURE
    │       "After" = selected text (from clipboard/selection API)
    │       "Before" = last injected text (from Oops buffer)
    │
    ├──► [2] DIFF + RULE EXTRACTION (LLM call)
    │       Prompt: "Compare these two texts. Identify what the user
    │                corrected and extract a formatting/vocabulary rule."
    │       Input:  { before: "...", after: "..." }
    │       Output: { trigger: "dictame", correction: "dIKta.me",
    │                 rule: "Brand name, always use this exact casing",
    │                 scope: "global" }
    │
    ├──► [3] STORE AS OBSERVATION
    │       Type: instruction
    │       Content: "When user says 'dictame', format as 'dIKta.me'"
    │       Mode scope: from LLM extraction (global or mode-specific)
    │       Confidence: 1.0 (user-initiated = highest confidence)
    │       Tag: source_type = "teach_correction"
    │
    └──► [4] CONFIRMATION
            Toast notification: "Learned: dictame → dIKta.me ✓"
```

### 7.3 Memory Integration

Teach-by-Correction observations are standard `instruction`-type Tier 2 observations with a `teach_correction` source tag. They are:

- **Injected** into future LLM prompts via the existing `OnBeforeLlmProcessing` hook alongside other memory context
- **Retrievable** via semantic search ("how does the user format brand names?")
- **Manageable** through the Memory Settings page (user can review, edit, or delete learned corrections)
- **Consolidatable** — recurring corrections may be promoted to Tier 3 User Profile entries (e.g., "User has specific brand formatting preferences")

### 7.4 Key Design Decisions

| Decision | Rationale |
|----------|----------|
| **Oops buffer as "before" source** | Already implemented, zero new infrastructure needed |
| **LLM diff, not string diff** | The user may correct only part of the injected text; the LLM can identify the meaningful change within a larger text block |
| **Confidence = 1.0** | User explicitly initiated the correction — this is the highest-signal memory source |
| **Stored as `instruction` type** | Corrections are directives ("always format X as Y"), not preferences or facts |
| **Global scope by default** | Vocabulary corrections typically apply across all modes unless the LLM detects mode-specific context |

### 7.5 Examples

| Spoken | Pipeline Output | User Correction | Learned Rule |
|--------|----------------|-----------------|-------------|
| "dictame" | Dictame | dIKta.me | Brand name: always `dIKta.me` |
| "kubernetes" | kubernetes | Kubernetes | Capitalize product name |
| "c sharp" | C Sharp | C# | Programming language shorthand |
| "my company acme corp" | ACME Corp | Acme Corporation | Company name: full form, title case |
| "doctor smith" | Doctor Smith | Dr. Smith | Use abbreviated title |

---

## 8. User Experience

### 8.1 Invisible by Default

Memory is **background infrastructure**. The user does not interact with it during normal use. AI just "remembers" — prompts are richer, responses are more relevant, corrections are learned.

### 8.2 Memory Settings Page

The primary user-facing surface (contributed via `IPluginUIRegistry.AddSettingsPage()`):

- **Master toggle**: Enable/Disable memory (default: disabled — opt-in)
- **User Profile browser**: What memory "knows" about the user (Tier 3). Editable, deletable.
- **Observation browser**: Searchable list of Tier 2 observations. User can delete individual entries.
- **Learned Corrections browser**: View/edit/delete Teach-by-Correction rules (filtered by `teach_correction` tag)
- **Stats**: Total observations, embeddings count, storage size, oldest/newest entry
- **Retention slider**: Days to retain observations (default: 365)
- **Consolidation**: Manual trigger button, last run timestamp, pending review items
- **Clear All**: With confirmation dialog

### 8.3 Future: Memory Search (Optional)

An optional search textbox in the Memory Settings page — query top-5 similar past interactions. Not a primary feature; the value is in automatic context injection, not manual search.

---

## 9. Implementation Phases

Mapped to SPEC_015 Phases O-Q:

### Phase O: Core Memory Infrastructure [SPEC_015-O]

| Task | Description |
|------|-------------|
| O.1 | `IMemoryLayer` interface with 3-tier model, observation types, search filters |
| O.2 | Records: `Observation`, `UserProfile`, `MemorySearchFilter`, `ConsolidationResult`, `MemoryStats` |
| O.3 | SQLite VSS extension integration — native extension loading |
| O.4 | `SqliteMemoryStore : IMemoryLayer` — vector schema, CRUD, similarity search |
| O.5 | Local embedding: ONNX `all-MiniLM-L6-v2` (384 dims) |
| O.6 | Privacy gating per tier |
| O.7 | `MemoryPluginSettings` via `IPluginSettingsStore` |
| O.8 | `MemoryPlugin : IPlugin` entry class |
| O.9 | Novelty detection (embedding similarity dedup) |
| O.10 | Unit tests: store/search/delete, tiers, privacy, similarity, dedup |

### Phase P: Pipeline Hooks + Extraction + Teach-by-Correction [SPEC_015-P]

| Task | Description |
|------|-------------|
| P.1 | Subscribe to `OnCompleted` → observation extraction (LLM call) → embed → store in Tier 2 |
| P.2 | Subscribe to `OnBeforeLlmProcessing` → Tier 3 profile injection + Tier 2 semantic search → append to `AdditionalSystemContext` |
| P.3 | Tier 1 session context buffer (in-memory, auto-included in next pipeline) |
| P.4 | Observation extraction LLM prompt (returns typed observations as JSON) |
| P.5 | Embedding throttling via `Channel<T>` queue |
| P.6 | Consolidation process (background, configurable trigger) |
| P.7 | Unit tests: extraction, injection format, dedup, consolidation, profile update |
| P.8 | **Teach-by-Correction hotkey handler**: capture selection + retrieve Oops buffer → send to LLM for diff/rule extraction → store as `instruction` observation with `teach_correction` tag |
| P.9 | **Teach-by-Correction LLM prompt**: structured extraction of trigger word, correction, rule description, and scope from before/after text pair |
| P.10 | **Teach-by-Correction toast confirmation**: visual feedback on successful correction learning |
| P.11 | Unit tests: teach-by-correction flow, rule extraction, storage, injection into future prompts |

### Phase P-V: Visual Memory Indexing [SPEC_015-PV]

| Task | Description |
|------|-------------|
| PV.1 | **Capture-time metadata collector**: `VisionCaptureMetadata` record with app_name (via `GetForegroundWindow` → process name), window_title (`GetWindowText`), monitor_index, region_type, source_action, dimensions, file_size_bytes. Collected in `RunVisionPipelineAsync` before action dispatch. |
| PV.2 | **Background vision indexer**: `BackgroundIndexVisionAsync(byte[] imageData, string mimeType, VisionCaptureMetadata meta, string filePath)` — fire-and-forget after user action completes. Runs local vision model with structured JSON prompt → extracts description, keywords, content_type, ocr_text. |
| PV.3 | **Dominant color extraction**: Pixel sampling (3×3 grid) from PNG bytes → quantize to nearest named web colors. Pure computation, no AI. |
| PV.4 | **Store as Tier 2 observation**: Description as `content`, full metadata JSON (all fields) in `metadata` column, `observation_type = 'context'`, `mode_scope = NULL`, `metadata.vision = true` flag. Embed description + keywords for vector search. |
| PV.5 | **FTS5 index for OCR text**: `vision_fts` virtual table for fast text search across `ocr_text`, `window_title`, `keywords`. Populated alongside observation storage. |
| PV.6 | **JSON sidecar for offline metadata**: Write `{filename}.meta.json` alongside each saved PNG with capture-time metadata. Enables future batch re-indexing when memory is enabled later. |
| PV.7 | **Batch re-indexer**: `VisionIndexService.ReindexFolderAsync(string folderPath)` — point at the `vision/` folder, read all PNGs + their `.meta.json` sidecars, index any not yet in the DB. Useful for bootstrapping memory from existing screenshot history. |
| PV.8 | **Index report generator**: After batch indexing, produce a `vision_index_report.md` summarizing: total images indexed, content_type distribution, top keywords, date range covered, any failures. |
| PV.9 | **Chat integration**: When user asks about past screenshots in QuickChat, query visual memory (vector + FTS5 hybrid) → return matching entries with descriptions + file paths → offer to re-attach image. |
| PV.10 | Unit tests: metadata collection, structured prompt parsing, color extraction, FTS5 search, sidecar round-trip, batch re-index |

### Phase Q: Memory Settings Page [SPEC_015-Q]

| Task | Description |
|------|-------------|
| Q.1 | `MemorySettingsViewModel` — enable/disable, stats, retention, profile browser |
| Q.2 | `MemorySettingsPage.xaml` — contributed via `IPluginUIRegistry.AddSettingsPage()` |
| Q.3 | Profile editor: view/edit/delete Tier 3 entries |
| Q.4 | Observation browser: search, filter by type/mode, delete individual entries |
| Q.5 | Retention enforcement: purge observations older than `RetentionDays` on plugin enable |
| Q.6 | Optional: memory search textbox |
| Q.7 | Unit tests: settings round-trip, retention purge, stats |

---

## 10. Relationship to Existing Systems

| System | Purpose | Relationship to Memory Layer |
|--------|---------|------------------------------|
| HistoryManager | Structured pipeline session logs | Source of content for observation extraction; memory links to sessions via `source_pipeline_id` |
| ConversationManager | Chat conversation threads | Source of content; memory enables semantic search across conversations |
| MetricsCollector | Performance metrics | Unrelated; memory focuses on content semantics |
| SecureStorage | Encrypted secrets protection | Protects memory encryption keys |
| Connectors Plugin | External app integrations | Consumer: memory enriches connector LLM prompts. Producer: connector activity feeds observations. |
| Meetings Plugin | Meeting recording + synthesis | Consumer: historical context for synthesis. Producer: meeting observations. |
| PipelineEventBus | Plugin communication | Primary integration mechanism — `OnCompleted` and `OnBeforeLlmProcessing` hooks |

---

## 11. Explicit Scope Exclusions

### What NOT to Build

**From Honcho (too heavy for desktop):**
- Full dialectic agent (agentic reasoning at query time — too heavy for inline dictation latency)
- Multi-peer modeling (single-user desktop app)
- Full dreamer with separate deduction + induction specialists (simplify to single consolidation pass)
- Webhook event system (overkill for local app)
- Surprisal trees (geometric tree structures for anomaly detection — simple cosine similarity suffices)

**From LOOM (too rigid for desktop):**
- L4 immutable identity kernel (desktop app, not multi-agent governance framework)
- META governance agent (replace with user-facing Memory Settings UI)
- Agent University / versioning (no autonomous agents in dIKta.me — yet)
- Replication Layer (single-user, no distributed validation needed)
- Cross-World explicit authorization protocol (simple mode-based tagging suffices)

**Deferred:**
- ~~Dictation vocabulary/correction thin layer~~ → **Superseded by Section 7: Teach-by-Correction** (now part of this spec)
- MCP-based memory access for external agents (future, after SPEC_015 plugin architecture proves out)
- Memory export/import (future, post-V2)

---

## 12. Open Questions

1. **Observation extraction timing**: Synchronous (simple, immediate) vs. asynchronous (zero latency impact, delayed availability)? Both approaches documented — decision deferred to implementation.

2. **Consolidation trigger**: App idle (5 min), app shutdown, scheduled timer, or manual-only? Recommend starting with app shutdown + manual, add idle trigger later.

3. **Cross-mode bleeding**: Should some observation types be inherently global (facts about the user) while others are mode-scoped (professional vocabulary)? Current design: the extraction LLM decides scope per observation.

4. **Correction learning from Refinemmarly**: Should memory actively watch for grammar correction patterns and learn from them? Or is this too "surveillance-y"? Recommend: opt-in via a "Learn from corrections" toggle in Memory Settings.

5. **Profile injection size**: How much Tier 3 profile content can be injected without bloating the LLM prompt? Recommend: cap at ~500 tokens, prioritize by relevance to current mode.

6. **Teach-by-Correction hotkey assignment**: Should this be a dedicated global hotkey, or reuse the existing hotkey system with a modifier? Recommend: configurable in Settings, default to `Ctrl+Shift+T` or similar.

7. **Partial corrections**: What if the user only corrects one word within a longer injection? The LLM diff handles this, but should we store the full context or just the atomic correction? Recommend: store only the atomic correction rule, but include the surrounding context as metadata for retrieval.

---

## 13. Research Attribution

### Honcho (github.com/plastic-labs/honcho)

Open-source memory library for stateful AI agents by Plastic Labs.

| Honcho Pattern | dIKta.me Adaptation |
|----------------|---------------------|
| **Deriver** (background observation extraction from conversations) | Post-pipeline observation extraction (Phase P.1) |
| **Dreamer** (deduction + induction specialists for pattern consolidation) | Single-pass consolidation process (Phase P.6) |
| **Surprisal scoring** (geometric embedding distance for anomaly detection) | Simple cosine similarity dedup + novelty flagging |
| **Multi-level observations** (Explicit → Deductive → Inductive → Contradictions) | 4-type observations (fact, preference, instruction, context) |
| **Peer Card** (dynamic behavioral/preference summaries) | Tier 3 User Profile |
| **Dialectic Agent** (agentic reasoning at query time) | Not adopted — too heavy for inline latency. Standard semantic search instead. |

### LOOM Engine (user's cognitive architecture project)

Governance-first 4-layer memory hierarchy for long-horizon AI collaboration.

| LOOM Pattern | dIKta.me Adaptation |
|-------------|---------------------|
| **L4 Identity** (immutable, operator-defined) | Tier 3 User Profile (mutable by user and consolidation, not immutable) |
| **L3 Knowledge** (persistent structural memory) | Tier 2 Observations (persistent, typed, searchable) |
| **L2 Episodic** (condensed session summaries) | Observation extraction condenses sessions into atomic facts |
| **L1 Active** (ephemeral, always cleared) | Tier 1 Session Context (cleared on app close) |
| **World isolation** (hard cognitive sandboxes) | Mode-scoped observations and retrieval |
| **HAII Extraction** (high-signal moment identification) | Novelty detection (is_novel flag) |
| **Governance / META** (authorized memory evolution) | User-facing Memory Settings UI (review, edit, delete) |
| **One-way authority** (higher layers constrain lower) | Tier 3 profile influences all LLM prompts; Tier 2 feeds Tier 3 only via consolidation |
| **TEMPO** (pacing/depth control) | Not adopted — dIKta.me doesn't have reasoning depth modes |

### Airweave (github.com/airweave-ai/airweave) — Researched 2026-03-27

Open-source context retrieval layer for AI agents (YC X25). FastAPI + Vespa + Temporal. 50+ data connectors.

| Pattern | Applicability |
|---------|--------------|
| **Temporal relevance scoring** (`score = similarity × decay(age, recency_bias)`, bias 0.0–1.0, default 0.3) | **Adopt.** Our `SearchAsync` is pure vector — a 6-month-old fact ranks equally to yesterday's. Add `RecencyBias` float to `MemoryPluginSettings`, apply decay in search scoring. ~20 lines, high value for "what was I working on?" queries. |
| **Hybrid search** (semantic + keyword + LLM reranking, tiered: instant/classic/agentic) | **Promote from future to Phase O.** FTS5 already planned for vision OCR — extend to all observations. Vector misses exact matches on proper nouns, codes, brand names. RRF (Reciprocal Rank Fusion) to merge vector + FTS5 results. |

### MemPalace (github.com/milla-jovovich/mempalace) — Researched 2026-04-08

Self-described "highest-scoring AI memory system ever benchmarked" — 96.6% on LongMemEval R@5 with zero LLM calls. Contrarian architecture built on ChromaDB + pure regex extraction + verbatim chunk storage.

**Core claim:** LLM-based extraction (Mem0, Mastra, supermemory) introduces information loss. "User prefers Postgres" discards the *why* — the tradeoffs, alternatives considered. Verbatim storage + good embeddings outperforms extraction at smaller scales.

**Architecture overview:**

| Layer | Tokens | Content | When loaded |
|-------|--------|---------|-------------|
| L0 Identity | ~100 | Plain-text user profile (`~/.mempalace/identity.txt`) | Always |
| L1 Essential Story | ~500–800 | Pre-computed narrative of top-15 highest-importance memories | Always (on wake-up) |
| L2 On-Demand | ~200–500 | Wing/room-filtered retrieval on topic match | On demand |
| L3 Deep Search | Unlimited | Full semantic search via ChromaDB | Explicit query |

**Patterns worth considering at implementation time:**

| Pattern | MemPalace implementation | Consideration for SPEC_014 |
|---------|------------------------|---------------------------|
| **Essential Story layer** | Pre-computed ~500-token narrative of top 15 most important memories, always injected on wake-up. Separate from identity/profile. | SPEC_014's `GetProfilePromptFragmentAsync()` covers Tier 3 profile but has no equivalent "greatest hits" narrative. An L1-style pre-computed memory summary could complement profile injection — especially useful for Chaviz cold-start context. |
| **Regex pre-filter before LLM extraction** | Pure regex pattern matching (22 decision patterns, 18 preference patterns, 28+ milestone patterns) with sentiment-based disambiguation. No LLM in extraction hot path — 96.6% benchmark. | Phase P.1 currently calls LLM for every pipeline completion. A regex pre-filter could gate the LLM call: if no interesting patterns detected, skip extraction entirely. Saves cost on mundane dictations ("buy groceries"). |
| **Verbatim chunk storage alongside observations** | Stores raw verbatim text chunks (800 chars, 100-char overlap) — no extraction or summarization in storage layer. Semantic search finds the raw context. | SPEC_014 extracts atomic observations and discards source text (other than `source_pipeline_id` link to HistoryManager). Dual-path (verbatim chunks + extracted observations) could raise recall for edge cases where extraction loses context. Not a blocking concern at MVP scale but worth benchmarking. |
| **Deterministic chunk IDs for idempotent upsert** | `drawer_{wing}_{room}_{md5(source + chunk_index)[:16]}` — same source = same ID = safe to re-mine without duplicates. | Useful pattern for the batch re-indexer (Phase PV.7) and any future "re-extract" pass over HistoryManager sessions. Consider for observation IDs derived from `{source_pipeline_id}:{extraction_index}`. |
| **Wing-based grouping beyond mode-scope** | Memories organized by Wing (person/project) → Room (named idea) → Hall (memory type). Cross-domain "tunnels" (rooms spanning 2+ wings) surfaced automatically. | SPEC_014 uses `mode_scope` (Professional/Casual/etc.) but has no project/person axis. A user could have 50 dictations about "Project X" across multiple modes — no way to query by project. Consider a `context_tag` metadata field (free-form, LLM-assigned) as a lightweight alternative to formal Wing structure. |
| **Temporal knowledge graph** | Separate SQLite table with `valid_from` / `valid_to` per fact. `kg.invalidate("Max", "has_issue", "sports_injury")` marks facts expired. `as_of` date queries. | SPEC_014 uses `SupersedesId` + `IsSuperseded` boolean for version chains. Not queryable by date ("what did I know about this on Jan 15?"). A temporal validity column pair (`valid_from`, `valid_to`) on the `observations` table could replace the boolean flag with richer semantics at near-zero schema cost. |

**Where SPEC_014 is ahead of MemPalace:**
- Tier 1 session context (MemPalace has no ephemeral buffer — every lookup hits ChromaDB)
- Consolidation / dreamer process (MemPalace has no promotion from observations to profile)
- `MemoryTurnCache` per-turn dedup (MemPalace has no equivalent)
- Mode-scoped observations (MemPalace has Wing/Room but no mode semantics)
- Privacy tiers (MemPalace is local-only, no privacy model)
- Vision memory (MemPalace has no image indexing)

### SurfSense (github.com/MODSetter/SurfSense) — Previously Researched

| Pattern | Applicability |
|---------|--------------|
| **4-category memory** (preference, fact, instruction, context) | Directly adopted as observation types |
| **Hybrid search with RRF** (vector + FTS5) | Future enhancement for Phase Q memory search |
| **Content hashing for dedup** | Adopted in storage schema (`content_hash`) |
| **Capped memory with LRU eviction** | Informed `MaxEntries` setting + retention-based cleanup |

---

## 14. Implementation Readiness

This specification builds upon:
- Existing SQLite infrastructure (HistoryManager, ConversationManager)
- Privacy framework already implemented in SettingsManager
- Plugin architecture from SPEC_015 (PipelineEventBus, IPlugin, PluginManager)
- Dependency injection pattern used throughout the codebase
- ONNX Runtime already a dependency (Kokoro TTS)
- Event-driven pipeline architecture

No breaking changes to existing systems required. The Memory Layer adds capability through extension and integration points.

---

*This spec positions the Memory Layer as foundational infrastructure for dIKta.me's evolution from a dictation tool into an agentic workflow orchestration platform. Memory is what transforms a stateless voice tool into a system that learns, adapts, and coordinates intelligently across sessions, modes, and modules.*
