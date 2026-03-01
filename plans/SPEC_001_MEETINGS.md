# SPEC_001: Meeting Intelligence Module ("Scribe")

> **Status:** DRAFT
> **Date:** 2026-03-01
> **Supersedes:** V1 `SPEC_003_SCRIBE_LAYER.md` (conceptual, never implemented)
> **Competitive References:** [Granola](https://www.granola.ai), [Fellow.ai](https://fellow.ai), [HyprNote](https://github.com/fastrepl/hyprnote)
> **Priority:** TBD (post-V2 launch)

---

## 1. Executive Summary

dIKta.me currently captures ephemeral voice input — you speak, it types, it forgets. The Meeting Intelligence module adds a **persistent session layer**: record a meeting, take rough notes, and let AI synthesize a polished artifact (minutes, action items, follow-up emails).

This is the highest-value feature gap vs. the current competitive landscape. Granola ($14/mo) and Fellow ($7-25/mo) both target this space with cloud-dependent SaaS models. dIKta.me's angle: **local-first, privacy-respecting, no meeting bots, single-app experience** that also handles everyday dictation.

---

## 2. Competitive Landscape (March 2026)

### 2.1 Granola ($0 / $14 / $35 per user/month)

**How it works:** Records system audio (no bot), user types rough notes during meeting, AI merges transcript + notes into structured output post-meeting.

| Feature | Details |
|---------|---------|
| Recording | System audio capture — no bot in call. Zoom, Meet, Webex, Slack, Teams |
| Transcription | Cloud-based, multi-language |
| AI Outputs | Summaries, action items, follow-up emails, budget extraction, objection lists, blog drafts, participant lists |
| Templates | Customer discovery, user interviews, 1:1s, pitches, standups (customizable) |
| Chat | "Ask your meeting" — query transcripts post-meeting ("What's their budget?", "List objections") |
| Sharing | Public links, Slack, email, Notion, CRM (Attio, HubSpot), ATS |
| MCP | Official MCP server — Claude/Cursor/ChatGPT can read meeting notes. Enterprise: early access beta |
| Integrations | Slack, Notion, Attio, HubSpot, Affinity, Zapier |
| Platforms | macOS, Windows (desktop), iOS (mobile), web |
| Privacy | Opt-out of model training. Enterprise: org-wide auto-deletion, admin controls |
| Free tier | AI notes, limited history, chat, templates, multi-language |

**Key strength:** Simple UX — "just a notepad that listens." No bot friction.
**Key weakness:** Cloud-only processing, no local option, limited free tier.

### 2.2 Fellow.ai ($0 / $7 / $15 / $25 per user/month)

**How it works:** Full meeting lifecycle management — pre-meeting briefs, recording (bot OR botless), post-meeting AI processing. Strong enterprise/compliance focus.

| Feature | Details |
|---------|---------|
| Recording | Bot-based OR botless (system audio). Zoom, Meet, Teams, Slack Huddles |
| Transcription | Cloud-based, 92 languages, "very good" accuracy |
| AI Outputs | Summaries, action items, decisions, topic grouping, keyword tracking, Sales AI recaps |
| Pre-meeting | AI briefs from prior conversations, collaborative agenda editing, 500+ templates |
| Chat | "Ask Fellow" — searchable meeting queries across all meetings |
| Action items | Auto-extracted, synced to Asana, ClickUp, Monday, Linear, Jira |
| CRM Sync | Salesforce, HubSpot — auto-push meeting notes to deal/contact records |
| Integrations | 50+ tools: Slack, Asana, Monday, ClickUp, Linear, Jira, Notion, Confluence, Salesforce, HubSpot, Zapier, API, MCP Server |
| Platforms | Web, desktop, mobile |
| Privacy | SOC 2 Type 2, HIPAA, GDPR. No AI training on customer data |
| Free tier | 10 AI notes, 10 recordings, transcription, action items |
| Enterprise | SSO, domain controls, user provisioning, advanced recording permissions |

**Key strength:** Full lifecycle (before/during/after). Deep enterprise integrations. Compliance.
**Key weakness:** Complex — requires team buy-in. Free plan very limited. Bot mode can be intrusive.

### 2.3 HyprNote (Open Source)

| Feature | Details |
|---------|---------|
| Recording | System audio capture (no bot) |
| Processing | Truly local — Ollama + LM Studio |
| UX | Split view: Left = user memos, Right = passive transcription |
| Synthesis | AI merges memos + transcript into structured output |
| Platforms | macOS first, Windows/Linux planned for 2026 |

**Key strength:** Privacy-first, open source, local processing.
**Key weakness:** macOS only, early stage, no integrations.

### 2.4 Opportunity Matrix: What Competitors Do That We Don't

| Capability | Granola | Fellow | HyprNote | dIKta.me V2 | Opportunity |
|------------|:-------:|:------:|:--------:|:-----------:|-------------|
| System audio recording (no bot) | Yes | Yes | Yes | No | **HIGH** — must-have |
| User notes during meeting | Yes | Yes | Yes | No | **HIGH** — core differentiator |
| AI synthesis (transcript + notes) | Yes | Yes | Yes | No | **HIGH** — the "magic" |
| Post-meeting chat with transcript | Yes | Yes | No | No | **HIGH** — high user value |
| Customizable output templates | Yes | Yes | No | No | **MEDIUM** — nice-to-have at launch |
| Pre-meeting briefs | No | Yes | No | No | **LOW** — Fellow-unique, enterprise |
| Action item extraction | Yes | Yes | No | No | **HIGH** — universal need |
| Follow-up email generation | Yes | Yes | No | No | **MEDIUM** — quick win |
| CRM sync (Salesforce, HubSpot) | Yes | Yes | No | No | **LOW** — enterprise, defer |
| Task tool sync (Asana, Jira, etc.) | No | Yes | No | No | **LOW** — enterprise, defer |
| MCP server (expose notes to AI tools) | Yes | Yes | No | No | **MEDIUM** — growing ecosystem |
| Multi-language transcription | Yes | Yes | No | Partial (STT) | **MEDIUM** — already have STT infra |
| Speaker diarization | ? | Yes | No | No | **MEDIUM** — hard locally |
| Mobile app | Yes | Yes | No | No | **LOW** — defer (Windows-first) |
| Local/offline processing | No | No | Yes | Partial (Ollama) | **HIGH** — our differentiator |
| 500+ agenda templates | No | Yes | No | No | **LOW** — over-engineered for us |
| Keyword tracking / Sales AI | No | Yes | No | No | **LOW** — niche |

---

## 3. dIKta.me's Strategic Angle

### What We Already Have (Reusable)

| Existing Asset | Reuse For |
|----------------|-----------|
| `AudioRecorder` (NAudio) | Long-form session recording (needs disk-streaming mode) |
| Cloud STT (Deepgram) | Meeting transcription (already supports streaming) |
| `LLMRouter` + all providers | Synthesis (Gemini, Anthropic, OpenAI, Ollama) |
| `HistoryManager` (SQLite) | Session storage model |
| `PromptRepository` | Template prompts for synthesis |
| `SecureStorage` + API keys | Provider auth for cloud processing |
| WinUI 3 app shell | Scribe window (new page in existing app) |
| `NotificationService` | "Meeting processed" toast |
| `AudioDucker` | Auto-duck other apps during recording |
| `NoteWriter` | Append timestamped entries to Markdown file — reuse for session artifacts |
| `NotePipeline` | Voice → STT → optional LLM → text. Reuse as voice-note capture during sessions |
| `NoteSettings` | File path, timestamp format, LLM toggle — extend for session export |

### What Makes Us Different

1. **Local-first option** — Process meetings with Ollama. No data leaves your machine.
2. **No subscription** — BYOK model. Use your own Gemini/Anthropic/OpenAI key. No $14/mo.
3. **No meeting bot** — System audio capture only. No "Fellow AI has joined" awkwardness.
4. **Single app** — Dictation + Meeting Intelligence in one tool. Not two separate products.
5. **Privacy-first** — PII scrubber, local processing, no telemetry. Already built (E.3).
6. **Voice notes during meetings** — Neither Granola nor Fellow let you speak a quick note mid-meeting. We can, because we already have the Note hotkey infrastructure.

---

## 4. Feature Specification

### 4.0 Notes Integration: The Bridge Between Quick Notes and Sessions

The existing Notes feature (`Ctrl+Alt+N`) and the new Scribe sessions share a natural connection. Rather than building two isolated systems, they intertwine through a **session-aware routing model**:

#### Two Modes, One Hotkey

```
┌─────────────────────────────────────────────────────────────┐
│                     Ctrl+Alt+N pressed                       │
│                            │                                 │
│                  Active Scribe session?                       │
│                    /              \                           │
│                  NO                YES                        │
│                  │                  │                         │
│          [Standalone Mode]   [Session Mode]                  │
│          Same as today:      Voice note captured:            │
│          STT → LLM format   STT → LLM format                │
│          → append to         → append to session notepad     │
│          diktame-notes.md    → tagged with timestamp offset  │
│                              → also append to notes file     │
│                                (configurable)                │
└─────────────────────────────────────────────────────────────┘
```

**Standalone mode (no active session):** Identical to today. Hotkey → record voice → STT → optional LLM formatting → append to `diktame-notes.md`. Zero breaking changes.

**Session mode (Scribe recording active):** The Note hotkey captures a voice note that is:
1. Transcribed and formatted via the existing `NotePipeline`
2. Injected into the active session's left pane (notepad) as a timestamped entry
3. Stored in `Session.VoiceNotes` with the recording offset (e.g., "at 00:14:32")
4. Optionally also appended to the flat notes file (user setting)

#### Why Voice Notes Are a Differentiator

Granola and Fellow both require typing during meetings. dIKta.me lets users **speak** a quick thought mid-meeting — hands-free note capture. This is especially valuable when:
- User's hands are busy (presenting, coding, whiteboarding)
- A key moment happens and user wants to "bookmark" it without breaking flow
- User prefers voice input (the whole point of dIKta.me)

Voice notes act as **weighted signals** in the synthesis step — they tell the AI "the user flagged this moment as important."

#### Synthesis: Three Input Streams

The LLM synthesis prompt receives three distinct input streams, each with a different role:

```
┌─────────────────────────────────────────────────┐
│              LLM Synthesis Input                  │
│                                                   │
│  {{transcript}}     Full meeting transcript        │
│                     (complete but noisy)           │
│                                                   │
│  {{typed_notes}}    User's typed notes from        │
│                     the left pane (structured,     │
│                     user-curated)                  │
│                                                   │
│  {{voice_notes}}    Voice notes captured via        │
│                     Ctrl+Alt+N during session       │
│                     (timestamped highlights —       │
│                     treat as high-priority flags)  │
│                                                   │
│  {{template}}       Output structure template       │
│                     (meeting minutes, interview,   │
│                     etc.)                          │
│                                                   │
│              ↓                                     │
│       Structured artifact (Markdown)              │
└─────────────────────────────────────────────────┘
```

Example synthesis system prompt addition:
```
The user captured voice notes at specific moments during the meeting.
These are high-priority observations — ensure the topics they reference
are prominently covered in the output. Include the approximate timestamp
when relevant.
```

#### Post-Session: Unified Notes File

After a session completes, the user can optionally append the artifact summary to `diktame-notes.md`. This keeps the flat file as a **unified log** of both quick standalone notes and meeting summaries:

```markdown
## 2026-03-01 09:15
Quick note: remember to update the API docs before release.

## 2026-03-01 10:00 — Session: "Sprint Planning"
### Summary
Team agreed on Q3 priorities: auth system, meeting module, installer...
### Action Items
- [ ] Draft auth spec (Alice, by Mar 7)
- [ ] Research Opus compression (Bob, by Mar 5)

## 2026-03-01 14:30
Quick note: the Opus NuGet package is Concentus.OggFile.
```

#### Implementation Changes to Existing Code

| Component | Change | Effort |
|-----------|--------|--------|
| `NotePipeline` | Add session-awareness: check `SessionManager.ActiveSession`. If active, route output to session notepad instead of (or in addition to) flat file | Low |
| `NoteWriter` | No changes — still a simple append utility. Sessions call it for flat-file export | None |
| `NoteOptions` | Add `ActiveSessionId: Guid?` field. Pipeline checks this to decide routing | Low |
| `LoadingViewModel` | Update `RunNotePipelineAsync()` to pass active session ID to pipeline | Low |
| `Session` model | Add `VoiceNotes: List<TimestampedNote>` field | Low |
| `SessionManager` | Add `ActiveSession` property, `AddVoiceNote()` method | Low |
| Synthesis prompt | Include `{{voice_notes}}` section with timestamps and priority weighting | Low |
| Settings | Add toggle: "Also append voice notes to notes file during sessions" (default: on) | Low |

### 4.1 The Session Model

```
Session {
    Id: Guid
    Title: string                    // Auto-generated or user-provided
    StartedAt: DateTimeOffset
    EndedAt: DateTimeOffset?
    Duration: TimeSpan
    State: SessionState              // Recording | Processing | Complete | Failed
    AudioPath: string                // Relative path to .wav/.opus file
    TranscriptPath: string?          // Raw transcript JSON (timestamped segments)
    UserNotesMarkdown: string        // User's typed notes (persisted live)
    VoiceNotes: List<TimestampedNote> // Voice notes captured via Ctrl+Alt+N during session
    ArtifactMarkdown: string?        // AI-synthesized output
    TemplateName: string             // "meeting_minutes" | "interview" | "lecture" | etc.
    Participants: List<string>?      // Optional, extracted from transcript or user input
    WordCount: int                   // Transcript word count (for trial usage tracking)
    ModelUsed: string?               // Which LLM performed synthesis
}

TimestampedNote {
    CapturedAt: DateTimeOffset       // Wall-clock time
    SessionOffset: TimeSpan          // Offset from session start (e.g., 00:14:32)
    Text: string                     // Formatted voice note text
    RawTranscript: string            // Original STT output (before LLM formatting)
}
```

### 4.2 Core Workflow

```
[1] User clicks "Start Session" (or hotkey)
    → Audio recording begins (system audio + mic)
    → Notepad opens for user to type during meeting

[2] During meeting:
    → Audio streams to disk (not RAM)
    → Optional: live transcription sidebar (Deepgram streaming)
    → User types rough notes in left pane
    → Ctrl+Alt+N captures voice notes → injected into notepad with timestamp

[3] User clicks "End Session"
    → Recording stops
    → Background: full audio → STT transcription
    → Background: LLM synthesis (transcript + typed notes + voice notes + template → artifact)
    → Toast notification: "Meeting processed — click to view"

[4] Post-meeting:
    → View/edit synthesized artifact
    → "Ask this meeting" chat (RAG over transcript)
    → Copy/export artifact (Markdown, clipboard)
    → Future: share via integration
```

### 4.3 The Scribe Window (New WinUI 3 Page)

**Layout:**
```
┌──────────────────────────────────────────────────────┐
│  Session: "Planning Meeting v2"    [00:32:15]   [■]  │  ← Title bar + timer + stop
├────────────────────────┬─────────────────────────────┤
│                        │                             │
│    User Notes          │    AI Output / Chat         │
│    (Markdown editor)   │    (Read-only + query)      │
│                        │                             │
│    - Budget is $50k    │    ## Summary               │
│    - Launch by Q3      │    Team agreed on $50k...   │
│    - Need 2 more devs  │                             │
│                        │    ## Action Items           │
│                        │    - [ ] Hire 2 devs (Bob)  │
│                        │    - [ ] Draft budget (Ann) │
│                        │                             │
├────────────────────────┴─────────────────────────────┤
│  🎙 Recording... │ Template: Meeting Minutes │ ▼     │  ← Status bar
└──────────────────────────────────────────────────────┘
```

**Left Pane — User Notes (typed + voice):**
- Distraction-free text input (not a full Markdown editor — keep it simple)
- Auto-saves every few seconds
- Persists if app crashes during recording
- Voice notes (via `Ctrl+Alt+N`) appear inline with a microphone icon and timestamp badge (e.g., `[🎙 00:14:32] "Budget was confirmed at $50k"`)
- User can edit/delete voice note entries just like typed text

**Right Pane — AI Output (post-meeting):**
- During recording: empty or shows live transcript (optional)
- After processing: rendered Markdown artifact
- "Ask this meeting" chat input at bottom
- Copy / Export buttons

**Status Bar:**
- Recording indicator + duration timer
- Template selector dropdown
- Audio level meter (visual confirmation recording is active)

### 4.4 Output Templates

| Template | Sections Generated |
|----------|--------------------|
| **Meeting Minutes** | Summary, Decisions Made, Action Items (with owners), Next Steps, Open Questions |
| **Interview Notes** | Candidate Overview, Key Insights, Notable Quotes, Strengths/Concerns, Recommendation |
| **Lecture/Presentation** | Outline, Key Concepts, Important Details, Questions Raised, Review Notes |
| **Brainstorm** | Ideas Generated, Themes, Top Picks, Discarded Ideas, Next Actions |
| **Sales Call** | Customer Needs, Budget/Timeline, Objections Raised, Follow-up Actions, Deal Status |
| **Custom** | User-defined sections via prompt template (reuses `PromptRepository` infrastructure) |

### 4.5 "Ask This Meeting" Chat

Post-meeting RAG-like feature. Implementation options:

**Option A — Simple (MVP):** Pass full transcript + user question to LLM with system prompt: "Answer based only on this meeting transcript." Works for meetings under context window limit (~128k tokens = ~3-4 hours of audio).

**Option B — RAG (Scale):** Chunk transcript, embed into local vector store (e.g., LanceDB), retrieve relevant chunks for each question. Needed only for very long sessions or cross-meeting search.

**Recommendation:** Start with Option A. Most meetings are under 2 hours — well within modern context windows.

### 4.6 Audio Capture

**System audio** (what participants hear) + **microphone** (what user says):
- NAudio `WasapiLoopbackCapture` for system audio
- NAudio `WasapiCapture` for microphone
- Mix both streams into single recording file
- Stream to disk (not RAM buffer) for 1hr+ meetings
- Post-recording: optionally compress WAV → Opus/MP3 to save disk space

**No meeting bot.** No calendar integration for MVP. User manually starts/stops.

### 4.7 Transcription Strategy

| Mode | Tool | Use Case |
|------|------|----------|
| **Cloud (default)** | Deepgram (existing) | Fast, accurate, supports 30+ languages, streaming capable |
| **Local (optional)** | Whisper.net or faster-whisper sidecar | Privacy-first users. Slower but no data leaves machine |
| **Hybrid** | Cloud STT + Local LLM synthesis | Transcript via cloud, synthesis via Ollama |

**Speaker diarization:** Defer to Phase 2. Start without speaker labels — label all text as continuous transcript. Deepgram offers diarization in cloud mode which can be enabled later.

---

## 5. Implementation Phases

### Phase 1: Core Session Engine (MVP)
**Effort:** ~4-5 days

1. `Session` + `TimestampedNote` data models + `SessionManager` (CRUD, SQLite storage, `ActiveSession` property)
2. Long-form audio recording (disk-streaming via NAudio)
3. Post-recording transcription (batch — send full audio to Deepgram/Whisper)
4. LLM synthesis: `(transcript + typed_notes + voice_notes + template_prompt) → artifact`
5. Basic Scribe window: notepad (left) + rendered artifact (right) + record controls
6. Session list view (history of past meetings)
7. **Notes integration:** Session-aware `NotePipeline` — `Ctrl+Alt+N` routes voice notes to active session notepad with timestamp offset. Other hotkeys disabled during active session.
8. Post-session export: optionally append artifact summary to `diktame-notes.md` via `NoteWriter`

### Phase 2: Live Experience
**Effort:** ~2-3 days

1. Live transcription sidebar during recording (Deepgram streaming WebSocket)
2. Audio level meter (visual recording confirmation)
3. "Ask this meeting" chat (full-context, Option A)
4. Template selector with 5 built-in templates
5. Copy/export artifact (Markdown, clipboard, file)

### Phase 3: Polish & Integration
**Effort:** ~2-3 days

1. Session search (full-text across all past meetings)
2. Audio playback linked to transcript timestamps (click to seek)
3. Speaker diarization (Deepgram cloud)
4. Auto-title generation from transcript
5. Hotkey to start/stop session (global, like existing dictation hotkeys)
6. `AudioDucker` integration (auto-duck when session recording starts)
7. Notification: "Meeting processed" toast with quick-view

### Phase 4: Advanced (Future)
**Effort:** TBD

1. Cross-meeting search ("What did we decide about pricing across all meetings?")
2. RAG over meeting corpus (LanceDB or similar)
3. MCP server (expose meeting notes to Claude/Cursor/ChatGPT)
4. Calendar integration (auto-detect meeting start/end)
5. Export integrations (Notion, Slack, email)
6. Participant extraction from transcript
7. Follow-up email generation
8. Local Whisper transcription option

---

## 6. Technical Considerations

### Disk Space
- 1 hour WAV (stereo, 48kHz, 16-bit) = ~660MB
- Compressed to Opus = ~30MB
- Transcript JSON = ~500KB
- **Recommendation:** Record to WAV, compress to Opus post-session, delete WAV. Configurable retention policy (default: 90 days, matching `HistoryManager`).

### Context Window Limits
- 1 hour meeting = ~8,000-12,000 words transcript
- At ~1.3 tokens/word = ~10,000-16,000 tokens
- Well within Gemini (1M), Anthropic (200k), OpenAI (128k), even Ollama models (32-128k)
- Only a concern for 4+ hour recordings — handle with chunked summarization

### Concurrent Recording + Dictation
- Session recording uses system audio loopback + mic
- Dictation and Note modes use mic only
- **Potential conflict:** If user triggers dictation/note during a session, mic input could be captured by both
- **Solution for Note hotkey:** This is the *intended* use case — voice notes during sessions. Briefly pause session mic capture during the note burst to avoid echo, then resume. The note audio is processed by `NotePipeline` independently.
- **Solution for other hotkeys (Dictate, Ask, Refine, Translate):** Disable during active session. These modes inject text into external apps, which conflicts with the meeting context. Show a toast: "Pause or end session to use dictation."

### Storage Location
- Sessions stored under `%APPDATA%/DiktaMe/sessions/{session_id}/`
- Audio: `recording.opus`
- Transcript: `transcript.json`
- Metadata + notes + artifact: SQLite (extend existing DB)

---

## 7. Pricing Comparison & Positioning

| | Granola | Fellow | dIKta.me (proposed) |
|---|---------|--------|---------------------|
| **Free tier** | Limited history | 10 notes | Unlimited (BYOK) |
| **Paid** | $14/mo | $7-25/mo | $0 (bring your own API key) |
| **Local option** | No | No | Yes (Ollama) |
| **Privacy** | Cloud-only | Cloud-only (SOC2) | Local-first, PII scrubber |
| **Meeting bots** | No | Optional | No |
| **Beyond meetings** | No | No | Yes (dictation, translate, ask, chat) |

**Value proposition:** "The meeting intelligence tool that doesn't charge a subscription, doesn't send your data to the cloud (unless you want it to), and also does everything else."

---

## 8. Open Questions

1. **Calendar integration for MVP?** — Probably no. Manual start/stop is simpler and avoids permission complexity. Revisit in Phase 4.
2. **Separate window or tab in main app?** — Recommend: separate window (like Quick Chat), launchable from tray menu + hotkey.
3. **Audio format for long recordings?** — WAV during recording (simplest), auto-compress to Opus after session ends.
4. **How to handle "Ask this meeting" for local models with small context?** — Chunked summarization for Ollama models with <32k context. Or require cloud provider for chat feature.
5. **Should sessions be shareable?** — Defer. Export to Markdown file is sufficient for MVP.
6. **MCP server priority?** — Both Granola and Fellow have MCP. Consider as Phase 4 item — growing ecosystem play.

---

## 9. Success Criteria

- [ ] User can record a 1-hour meeting with system audio + mic
- [ ] User can type notes during the recording
- [ ] User can capture voice notes via `Ctrl+Alt+N` during a session — they appear in the notepad with timestamp
- [ ] Voice notes are weighted as high-priority signals in synthesis output
- [ ] `Ctrl+Alt+N` without an active session works exactly as before (standalone mode)
- [ ] AI synthesizes transcript + typed notes + voice notes into a structured artifact within 60 seconds of session end
- [ ] User can view, copy, and export the artifact
- [ ] User can "ask" the meeting transcript questions and get accurate answers
- [ ] Works fully offline with Ollama (local STT + local LLM)
- [ ] Session data persists across app restarts
- [ ] Audio files auto-compress and respect retention policy

---

## 10. References

- V1 Spec: `E:\git\diktate\docs\internal\specs\deferred\SPEC_003_SCRIBE_LAYER.md`
- Granola: https://www.granola.ai — Features, pricing ($0/$14/$35), MCP support
- Fellow.ai: https://fellow.ai — Features, pricing ($0/$7/$15/$25), 50+ integrations, SOC2/HIPAA
- HyprNote: https://github.com/fastrepl/hyprnote — Open source, local-first, macOS
- Granola MCP: https://www.granola.ai/blog/granola-mcp
- Fritz.ai Fellow review: https://fritz.ai/fellow-ai-review/
