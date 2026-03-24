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

A thin dictation-level vocabulary/correction layer (STT hints, common error patterns) is noted as a future lightweight derivative — it is not part of this spec.

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
    DateTimeOffset Timestamp);

public record UserProfile(
    IReadOnlyDictionary<string, string> GlobalPreferences,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ModePreferences);

public record ConsolidationResult(
    int ObservationsProcessed,
    int PatternsDetected,
    int ProfileUpdatesApplied,
    IReadOnlyList<string> PendingReviewItems);
```

### 2.4 Local Security & Authentication

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
| **Vision (SPEC_002)** | Project context for visual queries | Visual context observations | `OnBeforeLlmProcessing` |
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

---

## 7. User Experience

### 7.1 Invisible by Default

Memory is **background infrastructure**. The user does not interact with it during normal use. AI just "remembers" — prompts are richer, responses are more relevant, corrections are learned.

### 7.2 Memory Settings Page

The only user-facing surface (contributed via `IPluginUIRegistry.AddSettingsPage()`):

- **Master toggle**: Enable/Disable memory (default: disabled — opt-in)
- **User Profile browser**: What memory "knows" about the user (Tier 3). Editable, deletable.
- **Observation browser**: Searchable list of Tier 2 observations. User can delete individual entries.
- **Stats**: Total observations, embeddings count, storage size, oldest/newest entry
- **Retention slider**: Days to retain observations (default: 365)
- **Consolidation**: Manual trigger button, last run timestamp, pending review items
- **Clear All**: With confirmation dialog

### 7.3 Future: Memory Search (Optional)

An optional search textbox in the Memory Settings page — query top-5 similar past interactions. Not a primary feature; the value is in automatic context injection, not manual search.

---

## 8. Implementation Phases

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

### Phase P: Pipeline Hooks + Extraction [SPEC_015-P]

| Task | Description |
|------|-------------|
| P.1 | Subscribe to `OnCompleted` → observation extraction (LLM call) → embed → store in Tier 2 |
| P.2 | Subscribe to `OnBeforeLlmProcessing` → Tier 3 profile injection + Tier 2 semantic search → append to `AdditionalSystemContext` |
| P.3 | Tier 1 session context buffer (in-memory, auto-included in next pipeline) |
| P.4 | Observation extraction LLM prompt (returns typed observations as JSON) |
| P.5 | Embedding throttling via `Channel<T>` queue |
| P.6 | Consolidation process (background, configurable trigger) |
| P.7 | Unit tests: extraction, injection format, dedup, consolidation, profile update |

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

## 9. Relationship to Existing Systems

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

## 10. Explicit Scope Exclusions

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
- Dictation vocabulary/correction thin layer (future lightweight derivative — separate from this spec)
- MCP-based memory access for external agents (future, after SPEC_015 plugin architecture proves out)
- Memory export/import (future, post-V2)

---

## 11. Open Questions

1. **Observation extraction timing**: Synchronous (simple, immediate) vs. asynchronous (zero latency impact, delayed availability)? Both approaches documented — decision deferred to implementation.

2. **Consolidation trigger**: App idle (5 min), app shutdown, scheduled timer, or manual-only? Recommend starting with app shutdown + manual, add idle trigger later.

3. **Cross-mode bleeding**: Should some observation types be inherently global (facts about the user) while others are mode-scoped (professional vocabulary)? Current design: the extraction LLM decides scope per observation.

4. **Correction learning from Refinemmarly**: Should memory actively watch for grammar correction patterns and learn from them? Or is this too "surveillance-y"? Recommend: opt-in via a "Learn from corrections" toggle in Memory Settings.

5. **Profile injection size**: How much Tier 3 profile content can be injected without bloating the LLM prompt? Recommend: cap at ~500 tokens, prioritize by relevance to current mode.

---

## 12. Research Attribution

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

### SurfSense (github.com/MODSetter/SurfSense) — Previously Researched

| Pattern | Applicability |
|---------|--------------|
| **4-category memory** (preference, fact, instruction, context) | Directly adopted as observation types |
| **Hybrid search with RRF** (vector + FTS5) | Future enhancement for Phase Q memory search |
| **Content hashing for dedup** | Adopted in storage schema (`content_hash`) |
| **Capped memory with LRU eviction** | Informed `MaxEntries` setting + retention-based cleanup |

---

## 13. Implementation Readiness

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
