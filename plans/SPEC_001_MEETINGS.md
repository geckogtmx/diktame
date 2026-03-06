# SPEC_001: Meeting Intelligence Module ("Scribe")

> **Status:** DRAFT
> **Date:** 2026-03-01
> **Supersedes:** V1 `SPEC_003_SCRIBE_LAYER.md` (conceptual, never implemented)
> **Competitive References:** [Granola](https://www.granola.ai), [Fellow.ai](https://fellow.ai), [HyprNote](https://github.com/fastrepl/hyprnote)
> **Priority:** TBD (post-V2 launch)

---

## 1. Executive Summary

dIKta.me currently captures ephemeral voice input — you speak, it types, it forgets. The Meeting Intelligence module adds a **persistent session layer**: record a meeting, type rough notes during it, and let AI synthesize a polished artifact (minutes, action items, follow-up emails).

**The core value is in the intersection of notes and transcript.** The user's typed notes act as **intent signals** — they tell the AI which topics matter to this specific user. The AI then locates every moment in the full transcript where those topics were discussed, identifies who said what, what was agreed, what was contested, and synthesizes a structured output organized around what the user actually cared about. Neither the raw transcript nor the sparse notes are nearly as valuable in isolation — together, they produce a meeting artifact that reflects the user's priorities.

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
| User notes during meeting (typed) | Yes | Yes | Yes | Planned | **HIGH** — core differentiator |
| AI synthesis (transcript + notes) | Yes | Yes | Yes | Planned | **HIGH** — the "magic" |
| Post-meeting chat with transcript | Yes | Yes | No | No | **HIGH** — high user value |
| Customizable output templates | Yes | Yes | No | No | **MEDIUM** — nice-to-have at launch |
| Pre-meeting briefs | No | Yes | No | No | **LOW** — Fellow-unique, enterprise |
| Action item extraction | Yes | Yes | No | No | **HIGH** — universal need |
| Follow-up email generation | Yes | Yes | No | No | **MEDIUM** — quick win |
| CRM sync (Salesforce, HubSpot) | Yes | Yes | No | No | **LOW** — enterprise, defer |
| Task tool sync (Asana, Jira, etc.) | No | Yes | No | No | **LOW** — enterprise, defer |
| MCP server (expose notes to AI tools) | Yes | Yes | No | Planned (local) | **MEDIUM** — local-first differentiator |
| Multi-language transcription | Yes | Yes | No | Partial (STT) | **MEDIUM** — already have STT infra |
| Speaker diarization | ? | Yes | No | Planned (Deepgram) | **MEDIUM** — Deepgram batch diarization |
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
| Cloud STT (Deepgram) | Meeting transcription (batch mode with diarization) |
| `LLMRouter` + all providers | Synthesis (Gemini, Anthropic, OpenAI, Ollama) |
| `HistoryManager` (SQLite) | Session storage model |
| `PromptRepository` | Template prompts for synthesis |
| `SecureStorage` + API keys | Provider auth for cloud processing |
| WinUI 3 app shell | Scribe window (new page in existing app) |
| `NotificationService` | "Meeting processed" toast |
| `AudioDucker` | Auto-duck other apps during recording |

### What Makes Us Different

1. **Local-first option** — Process meetings with Ollama. No data leaves your machine.
2. **No subscription** — BYOK model. Use your own Gemini/Anthropic/OpenAI key. No $14/mo.
3. **No meeting bot** — System audio capture only. No "Fellow AI has joined" awkwardness.
4. **Single app** — Dictation + Meeting Intelligence in one tool. Not two separate products.
5. **Privacy-first** — PII scrubber, local processing, no telemetry. Already built (E.3).

---

## 4. Feature Specification

### 4.0 Hotkey Behavior During Active Sessions

The existing Notes feature (`Ctrl+Alt+N`) and all other voice hotkeys remain **completely independent** from Scribe sessions. No changes are made to `NotePipeline`, `NoteWriter`, or `NoteOptions`.

During an active Scribe session, the microphone is occupied by meeting recording (system audio + mic). Voice-based hotkeys are **silently disabled** — no toast, no error, the keypress is simply ignored:

| Hotkey | During Session | Reason |
|--------|---------------|--------|
| Dictate | Disabled | Uses mic |
| Ask | Disabled | Uses mic |
| Translate | Disabled | Uses mic |
| Note | Disabled | Uses mic |
| Refine (Voice mode) | Disabled | Uses mic |
| Refine (Auto mode) | **Available** | No mic — reads selection, LLM cleanup, injects |
| Chat | **Available (text-only)** | Quick Chat overlay opens, but mic button is disabled |

When the session ends, all hotkeys are re-enabled automatically.

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
    UserNotesMarkdown: string        // User's typed notes (persisted live, auto-saved)
    ArtifactMarkdown: string?        // AI-synthesized output
    TemplateName: string             // "meeting_minutes" | "interview" | "lecture" | etc.
    Participants: List<string>?      // Optional, extracted from transcript or user input
    WordCount: int                   // Transcript word count (for trial usage tracking)
    ModelUsed: string?               // Which LLM performed synthesis
}
```

### 4.2 Core Workflow

```
[1] User clicks "Start Session" (or hotkey)
    → Audio recording begins (system audio + mic)
    → Notepad opens for user to type during meeting
    → Voice hotkeys silently disabled (mic occupied)

[2] During meeting:
    → Audio streams to disk (not RAM)
    → User types rough notes in left pane (basic markdown)

[3] User clicks "End Session"
    → Recording stops
    → Voice hotkeys re-enabled
    → Background: full audio → STT transcription
    → Background: LLM synthesis (transcript + typed notes + template → artifact)
    → Toast notification: "Meeting processed — click to view"

[4] Post-meeting:
    → View/edit synthesized artifact
    → "Ask this meeting" chat (RAG over transcript)
    → Copy/export artifact (Markdown, clipboard)
    → Future: share via integration
```

### 4.3 Synthesis Model: Notes as Intent Signals

The core value of the Scribe module is not in the transcript or the notes separately — it's in the **intersection**. The user's typed notes act as intent signals that tell the AI which topics matter to this specific user.

**Two input streams + template:**

```
┌─────────────────────────────────────────────────┐
│              LLM Synthesis Input                  │
│                                                   │
│  {{transcript}}     Full meeting transcript        │
│                     (complete, but noisy and       │
│                     unfocused)                     │
│                                                   │
│  {{typed_notes}}    User's typed notes from        │
│                     the left pane — these are      │
│                     intent signals that tell the   │
│                     AI which topics matter          │
│                                                   │
│  {{template}}       Output structure template       │
│                     (meeting minutes, interview,   │
│                     etc.)                          │
│                                                   │
│              ↓                                     │
│       Structured artifact (Markdown)              │
│       organized around the user's notes            │
└─────────────────────────────────────────────────┘
```

**How synthesis works:**

For each topic the user noted, the AI locates every relevant moment in the full transcript — who said what, what was agreed, what was contested, what remains unresolved — and synthesizes it into the template structure. The output is organized around **what the user cared about**, not a generic chronological summary.

**Example:** User types "Budget is $50k" during the meeting. The AI finds all transcript segments where budget was discussed, identifies that Alice proposed $50k, Bob pushed back citing Q2 overruns, and the team settled on $45k with a $5k contingency. The artifact's "Decisions Made" section reflects this nuance — not just "budget was discussed."

**Individual artifacts remain available:** Users can still export the raw transcript or their notes separately. But the primary output — the synthesized artifact — draws its value from combining both.

**Example synthesis system prompt:**
```
You are a meeting intelligence assistant. You receive a full meeting
transcript and the user's typed notes taken during the meeting.

The user's notes indicate which topics they consider important. For each
noted topic, find all relevant discussion in the transcript and synthesize:
- What was said and by whom
- What was agreed or decided
- What was contested or left unresolved
- Any action items that emerged

Structure the output according to the provided template. Prioritize depth
on noted topics over generic coverage of the full transcript.
```

### 4.4 The Scribe Window (New WinUI 3 Page)

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

**Left Pane — User Notes:**
- Basic markdown editor (bold, lists, headings via keyboard shortcuts)
- Distraction-free — no toolbar, no formatting buttons. Just type.
- Auto-saves every few seconds
- Persists if app crashes during recording

**Right Pane — AI Output (post-meeting):**
- During recording: empty (or placeholder/instructions)
- After processing: rendered Markdown artifact
- "Ask this meeting" chat input at bottom
- Copy / Export buttons

**Status Bar:**
- Recording indicator + duration timer
- Template selector dropdown
- Audio level meter (visual confirmation recording is active)

### 4.5 Output Templates

| Template | Sections Generated |
|----------|--------------------|
| **Meeting Minutes** | Summary, Decisions Made, Action Items (with owners), Next Steps, Open Questions |
| **Interview Notes** | Candidate Overview, Key Insights, Notable Quotes, Strengths/Concerns, Recommendation |
| **Lecture/Presentation** | Outline, Key Concepts, Important Details, Questions Raised, Review Notes |
| **Brainstorm** | Ideas Generated, Themes, Top Picks, Discarded Ideas, Next Actions |
| **Sales Call** | Customer Needs, Budget/Timeline, Objections Raised, Follow-up Actions, Deal Status |
| **Custom** | User-defined sections via prompt template (reuses `PromptRepository` infrastructure) |

### 4.6 "Ask This Meeting" Chat

Post-meeting RAG-like feature. Implementation options:

**Option A — Simple (MVP):** Pass full transcript + user question to LLM with system prompt: "Answer based only on this meeting transcript." Works for meetings under context window limit (~128k tokens = ~3-4 hours of audio).

**Option B — RAG (Scale):** Chunk transcript, embed into local vector store (e.g., LanceDB), retrieve relevant chunks for each question. Needed only for very long sessions or cross-meeting search.

**Recommendation:** Start with Option A. Most meetings are under 2 hours — well within modern context windows.

### 4.7 Audio Capture

**System audio** (what participants hear) + **microphone** (what user says):
- NAudio `WasapiLoopbackCapture` for system audio
- NAudio `WasapiCapture` for microphone
- Mix both streams into single recording file
- Stream to disk (not RAM buffer) for 1hr+ meetings
- Post-recording: optionally compress WAV → Opus/MP3 to save disk space

**No meeting bot.** No calendar integration for MVP. User manually starts/stops.

### 4.8 Transcription Strategy

All transcription is **batch/post-meeting** — the full audio file is submitted after the session ends. No streaming transcription during the meeting (the user is already in the meeting hearing everything live).

| Mode | Tool | Use Case |
|------|------|----------|
| **Cloud (default)** | Deepgram (existing) | Fast, accurate, supports 30+ languages |
| **Local (optional)** | Whisper.net or faster-whisper sidecar | Privacy-first users. Slower but no data leaves machine |
| **Hybrid** | Cloud STT + Local LLM synthesis | Transcript via cloud, synthesis via Ollama |

**Recommended Deepgram parameters:** `diarize=true&utterances=true&smart_format=true`

**Speaker diarization** (`diarize=true`): Assigns numeric speaker labels (`Speaker 0`, `Speaker 1`, ...) to every word in the transcript with confidence scores. Deepgram's June 2024 diarization model showed 61.5% improved accuracy specifically on meeting audio. Accuracy is professional-grade for 2-3 speakers, good for 4-6, and degrades for 7+ (all vendors struggle at that level). No cap on number of speakers. Cost: ~$0.002/min extra.

**Utterances** (`utterances=true`): Segments the transcript into speaker-attributed paragraph blocks — each utterance has start/end timestamps, speaker label, and full text. This is the most natural format for a readable meeting transcript.

**Smart formatting** (`smart_format=true`): Automatic punctuation, capitalization, and entity formatting (dates, currency, phone numbers). Included at no extra cost.

**Speaker naming** (Phase 3): Diarization only provides numeric labels, not names. Speaker identification requires a separate step:
- **LLM inference**: Feed the diarized transcript to the LLM and ask it to infer names from conversational context ("Hey Alice", "Thanks Bob"). Works well when participants address each other by name.
- **Manual labeling**: Post-meeting UI where the user assigns names to Speaker 0, Speaker 1, etc.
- **Calendar attendees**: If calendar integration is available, pre-fill candidate names from the meeting invite.

---

## 5. Implementation Phases

### Phase 1: Core Session Engine (MVP)
**Effort:** ~4-5 days

1. `Session` data model + `SessionManager` (CRUD, SQLite storage, `ActiveSession` property)
2. Long-form audio recording (disk-streaming via NAudio)
3. Post-recording transcription (batch — send full audio to Deepgram/Whisper)
4. LLM synthesis: `(transcript + typed_notes + template_prompt) → artifact` — notes as intent signals (see 4.3)
5. Basic Scribe window: notepad with basic markdown (left) + rendered artifact (right) + record controls
6. Session list view (history of past meetings)
7. Disable voice hotkeys during active session; Refine Auto and Chat (text-only) remain available

### Phase 2: Post-Meeting Experience
**Effort:** ~2-3 days

1. Audio level meter (visual recording confirmation)
2. "Ask this meeting" chat (full-context, Option A)
3. Template selector with 5 built-in templates
4. Copy/export artifact (Markdown, clipboard, file)

### Phase 3: Polish & Integration
**Effort:** ~2-3 days

1. Session search (full-text across all past meetings)
2. Audio playback linked to transcript timestamps (click to seek)
3. Speaker naming UI (assign names to Speaker 0/1/2 — manual + LLM inference from conversation context)
4. Auto-title generation from transcript
5. Hotkey to start/stop session (global, like existing dictation hotkeys)
6. `AudioDucker` integration (auto-duck when session recording starts)
7. Notification: "Meeting processed" toast with quick-view
8. Calendar sync (Google Calendar / Outlook OAuth — auto-detect meetings, pre-fill title + participants)

### Phase 4: Advanced (Future)
**Effort:** TBD

1. Cross-meeting search ("What did we decide about pricing across all meetings?")
2. RAG over meeting corpus (LanceDB or similar)
3. MCP server — local, app-wide (expose sessions, dictation history, notes to Claude/Cursor/ChatGPT)
4. Local REST API / export hooks (localhost API + file-based triggers for automation)
5. Notion export (push artifact as Markdown → Notion page)
6. Confluence export (push artifact as Markdown → Confluence page)
7. Follow-up email generation
8. Local Whisper transcription option
9. CRM sync (Salesforce/HubSpot — enterprise, significant effort)
10. Task tool sync (Jira/Linear/Asana — 2-way action items, enterprise)

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

### Hotkey Behavior During Sessions
- Session recording occupies the microphone (system audio loopback + mic)
- All voice hotkeys (Dictate, Ask, Translate, Note, Refine Voice) are silently disabled during an active session
- Refine Auto remains available (no mic — reads selection, processes via LLM, injects)
- Chat remains available with text input only (mic button disabled in Quick Chat overlay)
- Hotkeys are re-enabled automatically when the session ends

### Storage Location
- Sessions stored under `%APPDATA%/DiktaMe/sessions/{session_id}/`
- Audio: `recording.opus`
- Transcript: `transcript.json`
- Metadata + notes + artifact: SQLite (extend existing DB)

---

## 7. Integrations & Ecosystem

### 7.1 What Competitors Offer

**Granola** integrations: Slack (auto-post per folder), Notion (manual push), HubSpot/Attio/Affinity (CRM note push), Zapier (2 triggers, 8000+ downstream apps), MCP server (official cloud + community local), Enterprise API (read-only). All require paid plan ($14/mo+).

**Fellow** integrations: 50+ tools. Calendar sync (Google/Outlook), CRM (Salesforce with AI field suggestions, HubSpot, Pipedrive), task management (Jira/Linear/ClickUp/Asana — all 2-way sync), Slack/Teams, Notion/Confluence, Zapier (5 triggers + 3 actions), REST API + webhooks, MCP server (5 read-only tools), HRIS (BambooHR/Workday), Glean (enterprise search). Most require paid plan.

**Key insight:** Both competitors' integrations are cloud-hosted. dIKta.me's local-first architecture means we can offer MCP and API access without cloud dependencies — a genuine differentiator.

### 7.2 dIKta.me Integration Roadmap

| Integration | What It Does | Phase | Notes |
|---|---|---|---|
| **Clipboard export** | Copy artifact as Markdown | 1 | Must-have. Zero integration effort. |
| **Diarization** | Speaker-labeled transcript via Deepgram `diarize=true` | 1 | See 4.8. Numeric labels; naming deferred. |
| **File export** | Save artifact as `.md` file | 2 | One-click save to user-chosen location. |
| **Speaker naming** | Assign names to Speaker 0/1/2 via LLM inference or manual UI | 3 | LLM can infer from "Hey Alice" patterns in transcript. |
| **Calendar sync** | Google Calendar / Outlook via OAuth. Auto-detect meetings, pre-fill session title + participants from calendar event. Tray notification before meetings. | 3 | App-wide capability — could also be used for reminders. |
| **MCP server** | Local server exposing sessions, dictation history, notes to AI tools (Claude, Cursor, ChatGPT). Read-only. Tools: `search_sessions`, `get_session_transcript`, `get_session_artifact`, `get_dictation_history`, `search_notes`. | 4 | **Differentiator**: local-first MCP vs competitors' cloud-only. App-wide — not just Scribe. |
| **Local API / export hooks** | Lightweight REST API on localhost. File-based triggers (artifact → watched folder for automation). Optional webhook POST on events. | 4 | Replaces Zapier need without cloud infra. |
| **Notion export** | Push artifact as Markdown → Notion page via Notion API | 4 | One-click from artifact view. |
| **Confluence export** | Push artifact as Markdown → Confluence page via REST API | 4 | One-click from artifact view. |
| **CRM sync** | Push meeting notes to Salesforce/HubSpot contact/deal records | 4+ | Enterprise feature. Both Granola and Fellow offer this. Significant integration effort. |
| **Task tool sync** | 2-way action item sync with Jira/Linear/Asana/ClickUp | 4+ | Enterprise feature. Fellow's deepest integration category. |

### 7.3 Integration Architecture Principles

1. **Local-first**: No cloud relay for integrations. MCP server runs on localhost. API serves from the app process.
2. **Clipboard-first**: The simplest "integration" is copying Markdown to clipboard. This covers most ad-hoc needs without building anything.
3. **App-wide**: MCP server, local API, and calendar sync benefit the entire app (dictation history, notes, chat), not just Scribe.
4. **No Zapier**: Zapier requires a cloud-hosted webhook receiver and app registration. Doesn't fit our architecture. The local API + MCP server + file export cover the same use cases.

---

## 8. Pricing Comparison & Positioning

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

## 9. Open Questions

1. **Calendar integration for MVP?** — No. Manual start/stop for MVP. Calendar sync planned for Phase 3 (Google Calendar / Outlook OAuth).
2. **Separate window or tab in main app?** — Recommend: separate window (like Quick Chat), launchable from tray menu + hotkey.
3. **Audio format for long recordings?** — WAV during recording (simplest), auto-compress to Opus after session ends.
4. **How to handle "Ask this meeting" for local models with small context?** — Chunked summarization for Ollama models with <32k context. Or require cloud provider for chat feature.
5. **Should sessions be shareable?** — Defer. Export to Markdown file is sufficient for MVP.
6. **MCP server priority?** — Phase 4. Both Granola and Fellow have cloud-hosted MCP. Ours will be local-first (differentiator). App-wide scope (not just Scribe).

---

## 10. Success Criteria

- [ ] User can record a 1-hour meeting with system audio + mic
- [ ] User can type notes (basic markdown) during the recording
- [ ] AI synthesis output is organized around user's typed notes — each noted topic is enriched with relevant transcript context (who said what, decisions, disagreements)
- [ ] AI synthesizes transcript + typed notes into a structured artifact within 60 seconds of session end
- [ ] All voice hotkeys are silently disabled during active session; Refine Auto and Chat (text-only) remain available
- [ ] User can view, copy, and export the artifact
- [ ] User can "ask" the meeting transcript questions and get accurate answers
- [ ] Works fully offline with Ollama (local STT + local LLM)
- [ ] Session data persists across app restarts
- [ ] Audio files auto-compress and respect retention policy

---

## 11. References

- V1 Spec: `E:\git\diktate\docs\internal\specs\deferred\SPEC_003_SCRIBE_LAYER.md`
- Granola: https://www.granola.ai — Features, pricing ($0/$14/$35), MCP support
- Fellow.ai: https://fellow.ai — Features, pricing ($0/$7/$15/$25), 50+ integrations, SOC2/HIPAA
- HyprNote: https://github.com/fastrepl/hyprnote — Open source, local-first, macOS
- Granola MCP: https://www.granola.ai/blog/granola-mcp
- Fritz.ai Fellow review: https://fritz.ai/fellow-ai-review/
