# SPEC_014: Memory Layer

> **Status:** DRAFT
> **Date:** 2026-03-13
> **Supersedes:** N/A (Net-new architecture)
> **Goal:** Add a semantic memory layer to dIKta.me that enables context-aware AI interactions by storing and retrieving meaningful patterns from user interactions while maintaining strict local-first, privacy-first principles.

## 1. Executive Summary

As dIKta.me evolves into a more context-aware assistant, the ability to remember and recall relevant past interactions becomes crucial for providing personalized, intelligent responses. The existing storage systems (HistoryManager, ConversationManager) excel at structured data storage and retrieval but lack semantic understanding capabilities.

The Memory Layer introduces vector-based semantic storage that enables:
- Semantic search across past conversations and dictations
- Contextual recall for enhancing LLM prompts with relevant history
- Pattern recognition in user behavior and preferences
- Knowledge graph capabilities for connecting related concepts
- All while maintaining the local-first, privacy-first ethos of dIKta.me

This layer works alongside existing storage systems rather than replacing them, providing enhanced retrieval capabilities for AI features while preserving the audit trail and structured data benefits of the current SQLite-based approach.

## 2. Architectural Design

### 2.1 Core Concepts

The Memory Layer consists of two complementary systems:

1. **Vector Memory Store**: Stores embeddings of text content (conversations, dictations, notes) for semantic similarity search
2. **Concept Graph** (Optional Future Extension): Tracks relationships between entities, topics, and concepts mentioned in user interactions

### 2.2 Interface Definition

All memory operations operate through a standardized C# interface within `DiktaMe.Core`:

```csharp
public interface IMemoryLayer
{
    /// <summary>
    /// Stores a piece of text content with associated metadata for future semantic retrieval.
    /// </summary>
    Task<MemoryEntryId> StoreAsync(
        string content,
        MemoryMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves semantically similar content based on a query vector or text.
    /// </summary>
    Task<IReadOnlyList<MemoryResult>> SearchAsync(
        string queryText,
        int limit = 10,
        double minSimilarity = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves memories associated with specific metadata filters.
    /// </summary>
    Task<IReadOnlyList<MemoryResult>> GetByMetadataAsync(
        Func<MemoryMetadata, bool> metadataFilter,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a memory entry by ID.
    /// </summary>
    Task<bool> DeleteAsync(MemoryEntryId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all memories (respecting privacy settings).
    /// </summary>
    Task<bool> ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the memory store.
    /// </>
    Task<MemoryStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

public record MemoryEntryId(string Value);
public record MemoryResult(
    MemoryEntryId Id,
    string Content,
    MemoryMetadata Metadata,
    double SimilarityScore);

public record MemoryMetadata(
    string SourceMode,           // e.g., "dictate", "ask", "chat"
    DateTimeOffset Timestamp,
    string? SttProvider = null,
    string? LlmProvider = null,
    string? ConversationId = null,
    IReadOnlyList<string> Tags = null,
    PrivacyLevel Level = PrivacyLevel.Balanced);
```

### 2.3 Local Security & Authentication

- **Strictly Local**: All memory data resides encrypted on the user's device
- **Integration with SecureStorage**: Memory encryption keys are stored using the existing DPAPI-based SecureStorage system
- **Privacy-Level Aware**: Memory storage respects the user's privacy level settings (Ghost, Stats, Balanced, Full)
- **Optional Encryption**: For Balanced+ privacy levels, memory vectors can be encrypted at rest
- **No Cloud Sync**: Unlike commercial AI memory systems, nothing leaves the user's device unless explicitly exported

## 3. Implementation Approach

### 3.1 Vector Storage Technology Selection

For a local-first .NET application, we evaluate several options:

#### Option A: Embedded Vector Search in SQLite (Recommended)
- Use SQLite with the `sqlite-vss` extension (Vector Search in SQLite)
- Pros: 
  - Single file storage (like existing history.db)
  - No additional dependencies beyond SQLite
  - ACID transactions
  - Familiar SQL interface
  - Proven in production (used by Discord, etc.)
  - .NET bindings available
- Cons:
  - Requires native extension loading
  - Slightly more complex deployment

#### Option B: Microsoft ML Vector Search
- Uses `Microsoft.ML.VectorSearch` NuGet package
- Pros:
  - Pure .NET managed code
  - Good integration with ML.NET ecosystem
  - Active Microsoft development
- Cons:
  - Separate storage mechanism from existing SQLite
  - Less mature than dedicated vector DBs
  - May require more memory overhead

#### Option C: Faiss.NET or Similar
- Bindings to Facebook's FAISS library
- Pros:
  - Industry standard for vector similarity search
  - High performance
  - Multiple index types (IVF, HNSW, etc.)
- Cons:
  - Native dependency complexity
  - More moving parts
  - Overkill for desktop application scale

**Recommendation**: Option A (SQLite with sqlite-vss) for its simplicity, single-file nature, and alignment with existing architecture.

### 3.2 Integration Points

The Memory Layer integrates with existing systems at several points:

1. **Pipeline Completion**: After each pipeline run (Dictate, Ask, Chat, etc.), store the result in memory
2. **Context Enhancement**: Before LLM processing, query memory for relevant context to enhance prompts
3. **User-Initiated Recall**: Allow users to search their memory via the UI
4. **Privacy Compliance**: Respect privacy levels when storing and retrieving memories

### 3.3 Privacy-First Design

The Memory Layer strictly adheres to dIKta.me's privacy model:

- **Ghost Level (0)**: No memory storage whatsoever
- **Stats Level (1)**: Store only metadata and vector hashes (no retrievable content)
- **Balanced Level (2)**: Store encrypted vectors and metadata; content only retrievable with user consent
- **Full Level (3)**: Store vectors and content with PII scrubbing if enabled

Vectors themselves are considered metadata since they don't directly reveal content, but we still apply appropriate protection based on privacy level.

## 4. User Experience & Workflows

### 4.1 Automatic Memory Storage

After any successful pipeline execution:
1. Extract text content (transcription, LLM response, etc.)
2. Generate embedding using local embedding model
3. Store in vector memory with associated metadata
4. Link to existing HistoryManager/ConversationManager records via IDs

### 4.2 Contextual AI Enhancement

When user initiates an AI interaction (Ask, Chat, etc.):
1. Convert user query to embedding
2. Search memory layer for semantically similar past interactions
3. Retrieve top-K most relevant memories
4. Inject relevant memories into LLM system prompt as contextual examples
5. Process enhanced prompt with LLM
6. Store new interaction in memory

### 4.3 User-Facing Memory Features

1. **Memory Search UI**: Dedicated interface for searching past interactions by meaning
2. **Memory Timeline**: Visualization of when certain topics/concepts were discussed
3. **Memory Insights**: Statistics about most discussed topics, interaction patterns
4. **Selective Forgetting**: Ability to delete specific memories or time ranges
5. **Memory Export/Import**: Encrypted backup and restore capabilities (user-initiated only)

## 5. Technical Implementation Details

### 5.1 Embedding Model Selection

For local, privacy-first operation:
- **Primary**: Use a small, efficient embedding model like:
  - `all-MiniLM-L6-v2` (384 dimensions, fast, good quality)
  - `paraphrase-MiniLM-L3-v2` (even smaller, 384 dim)
  - Custom distilled model trained on user interaction patterns
- **Fallback**: TF-IDF or BM25 for very low-resource scenarios
- **Privacy**: Model runs entirely locally; no data leaves device

### 5.2 Storage Schema (SQLite with VSS)

```sql
CREATE TABLE IF NOT EXISTS memory_vectors (
    id           TEXT PRIMARY KEY,      -- UUID
    content_hash BLOB NOT NULL,        -- Hash of original content for verification
    vector       BLOB NOT NULL,        -- Float32 vector embedding
    metadata     JSON NOT NULL,        -- JSON serialized MemoryMetadata
    created_at   INTEGER NOT NULL,     -- Unix timestamp
    privacy_level INTEGER NOT NULL     -- Stored privacy level at time of storage
);

-- Vector similarity search index (provided by sqlite-vss extension)
CREATE VIRTUAL TABLE IF NOT EXISTS memory_vss USING vss0(
    embedding(384)  -- 384-dimensional vectors for MiniLM models
);
```

### 5.3 Implementation Phases

#### Phase 1: Core Vector Storage
- Implement IMemoryLayer interface with SQLite+VSS backend
- Add embedding generation using local ONNX model
- Basic Store and Search operations
- Privacy level compliance

#### Phase 2: Pipeline Integration
- Auto-storage after pipeline completion
- Context enhancement for Ask and Chat pipelines
- Memory-aware LLMRouter integration

#### Phase 3: User Interface
- Memory search dialog in Settings or new Memory tab
- Integration with existing UI patterns
- Memory statistics and management controls

#### Phase 4: Advanced Features
- Concept extraction and tagging
- Temporal decay weighting
- Memory consolidation (forgetting less important memories)
- Cross-modal memory (linking audio, text, actions)

## 6. Security & Privacy Considerations

### 6.1 Data Protection
- Memory encryption keys derived from user's DPAPI-protected master key
- Vectors stored encrypted at rest for Balanced+ levels
- Memory access logged when privacy level permits
- Secure deletion when memories are removed

### 6.2 Privacy Compliance
- Ghost level: Memory layer disabled entirely
- Stats level: Only store non-reversible hashes and metadata
- Balanced level: Store encrypted vectors; require user consent for content retrieval
- Full level: Store vectors with optional PII scrubbing applied to source content

### 6.3 User Control
- Explicit opt-in for memory features (off by default)
- Granular controls for what gets stored (modes, content types)
- Easy one-click wipe for all memories
- Transparent reporting of what is stored and for how long

## 7. Open Questions & Future Considerations

### 7.1 Embedding Model Updates
- How to handle embedding model version changes?
- Strategy: Store model version with each memory; provide migration path

### 7.2 Memory Lifespan
- Should memories expire? Implement TTL or importance-based forgetting?
- Start with infinite retention (respecting existing history retention), add expiration later

### 7.3 Cross-Device Sync (If Ever Desired)
- While strictly local-first, design could optionally support encrypted sync
- Would require explicit user consent and end-to-end encryption

### 7.4 Performance Optimization
- Memory-mapped vector storage for faster loading
- Quantization of vectors for smaller storage footprint
- Async embedding generation to avoid UI blocking

## 8. Relationship to Existing Systems

The Memory Layer complements rather than replaces existing storage:

| System | Purpose | Relationship to Memory Layer |
|--------|---------|------------------------------|
| HistoryManager | Structured pipeline session logs | Source of content for memory storage; memory enhances recall of these sessions |
| ConversationManager | Chat conversation threads | Source of content; memory enables semantic search across conversations |
| MetricsCollector | Performance metrics | Unrelated; memory layer focuses on content semantics |
| SecureStorage | Encrypted secrets protection | Protects memory encryption keys |
| Connectors Framework | External app integrations | Memory layer can enhance connector context with relevant personal history |

## 9. Implementation Readiness

This specification builds upon:
- Existing SQLite infrastructure (HistoryManager, ConversationManager)
- Privacy framework already implemented in SettingsManager
- Local-first architecture established in Connectors SPEC
- Dependency injection pattern used throughout the codebase
- Event-driven pipeline architecture

No breaking changes to existing systems are required; the Memory Layer adds capability through extension and integration points.

---
*This MEMORY LAYER specification enables dIKta.me to evolve from a transactional tool to a truly context-aware assistant while maintaining its core principles of local execution, user privacy, and transparent data handling.*