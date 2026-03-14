# SPEC_013: Connector Use Cases & Competitive Analysis

> **Generated**: 2026-03-14
> **Scope**: Exhaustive use case catalog for dIKta.me V2 Connector Framework
> **Companion to**: [`SPEC_013_CONNECTORS_IMPLEMENTATION.md`](SPEC_013_CONNECTORS_IMPLEMENTATION.md)
> **Related Specs:**
> - [`SPEC_001_MEETINGS.md`](SPEC_001_MEETINGS.md) — §8 use cases reference Scribe workflows + meeting distribution
> - [`SPEC_002_VISION.md`](SPEC_002_VISION.md) — §6 use cases reference screenshot-based connectors
> - [`SPEC_014_MEMORY_LAYER.md`](SPEC_014_MEMORY_LAYER.md) — Memory-enriched connector workflows (context-aware output routing)
> - [`SPEC_015_MODULES_SPRINT.md`](SPEC_015_MODULES_SPRINT.md) — Implementation sprint that builds on these use cases

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Competitive Landscape](#2-competitive-landscape)
3. [Market Gap Analysis](#3-market-gap-analysis)
4. [Tier 1 — Casual: Google Ecosystem](#4-tier-1--casual-google-ecosystem)
5. [Tier 2 — Mid-Tier: Knowledge Workers](#5-tier-2--mid-tier-knowledge-workers)
6. [Tier 3 — Advanced: Power Users & Developers](#6-tier-3--advanced-power-users--developers)
7. [Cross-Connector Workflows](#7-cross-connector-workflows)
8. [Accessibility Use Cases](#8-accessibility-use-cases)
9. [Privacy-Sensitive Use Cases](#9-privacy-sensitive-use-cases)
10. [Key Patterns & Strategic Insights](#10-key-patterns--strategic-insights)

---

## 1. Executive Summary

This document catalogs **200+ real-life use cases** for dIKta.me's connector framework, organized by user tier, connector, and domain. It also includes competitive analysis of 14 tools and a gap analysis showing dIKta.me's unique market position.

**Core finding**: The dictation market has two camps with a massive unoccupied gap between them:

| Camp | Tools | Integrations | Privacy | Price |
|------|-------|-------------|---------|-------|
| **Cloud Meeting Tools** | Otter, Fireflies, Granola, Notta, Fellow, Tactiq | Rich (Slack, Notion, CRM, Zapier) | Cloud-only | $8–39/mo/user |
| **Local Desktop Tools** | Superwhisper, MacWhisper, Buzz, Dragon, Win/Apple built-in | Zero to none | Local/offline | Free – $699 one-time |

**dIKta.me bridges both camps** — local-first privacy WITH meaningful integrations. No competitor does this. Superwhisper (closest in concept) is Mac-only, leaving Windows uncontested.

---

## 2. Competitive Landscape

### 2.1 Competitor Matrix

| Tool                 | Free Integrations               | Paid Integrations                                   | Enterprise                    | Local?                            | API/Webhook?                 | Price                     |
| -------------------- | ------------------------------- | --------------------------------------------------- | ----------------------------- | --------------------------------- | ---------------------------- | ------------------------- |
| **Granola**          | Calendar sync                   | Salesforce, HubSpot, Notion, Slack                  | SSO, custom                   | Audio local, AI cloud             | No                           | $0–28/mo                  |
| **Otter.ai**         | Zoom/Meet/Teams bot             | Slack, Zapier                                       | Salesforce, HubSpot, API, SSO | No                                | API @ $30/mo                 | $0–30/mo                  |
| **Fireflies.ai**     | Zoom/Meet/Teams/Webex           | Slack, Notion, Asana, Trello, CRM, **API+Webhooks** | SSO, priority API             | No                                | **API+Webhooks @ $10–18/mo** | $0–39/mo                  |
| **Tactiq**           | Meet/Zoom/Teams (browser)       | Notion, Slack, CRM, Markdown export                 | Team sharing                  | No                                | No                           | $0–20/mo                  |
| **Krisp**            | Any app (virtual audio)         | Slack                                               | Salesforce, HubSpot           | **Noise cancellation local**      | Limited                      | $0–15/mo                  |
| **Superwhisper**     | System-wide injection, AI modes | —                                                   | —                             | **STT local, LLM via API/Ollama** | None                         | ~$10/mo or ~$200 lifetime |
| **MacWhisper**       | Export: TXT/SRT/VTT/JSON        | —                                                   | —                             | **100% local**                    | None                         | $0–50 one-time            |
| **Dragon Pro**       | Deep MS Office, macros          | —                                                   | SDK (expensive)               | **Primarily local**               | COM SDK (enterprise $$)      | ~$699 one-time            |
| **Win Voice Typing** | System-wide typing              | —                                                   | —                             | Partial (Win11)                   | Windows Speech API           | Free                      |
| **Apple Dictation**  | System-wide typing              | —                                                   | —                             | Yes (Apple Silicon)               | SFSpeechRecognizer           | Free                      |
| **Talon Voice**      | Code editors, eye tracking      | —                                                   | —                             | **100% local**                    | Python scripting             | Free (beta)               |
| **Buzz**             | Export: TXT/SRT/VTT             | —                                                   | —                             | **100% local**                    | None                         | Free                      |
| **Notta.ai**         | Zoom/Meet/Teams bot             | Notion, Slack, Zapier                               | Salesforce, HubSpot, API, SSO | No                                | API @ Enterprise only        | $0–28/mo                  |
| **Fellow**           | Calendar, Meet/Zoom/Teams       | Slack, **Jira, Linear**, Asana, Notion, Zapier      | SSO, REST API                 | No                                | API @ Enterprise only        | $0–20/mo                  |

### 2.2 Integration Pricing Patterns

| Integration | Free? | Typical Unlock | Enterprise Gate? |
|------------|-------|---------------|-----------------|
| Calendar sync | Almost always | — | — |
| Meeting platforms | Almost always | — | — |
| Slack | Sometimes | $8–18/mo | — |
| Notion | Rarely | $8–18/mo | — |
| Zapier | Never | $10–18/mo | — |
| CRM (Salesforce/HubSpot) | Never | $18–30/mo | Sometimes |
| REST API | Never | $10–30/mo | Often |
| Webhooks | Never | $10–18/mo | Sometimes |
| SSO/SAML | Never | — | Always ($30+/mo) |

**Key insight**: The most-demanded integrations (Obsidian, daily note, Markdown output, local webhook) are **file-system-based and cost nothing to provide**. dIKta.me can include them at no marginal cost — a structural advantage over cloud tools.

---

## 3. Market Gap Analysis

### 3.1 Gaps No Competitor Fills

| Gap | Demand | Who Wants It | Why It Doesn't Exist |
|-----|--------|-------------|---------------------|
| **Native Obsidian vault integration** | Very High | 2M+ Obsidian users, PKM community | Requires local file system access — cloud tools can't do it |
| **Local-first + integrations** | High | Privacy users, enterprise, govt, healthcare | Local tools have no integrations; integrated tools are cloud-only |
| **Dictation-to-Markdown** with formatting | High | Writers, devs, PKM users | Requires LLM post-processing with Markdown-aware prompts |
| **Ad-hoc voice-to-CRM** (not meeting-tied) | Medium-High | Sales reps, field workers | All CRM integrations are tied to meeting transcription |
| **Webhook/API from desktop app** | Medium-High | n8n/Make users, developers | Desktop apps haven't built webhook servers |
| **Context-aware routing** by focused app | Medium | Power users | Requires OS-level window detection — only desktop apps can do this |
| **Voice-to-daily-note** auto-append | Medium | PKM community, journalers | Simple to implement locally, but no tool has prioritized it |

### 3.2 dIKta.me's Unique Advantages

1. **Direct file system access** → Obsidian vault writes, Logseq, any Markdown tool
2. **Zero marginal cost for local integrations** → can undercut $18–30/mo cloud pricing
3. **System-level context awareness** → detect focused app via Win32 `GetForegroundWindow`
4. **Local webhook server** → `localhost` endpoints for n8n, scripts, automation
5. **Privacy-preserving pipeline** → local STT + local LLM + local file write = air-gapped
6. **Windows-exclusive positioning** → Superwhisper is Mac-only, Dragon is dying

---

## 4. Tier 1 — Casual: Google Ecosystem

> **Connectors**: Google Calendar (Pull: read events; Push: create events), Gmail (Pull: read threads; Push: create drafts)
> **Target users**: Managers, professionals, students, parents, accessibility users, anyone with a Google account
> **Key insight**: Calendar + Gmail combined use cases are disproportionately valuable because they mirror how humans actually work — meetings and emails are deeply intertwined but managed in separate tools. The LLM unifies them.

### 4.1 Google Calendar — Pull (Read Events)

| # | Use Case | Trigger | Flow | Value | Primary User |
|---|----------|---------|------|-------|-------------|
| C-01 | **Morning Briefing** — "What's on my calendar today?" | Chat | Calendar Pull → LLM formats briefing → TTS reads aloud | Zero-click morning routine, eyes-free | Manager, any professional |
| C-02 | **Pre-Meeting Context** — "Who's in my next meeting?" | Chat / Dictate | Calendar Pull → LLM extracts attendees, title, links | Instant context-switch prep, no alt-tabbing | Consultant, salesperson |
| C-03 | **Availability Check Mid-Call** — "Am I free Thursday at 2?" | Dictate | Calendar Pull → LLM checks conflicts → text response | Real-time scheduling without putting client on hold | Freelancer, anyone scheduling |
| C-04 | **Weekly Planning Review** | Chat | Calendar Pull (7 days) → LLM groups by day, flags busy blocks | Structured weekly overview without scanning 5 views | Solopreneur |
| C-05 | **Meeting Load Analysis** — "Hours of meetings this week vs last?" | Chat | Calendar Pull (2 weeks) → LLM calculates, compares | Quantified meeting burden, justifies no-meeting days | Manager |
| C-06 | **Vision-Impaired Calendar Access** | Chat | Calendar Pull → LLM → TTS | Calendar becomes conversational, not a visual grid | Accessibility |
| C-07 | **Commute/Travel Time Check** — "Where's my next meeting?" | Chat | Calendar Pull → LLM extracts time + location | Quick location + time math without opening Calendar | Field salesperson |
| C-08 | **Student Class Schedule** — "What class do I have next?" | Dictate | Calendar Pull → LLM finds next class event | Instant recall without pulling out phone | Student |
| C-09 | **Caregiver Multi-Calendar Check** | Chat | Calendar Pull (personal + family) → LLM cross-references | Multi-calendar conflict detection via voice | Parent, caregiver |
| C-10 | **Flow State Time Awareness** — "How long before my next meeting?" | Chat | Calendar Pull → LLM computes delta | Preserves developer focus, no clock-watching | Developer, creative |

### 4.2 Google Calendar — Push (Create Events)

| # | Use Case | Trigger | Flow | Value | Primary User |
|---|----------|---------|------|-------|-------------|
| C-11 | **Voice-Create Meeting** — "Schedule design review Tuesday 10 AM" | Dictate | LLM parses natural language → Calendar Push | Event creation without date pickers, one step | Manager |
| C-12 | **Time-Block Deep Work** — "Block 2–4 PM tomorrow for writing" | Dictate | LLM → Calendar Push (no attendees) | Time-blocking via voice, no context-switch | Productivity-focused |
| C-13 | **Post-Call Follow-Up** — "Schedule follow-up with Acme Friday 3 PM" | Dictate | LLM → Calendar Push with title + description | Captures intent immediately, no forgetting | Salesperson |
| C-14 | **Multilingual Event Creation** — dictate in Spanish/French/etc. | Dictate | STT in native language → LLM → Calendar Push | No mental translation overhead | Multilingual professional |
| C-15 | **ADHD Instant Anchoring** — "Reminder tomorrow 9 AM call insurance" | Dictate | LLM → Calendar Push | Externalizes memory in under 5 seconds | Neurodivergent |
| C-16 | **Hands-Free While Walking** | Dictate | LLM → Calendar Push → TTS confirms | Captures intent before forgotten, no screen needed | Commuter |
| C-17 | **Selected Text → Calendar Event** — select "let's meet Wednesday 3 PM" | Select + hotkey | LLM extracts event details → Calendar Push | Text-to-calendar from any app, zero data entry | Any professional |

### 4.3 Gmail — Pull (Read Emails)

| # | Use Case | Trigger | Flow | Value | Primary User |
|---|----------|---------|------|-------|-------------|
| C-18 | **Morning Email Briefing** — "Summarize unread emails" | Chat | Gmail Pull → LLM summarizes + prioritizes → TTS | Email triage without opening Gmail | Manager (50+ daily emails) |
| C-19 | **Natural Language Email Search** — "Find that contract amendment email" | Chat | Gmail Pull (search) → LLM identifies best match | More intuitive than Gmail search operators | Lawyer |
| C-20 | **Pre-Meeting Email Context** — "Recent emails with John at Acme?" | Chat | Gmail Pull (filter by contact) → LLM summarizes | Instant relationship context before meetings | Consultant |
| C-21 | **Vision-Impaired Email Access** | Chat | Gmail Pull → LLM reads/summarizes → TTS | Email as conversation, bypasses complex Gmail UI | Accessibility |
| C-22 | **PTO Urgency Check** — "Anything urgent in last 24 hours?" | Chat | Gmail Pull → LLM filters for urgency signals | Controlled PTO check without inbox rabbit hole | Anyone on leave |
| C-23 | **Student Professor Email Check** | Chat | Gmail Pull (filter) → LLM summarizes | Targeted check instead of scrolling spam | Student |
| C-24 | **Freelancer Invoice Tracking** — "Did DataCo reply about my invoice?" | Chat | Gmail Pull (search) → LLM checks for replies | Quick accounts-receivable check | Freelancer |

### 4.4 Gmail — Push (Create Drafts)

| # | Use Case | Trigger | Flow | Value | Primary User |
|---|----------|---------|------|-------|-------------|
| C-25 | **Dictate Full Email Draft** | Dictate | LLM parses to/subject/body → Gmail Push (draft) | Full email composition via voice, no typing | Salesperson |
| C-26 | **Contextual Reply Draft** — "Draft a reply to that John email" | Chat | LLM uses Pull context → composes reply → Gmail Push | Read + reply without opening Gmail | Any professional |
| C-27 | **Refine Notes → Email Draft** — select rough notes, refine into email | Select + Refine | LLM refines into email → Gmail Push (draft) | Messy bullet points become polished draft | Writer, professional |
| C-28 | **Translate and Draft** — "Reply in French to Pierre about the quote" | Dictate | LLM composes in target language → Gmail Push | Professional foreign-language correspondence | Multilingual |
| C-29 | **Post-Session Clinical Summary** — therapist dictates to referring physician | Dictate | LLM structures → Gmail Push (draft) | Clinical notes captured immediately, draft = safe | Doctor, therapist |
| C-30 | **Hands-Free Email for Mobility Impaired** | Dictate | LLM → Gmail Push (draft) | Email fully accessible, no keyboard needed | Accessibility |
| C-31 | **Batch Personalized Drafts** — "Draft 3 emails to interview candidates" | Chat | LLM generates 3 personalized drafts → 3× Gmail Push | Batch email prep in under 2 minutes | Recruiter, organizer |
| C-32 | **Parent School Communication** — "Email teacher about Emma's absence" | Dictate | LLM composes polite email → Gmail Push | Quick parent-teacher emails, faster than thumb-typing | Parent |

### 4.5 Calendar + Gmail Combined

| # | Use Case | Trigger | Flow | Value | Primary User |
|---|----------|---------|------|-------|-------------|
| C-33 | **Unified Daily Briefing** — "Brief me on today" | Chat | Calendar Pull + Gmail Pull → LLM combines | Single-command briefing across both sources | Manager |
| C-34 | **Meeting Prep Package** — "Prep me for my next meeting" | Chat | Calendar Pull (attendees) + Gmail Pull (email history with them) → LLM synthesizes | Cross-referencing calendar + email = neither provides alone | Consultant |
| C-35 | **Schedule + Draft Invite** — for external contacts | Dictate | Calendar Push + Gmail Push (confirmation draft) | Two actions from one voice command | External meeting setup |
| C-36 | **Reschedule + Notify** | Chat | Calendar Pull → compose reschedule email → Gmail Push | Calendar data makes email accurate | Anyone rescheduling |
| C-37 | **End-of-Day Follow-Ups** | Chat | Calendar Pull (today's meetings) → user dictates notes → Gmail Push per meeting | Structured EOD routine, nothing falls through cracks | Professional |
| C-38 | **Conflict Resolution + Notification** | Dictate | Calendar Pull (check) → conflict → suggest alternatives or Calendar Push + Gmail Push | Pull-before-Push = intelligent scheduling | Scheduler |
| C-39 | **Delegate Meeting + Context Email** | Chat | Calendar Pull (details, attendees, agenda) → Gmail Push to delegate | Email includes real meeting details, not just "go to my 2 PM" | Manager |
| C-40 | **Weekly Status From Calendar** | Chat | Calendar Pull (week's events) → LLM narratives → Gmail Push (draft) | Calendar as work log → automated status update | Contractor, consultant |
| C-41 | **Networking Follow-Ups** — after events | Dictate | Calendar Pull (event name) → LLM composes personalized emails → Gmail Push × N | Batch follow-ups referencing the actual event | BD, salesperson |
| C-42 | **"What Did I Miss?" Catch-Up** after PTO/sick | Chat | Calendar Pull (missed meetings) + Gmail Pull (unread) → LLM briefing | Re-entry after absence replaces 30–60 min of scrolling | Returning from absence |
| C-43 | **Conditional Invoice Follow-Up** | Chat | Gmail Pull (check for reply) → no reply → Calendar Push (reminder) | Cross-connector conditional workflow via natural language | Freelancer |
| C-44 | **Anxiety-Reducing Completeness Check** — "Did I miss anything?" | Chat | Calendar Pull + Gmail Pull → LLM reassures or flags | Information verification for anxiety management | Neurodivergent |

---

## 5. Tier 2 — Mid-Tier: Knowledge Workers

> **Connectors**: Obsidian Vault (daily note append + standalone), Custom Folder (.md/.txt to any dir), Notion (future — database entries + pages)
> **Target users**: PKM practitioners, researchers, writers, developers, therapists, journalists, students, consultants
> **Key insight**: Voice adds value when hands are busy, capture windows are narrow, or speaking is more natural than writing. The LLM adds value by structuring, tagging, extracting, and cross-linking — turning raw speech into organized knowledge artifacts.

### 5.1 Personal Knowledge Management (PKM / Zettelkasten)

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-01 | **Fleeting Note Capture** — sudden insight while reading/walking | Dictate | Obsidian standalone | Auto-tags, generates filename, YAML frontmatter (`type: fleeting`) |
| K-02 | **Literature Note from Passage** — paraphrase in own words | Select + Note | Obsidian standalone | Paraphrases, blockquotes original, suggests connections |
| K-03 | **Daily Idea Log** — small thoughts throughout the day | Dictate | Obsidian daily append | Timestamped bullet under `## Ideas`, creates heading if missing |
| K-04 | **Concept Explanation (Feynman Technique)** — explain aloud | Dictate (1–3 min) | Obsidian standalone | Structures into definition / example / connections, suggests `[[wiki-links]]` |
| K-05 | **Weekly Review Synthesis** | Dictate (2–5 min) | Obsidian standalone | Formats: Themes, Insights, Priorities, Open Questions |
| K-06 | **Connection Note Between Concepts** — "these two ideas relate because..." | Select + Dictate | Obsidian standalone | Bridge note with `[[noteA]]`, `[[noteB]]`, relationship type |
| K-07 | **Permanent Note Promotion** — promote fleeting to permanent | Select + Dictate | Obsidian standalone | Combines + restructures, `evolved-from:` link, tags |
| K-08 | **Voice Bookmark While Reading** — WHY it matters, not just WHERE | Dictate | Obsidian daily append | Extracts page/location, observation, connection |
| K-09 | **GTD Inbox Capture** — tasks, ideas, random thoughts | Dictate | Obsidian daily append | Pre-classifies: task / idea / reference / someday-maybe |

### 5.2 Research & Academia

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-10 | **Lab Notebook Voice Entry** — gloved hands, wet lab | Dictate | Obsidian daily / Custom Folder | Timestamp, parse numbers/units, flag deviations |
| K-11 | **Field Notes** — ecology, archaeology, anthropology | Dictate | Custom Folder | Timestamp, location, species/artifact, hypotheses |
| K-12 | **Literature Review Annotation** — per-paper summary | Dictate (1–3 min) | Obsidian standalone | Summary / Methods / Findings / Limitations / Relevance template |
| K-13 | **Research Question Decomposition** — think aloud | Dictate (2–5 min) | Obsidian standalone | Extracts sub-questions hierarchically, flags testable vs. theoretical |
| K-14 | **Conference Talk Notes** | Dictate | Obsidian daily append | Speaker / Key Claims / Questions / Follow-up Actions |
| K-15 | **Thesis Argument Development** | Dictate (2–5 min) | Obsidian standalone | Thesis / Premises / Evidence Needed / Counterarguments |
| K-16 | **Peer Review Feedback** | Dictate (3–5 min) | Custom Folder | Summary / Major Concerns / Minor / Recommendation |
| K-17 | **Post-Lecture Reflection** (student, between classes) | Dictate | Obsidian daily append | Key Takeaways / Concepts to Review / Questions for Prof |
| K-18 | **Flashcard Generation** from selected text | Select + Ask | Obsidian standalone | Q&A pairs in Spaced Repetition plugin format |
| K-19 | **Verbal Self-Testing** for exam prep | Dictate | Obsidian daily append | Evaluates answer, scores, identifies gaps |
| K-20 | **Dissertation Writing by Dictation** — chapter by chapter | Dictate (long) | Custom Folder | Cleans spoken → academic prose, maintains citation format |

### 5.3 Software Development

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-21 | **Architecture Decision Record (ADR)** | Dictate (1–3 min) | Custom Folder (`docs/adr/`) | Context / Decision / Alternatives / Consequences template |
| K-22 | **Debugging Journal** — "what I just spent 2 hours finding" | Dictate | Obsidian daily append | Symptom / Investigation / Root Cause / Fix / Prevention |
| K-23 | **Code Review Dictation** — overall assessment before inline comments | Dictate | Custom Folder | Assessment / Concerns / Suggestions / Questions |
| K-24 | **Dev Log / Standup Prep** | Dictate (30–60s) | Obsidian daily append | Done / Blocked / Next bullet points |
| K-25 | **Rubber Duck Debugging** — explain problem aloud | Dictate (1–5 min) | Obsidian standalone | Summarizes problem, identifies assumptions, suggests approaches |
| K-26 | **Select Code → Explain → File** | Select + Ask | Obsidian standalone | Explanation with original code block, pattern identification |
| K-27 | **Incident Post-Mortem Draft** | Dictate (3–5 min) | Custom Folder (`docs/postmortems/`) | Timeline / Impact / Root Cause / Contributing Factors / Action Items |

### 5.4 Project Management

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-28 | **Meeting Notes with Action Items** | Dictate (2–3 min) | Obsidian standalone | Attendees / Decisions / Discussion / Action Items (checkboxes with owners) |
| K-29 | **Sprint Retrospective Capture** | Dictate | Obsidian standalone | Went Well / Didn't / Action Items with checkboxes |
| K-30 | **Status Update for Stakeholders** | Dictate (1–2 min) | Custom Folder / Obsidian | Progress / Risks / Next Week / Escalations |
| K-31 | **Project Decision Log** — "who decided that and why?" | Dictate (15–30s) | Obsidian daily append | Decision / Context / Decided By / Implications |
| K-32 | **Risk Register Entry** | Dictate | Obsidian standalone | Description / Likelihood / Impact / Mitigation / Owner |

### 5.5 Writing & Content Creation

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-33 | **Brainstorming / Freewriting** | Dictate (3–10 min) | Obsidian standalone | Light thematic clustering, "Promising Threads" section |
| K-34 | **World-Building Entry** (fiction) | Dictate | Obsidian standalone | Category-appropriate sections (character/location/magic), `[[wiki-links]]` |
| K-35 | **Blog Post First Draft** — while walking/commuting | Dictate (5–15 min) | Custom Folder | Title, intro, body with subheadings, conclusion |
| K-36 | **Video Script Outline** | Dictate | Custom Folder / Obsidian | Hook / Key Points / B-Roll Ideas / CTA, estimated durations |
| K-37 | **Newsletter Draft** | Dictate (5–10 min) | Custom Folder | Opening / Main Story / Links / CTA / Sign-off |
| K-38 | **Social Media Content Ideas** — rapid-fire | Dictate | Obsidian daily append | Separates ideas, suggests platform, drafts captions |
| K-39 | **Character Voice Exploration** — speak in-character | Dictate | Obsidian standalone | Preserves dialogue, adds stage directions, notes voice characteristics |
| K-40 | **Poet's Voice-First Composition** | Dictate → TTS → Refine | Obsidian standalone | TTS for rhythm check, revision history |
| K-41 | **D&D / TTRPG Session Recap** | Dictate (3–5 min) | Obsidian standalone | Summary / Events / NPCs / Loot / Mysteries / Next Session Hooks, `[[NPC]]` links |

### 5.6 Therapy, Coaching & Professional Services

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-42 | **Post-Session Clinical Notes** (therapist, NOT EMR) | Dictate (2–3 min) | Obsidian standalone (encrypted vault) | SOAP or DAP format, client initials only |
| K-43 | **Coaching Session Action Items** | Dictate | Obsidian standalone | Client Commitments / Coach Follow-ups / Key Themes / Progress |
| K-44 | **Client Intake Notes** (lawyer/consultant) | Dictate (2–3 min) | Obsidian standalone | Background / Stated Needs / Underlying Issues / Approach / Conflicts |
| K-45 | **Case Law Summary** (lawyer) | Dictate / Select + Ask | Obsidian standalone | Case Name / Citation / Facts / Holding / Implications |
| K-46 | **Deposition Summary** | Dictate | Obsidian standalone | Key Admissions / Contradictions / Impeachment Material / Strategic Notes |
| K-47 | **Discovery Interview Notes** (consultant) | Dictate | Obsidian standalone | Interviewee / Pain Points / Wish List / Contradictions / Quotes |

### 5.7 Journalism

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-48 | **Interview Notes with Quote Extraction** | Dictate | Obsidian standalone | Separates narrative from direct quotes, Key Themes / Usable Quotes |
| K-49 | **Story Idea Pitch** | Dictate (30–60s) | Obsidian daily append | Angle / Key Question / Sources to Contact / Effort Estimate |
| K-50 | **Source Profile** | Dictate | Obsidian standalone (`sources/`) | Name / Affiliation / Expertise / Reliability / Stories Useful For |
| K-51 | **Fact-Check Log** | Dictate | Obsidian standalone | Claim / Source / Verified (yes/no/partial) / Notes |

### 5.8 Personal Journaling & Reflection

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-52 | **Morning Pages / Freewriting** (5–15 min) | Dictate | Obsidian daily append | Minimal cleanup, preserves raw voice, auto-mood tag |
| K-53 | **Gratitude Journal** (3 items each evening) | Dictate (30–60s) | Obsidian daily append | Extracts 3 items, optional "why this matters" |
| K-54 | **Dream Journal** — immediately upon waking | Dictate (eyes closed) | Obsidian standalone | Light structure, symbol/theme tagging, `recurring: yes/no` |
| K-55 | **Mood & Energy Tracking** (3× daily) | Dictate (10–20s) | Obsidian daily append | Extracts mood (1–10), energy (1–10), keywords, context |
| K-56 | **End-of-Day Reflection** | Dictate (1–2 min) | Obsidian daily append | Wins / Lessons / Tomorrow's Priority |
| K-57 | **Micro-Journaling** — 5–10 brief entries throughout day | Dictate (each <30s) | Obsidian daily append | Timestamped bullets under `## Log` |
| K-58 | **Phone Call Summary** — immediately after hanging up | Dictate (30–90s) | Obsidian daily append | Called / Discussed / Committed To / Follow-up |

### 5.9 Niche & Specialized

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-59 | **Wine / Beer / Food Tasting Notes** | Dictate | Obsidian standalone | Appearance / Nose / Palate / Finish / Rating / Pairings |
| K-60 | **Birdwatching Field Log** — binoculars stay up | Dictate (5–10s) | Obsidian daily append | Species / Behavior / Location / Distance / Conditions |
| K-61 | **Workout / Training Log** — between sets, sweaty hands | Dictate (5–10s) | Obsidian daily append | Extract exercise / weight / reps / RPE |
| K-62 | **Travel Journal** | Dictate | Obsidian standalone | Location / Experience / Recommendations / Would Return? |
| K-63 | **Garden / Plant Care Log** — dirty hands | Dictate | Obsidian daily append | Plant / Location / Condition / Action Needed / Pest Notes |
| K-64 | **Recipe Notes** — cooking with messy hands | Dictate | Obsidian standalone | Ingredients / Instructions / Notes / Serves / Prep Time |
| K-65 | **Parenting / Child Development Notes** | Dictate | Obsidian daily append | Milestone / Age / Context, developmental category tags |
| K-66 | **Music Practice Log** | Dictate (30–60s) | Obsidian daily append | Pieces / Technique Focus / Breakthroughs / Needs Work |
| K-67 | **Vehicle Maintenance Log** | Dictate (15s) | Obsidian standalone | Service / Mileage / Cost / Provider / Next Due |
| K-68 | **Real Estate Property Notes** — during walkthrough | Dictate | Obsidian standalone | First Impression / Pros / Cons / Comparables / Verdict |
| K-69 | **Inventory / Collection Cataloging** | Dictate per item | Obsidian standalone | Type-appropriate fields (books: title/author/condition; vinyl: artist/pressing/grade) |
| K-70 | **Habit Tracking with Context** | Dictate | Obsidian daily append | Extract habit name + completion + qualitative notes |

### 5.10 Notion-Specific (Future)

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-71 | **Dictation → Notion Database Row** | Dictate | Notion | Extract task / assignee / deadline / priority → property mapping |
| K-72 | **CRM Contact Note** | Dictate | Notion | Extract name / company / role / interest / follow-up date |
| K-73 | **Content Calendar Entry** | Dictate | Notion | Extract title / type / target date / audience / keywords |
| K-74 | **Meeting Notes Page** | Dictate | Notion | Notion blocks (headings, bullets, todos) + page properties |
| K-75 | **Sprint Board Task Creation** | Dictate | Notion | Extract title / description / assignee / estimate / sprint |
| K-76 | **Bug Report** | Dictate | Notion | Steps to Reproduce / Expected / Actual / Severity |

### 5.11 Custom Folder-Specific

| # | Use Case | Trigger | Connector | LLM Value-Add |
|---|----------|---------|-----------|---------------|
| K-77 | **Logseq Daily Journal** — compatible outliner format | Dictate | Custom Folder | Logseq-compatible indented bullets (`-`) to `journals/YYYY_MM_DD.md` |
| K-78 | **Plain Markdown Project Wiki** — in git repo | Dictate | Custom Folder (`docs/wiki/`) | Wiki article with headings and cross-references |
| K-79 | **Joplin / Bear Import** — via watched folder | Dictate | Custom Folder | Standard markdown to import directory |
| K-80 | **Client Deliverable Draft** — reports, proposals | Dictate (5–15 min) | Custom Folder | Professional document structure |
| K-81 | **Academic Paper Section Draft** | Dictate | Custom Folder | Academic prose with section conventions |

---

## 6. Tier 3 — Advanced: Power Users & Developers

> **Connectors**: Generic Webhook (Zapier/n8n/Make/custom), Discord Webhook, Streamer.bot (WebSocket)
> **Target users**: Streamers, developers, DevOps, sales, support, community managers, home automation enthusiasts
> **Key insight**: Three transformative patterns: (1) voice replaces typing in external systems, (2) voice triggers multi-system orchestration, (3) fully local voice automation via n8n + Streamer.bot

### 6.1 Streaming & Content Creation (Streamer.bot)

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-01 | **Voice-Triggered Scene Switching** | Dictate: "switch to BRB" | → Streamer.bot → OBS scene change | Hands-free scene control during gameplay, essential for VR |
| A-02 | **Voice-Reactive Stream Alerts** | Dictate trigger phrases | → Streamer.bot → OBS text overlay | Unique interactive moments, raw transcript = exact spoken words |
| A-03 | **Answer Chat Questions by Voice** | Select chat Q + Ask | → Streamer.bot → Twitch chat post | Stay in gameplay while engaging chat, LLM refines answer |
| A-04 | **Voice-Controlled Lighting** — "go red", "party mode" | Dictate | → Streamer.bot → Hue/LIFX API | Theatrical mood shifts from voice alone |
| A-05 | **Live Captioning** for hearing-impaired viewers | Every dictation | → Discord Webhook → #live-captions | Accessibility via rawTranscript field |
| A-06 | **Highlight Bookmarking** — "mark highlight: triple kill" | Dictate with prefix | → Webhook → n8n → Google Sheet | Timestamps for post-stream clip editing |
| A-07 | **Voice Sound Effects** — "play sad trombone" | Dictate | → Streamer.bot → OBS media source | Hands-free audio production, spontaneous comedy |
| A-08 | **Voice Clip Creation** — "Clip that!" | Dictate | → Streamer.bot → OBS replay buffer / Twitch clip API | Instant clip without keyboard shortcut |
| A-09 | **Voice Camera Zoom/Pan** | Dictate: "close-up" / "wide" | → Streamer.bot → OBS source transform | Multi-camera feel from single camera |
| A-10 | **Voice Raid** — "Raid CozyGamer" | Dictate | → Streamer.bot → Twitch raid + OBS raid scene | Smooth end-of-stream, no typing `/raid` |
| A-11 | **VTuber Avatar Expressions** — "smile", "wave" | Dictate | → Streamer.bot → VTube Studio hotkey | Natural expression triggers during performance |
| A-12 | **Voice Giveaway System** — start, draw winner | Dictate | → Streamer.bot → chat monitor → random draw → announce | Entire giveaway by voice, stay in character |
| A-13 | **Bidirectional: Events → TTS** — sub/raid read aloud | Streamer.bot event | → dIKta.me Kokoro TTS | Higher quality alerts than generic TTS, unique stream identity |
| A-14 | **Voice OBS Filter Toggle** — "go black and white" | Dictate | → Streamer.bot → OBS filter toggle | Creative visual effects from voice |
| A-15 | **Voice Ad Break** — "run ads, 90 seconds" | Dictate | → Streamer.bot → Twitch ad API + OBS scene | Ads at natural breaks, streamer controls timing |

### 6.2 Developer Workflows (Webhook → Zapier/n8n)

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-16 | **Voice GitHub Issues** — dictate bug report | Dictate | → Webhook → Zapier → GitHub Issue | 15 seconds vs. 3 minutes, captures at discovery |
| A-17 | **Voice PR Descriptions** | Dictate → Refine | → Webhook → n8n → GitHub API | Comprehensive descriptions in seconds |
| A-18 | **Voice Commit Messages** | Dictate | → Refine → conventional commit format → clipboard | Encourages better commits: "fix stuff" → proper message |
| A-19 | **Voice CI/CD Pipeline Trigger** | Dictate: "deploy feature-auth to staging" | → Webhook → n8n → GitHub Actions workflow_dispatch | No browser navigation, hands on dashboards |
| A-20 | **Voice Code Documentation** | Select function + Ask | → LLM generates XML doc / JSDoc → inject | 10 seconds to document a function |
| A-21 | **Voice Jira/Linear Tickets** — during planning | Dictate | → Webhook → Zapier → Jira ticket | Tickets created during meeting, not after |
| A-22 | **Voice Error Log Annotation** | Select stack trace + Ask | → Webhook → n8n → debugging journal | Searchable debug knowledge base |
| A-23 | **Voice Database Migration Notes** | Dictate | → Webhook → n8n → docs | Captures the "why" behind schema changes |

### 6.3 DevOps & Incident Management

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-24 | **Voice Incident Logging** — during outage | Dictate (HMAC-signed) | → Webhook → n8n → incident log + Slack + PagerDuty | Hands on terminal, voice logs timeline |
| A-25 | **Voice Runbook Execution** — "restart payment service" | Dictate | → Webhook → n8n → lookup + execute | Reduces MTTR, junior engineers safely execute |
| A-26 | **Voice Status Page Update** | Dictate → Refine | → Webhook → Zapier → Statuspage.io | Customer-facing updates in seconds, not minutes |
| A-27 | **Voice Infrastructure Scaling** — "scale to 10 replicas" | Dictate (HMAC-signed) | → Webhook → n8n (whitelist-validated) → k8s/AWS | Faster than cloud consoles during traffic spikes |
| A-28 | **Voice Post-Mortem Capture** | Dictate | → Refine → Webhook → n8n → Notion + Jira tickets | Postmortem while fresh, action items auto-created |

### 6.4 Sales & CRM (Webhook → Zapier)

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-29 | **Post-Call CRM Notes** | Dictate | → Webhook → Zapier → HubSpot/Salesforce activity note | 30 seconds vs. 5 minutes, captures competitor intel |
| A-30 | **Trade Show Lead Capture** | Dictate | → Webhook → Zapier → HubSpot contact + deal + auto email | Instant CRM entry + follow-up, not business cards |
| A-31 | **Voice Follow-Up Scheduling** | Dictate | → Webhook → Zapier → Calendar + Todoist + Gmail draft | Follow-ups = #1 deal closure predictor, make it effortless |
| A-32 | **Voice Pipeline Stage Update** | Dictate | → Webhook → Zapier → CRM deal update | Pipeline accuracy improves when updates are frictionless |

### 6.5 Customer Support

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-33 | **Voice Support Tickets** — during/after call | Dictate | → Webhook → Zapier → Zendesk/Freshdesk | Tickets with full detail, created real-time |
| A-34 | **Voice KB Article Draft** — recurring question spotted | Select Q + Ask | → Webhook → n8n → Notion/Confluence | KB articles that would never get written, 80% done instantly |
| A-35 | **Voice Multi-System Escalation** | Dictate | → Webhook → n8n → ticket + PagerDuty + Slack + account mgr | 4-system escalation in one voice command |

### 6.6 Discord Community Management

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-36 | **Voice Announcements** | Dictate | → Discord Webhook → #announcements embed | Formatted, timestamped, posted from anywhere |
| A-37 | **Voice Mod Logging** | Dictate | → Discord Webhook → #mod-log embed | Consistent audit trail without bot command syntax |
| A-38 | **Voice Bug Reports** (alpha/beta testing) | Dictate | → Discord Webhook → #bug-reports embed (red, severity field) | Bug reports in seconds, consistent structure |
| A-39 | **Voice Changelog** | Dictate → Refine | → Discord Webhook → #changelog embed | Real-time community updates without opening Discord |
| A-40 | **Multi-Language Announcements** | Dictate → Translate × N | → Discord Webhook × N (#announcements-en, -es, -ja) | One spoken announcement → 4+ language-specific posts |

### 6.7 Home Automation & IoT (n8n local)

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-41 | **Voice Smart Home Control** — fully local | Dictate | → Webhook → n8n (localhost) → Home Assistant REST API | No Alexa/Google listening, more flexible, works offline |
| A-42 | **Voice Morning/Night Routines** | Dictate: "good morning" | → Webhook → n8n → HA scene (blinds+coffee+thermostat+music) | One phrase → 5+ device actions with conditional logic |
| A-43 | **Voice → MQTT → Custom IoT** | Dictate | → Webhook → n8n → MQTT publish | Voice control for Arduino/ESP32, no full assistant stack needed |
| A-44 | **Voice Security System** — "arm away mode" | Dictate (HMAC-signed) | → Webhook → n8n (local) → HA alarm panel | Authenticated, fully local, convenient with full hands |

### 6.8 Music Production (Streamer.bot)

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-45 | **Voice DAW Control** — "record", "stop", "loop last 4 bars" | Dictate | → Streamer.bot → MIDI CC / keystrokes → DAW | Hands stay on instrument, no reaching for mouse |
| A-46 | **Voice Audio Routing** — "mute guest mic", "switch to mix B" | Dictate | → Streamer.bot → MIDI/OSC → audio interface | No touching mixer during live recording |
| A-47 | **Voice Session Notes** — creative ideas during production | Dictate | → Streamer.bot → session log | 5-second capture, don't break creative flow |

### 6.9 Data Collection & Productivity

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-48 | **Voice Time Tracking** — "log 2 hours on Atlas, billable" | Dictate | → Webhook → Zapier → Toggl/Harvest/Clockify | Capture more billable hours, 5 seconds vs. web UI |
| A-49 | **Voice Expense Logging** — "lunch with client, $47.50" | Dictate | → Webhook → Zapier → Expensify | Instant vs. receipt-shoebox-at-month-end |
| A-50 | **Voice Inventory Counting** — warehouse, hands carrying boxes | Dictate | → Webhook → n8n → inventory system | Both hands on boxes, voice captures counts + damage notes |
| A-51 | **Dictation Productivity Dashboard** — automatic | Every dictation (metadata only) | → Webhook → Zapier → Google Sheets → dashboard | "12,000 words this week at 142 WPM, 3.2 hours saved" |
| A-52 | **Voice Task Creation** — Todoist/Things/Asana | Dictate | → Webhook → Zapier → task manager API | Task capture at speed of thought |

### 6.10 Team Communication

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-53 | **Voice-to-Slack** | Dictate | → Webhook → Zapier → Slack API | 15 seconds vs. 2 minutes typing, posted immediately |
| A-54 | **Voice Standup Submission** (async) | Dictate → Refine | → Webhook → Zapier → #standups | Yesterday/Today/Blockers in 30 seconds of speech |
| A-55 | **Voice Meeting Notes Distribution** | Dictate → Refine | → Webhook → n8n → email + Asana tasks + Slack | One dictation fans out to 3 destinations, each formatted |
| A-56 | **Multi-Destination Broadcast** — same message everywhere | Dictate | → Webhook → n8n → Slack + Discord + Mailchimp + Notion | One input, 4 platform-adapted outputs |
| A-57 | **Voice Emergency Broadcast** — "all services down" | Dictate | → Webhook → n8n → Slack + PagerDuty + SMS + status page | Critical seconds matter, one voice → every channel |

### 6.11 Fully Local Automation Stack (n8n)

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-58 | **Zero-Cloud Voice Automation** | Any dictation | Whisper → Ollama → Webhook → n8n (localhost) → local services | Entire stack on one machine, zero cloud, zero cost |
| A-59 | **Voice Database Queries** (dev) | Dictate | → Webhook → n8n → parameterized SQL (dev only, whitelisted) | Faster than writing SQL, with safety guardrails |
| A-60 | **Voice Docker Management** | Dictate: "restart API container" | → Webhook → n8n → Docker CLI | Container management without terminal switching |
| A-61 | **Voice Script Execution** — "run weekly analytics report" | Dictate | → Webhook → n8n → Python/PowerShell script | Voice-activated script runner, no remembering paths |
| A-62 | **Voice File Organization** | Dictate: "move screenshots to archive by month" | → Webhook → n8n → file system ops | Natural language batch file operations |

### 6.12 Webhook Patterns (Zapier-Specific)

| # | Use Case | Trigger | Flow | Value |
|---|----------|---------|------|-------|
| A-63 | **Conditional Routing by Mode** | `mode` field in payload | → Zapier Filter: Dictation→Sheets, Translate→Slack, Ask→Notion | Single webhook URL, mode-based routing |
| A-64 | **Privacy-Mode Analytics** | Every dictation (metadata only) | → Zapier → Google Sheets (wordCount, timing, no text) | Productivity tracking without surveillance |
| A-65 | **HMAC-Verified Chain** for regulated industries | Every dictation (signed) | → n8n → verify signature → compliance database | Cryptographic data integrity proof for audits |

---

## 7. Cross-Connector Workflows

> **Key pattern**: One dictation triggers 2–5 connectors simultaneously. The value is combinatorial — eliminating the distribution labor of manually posting to multiple systems.

| # | Use Case | Connectors Fired | Flow |
|---|----------|-----------------|------|
| X-01 | **Meeting Minutes Broadcast** | Obsidian + Calendar + Webhook→Slack | LLM structures minutes → notes saved + follow-ups calendared + team notified |
| X-02 | **Sales Call Debrief** | Obsidian + Calendar + Webhook→CRM + Gmail | Intel captured → CRM updated → follow-up scheduled → draft sent |
| X-03 | **Incident Response Broadcast** | Webhook→PagerDuty + Discord + Calendar + Obsidian | Status to all stakeholders + time blocked + post-mortem draft started |
| X-04 | **Content Publishing Pipeline** | Obsidian + Webhook→WordPress + Discord + Gmail | Blog post saved + CMS draft created + community notified + newsletter drafted |
| X-05 | **Freelancer Time-and-Task** | Obsidian + Webhook→Toggl + Calendar + Notion | Work logged + time tracked + calendar updated + client tracker updated |
| X-06 | **Family Event Coordinator** | Calendar + Gmail + Custom Folder + Discord | Event created + invites drafted + planning doc saved + family notified |
| X-07 | **Research Paper Annotation** | Obsidian + Notion + Webhook→Zotero | Annotation in vault + shared research DB + paper record updated |
| X-08 | **Streamer Interactive Event** | Streamer.bot + Discord + Webhook→IFTTT | OBS scene + community notified + room lights changed |
| X-09 | **Focus Session Trigger** | Webhook→n8n (Toggl + Slack DND + phone DND + lights + site blocker) | One command sets up entire focus environment |
| X-10 | **Customer Onboarding** | Webhook→n8n (Notion + Slack + Gmail + Asana + Calendly + Stripe) | 6-system onboarding from 15 seconds of speech |

---

## 8. Accessibility Use Cases

> **Key insight**: Accessibility is not a niche — it's a fundamentally different interaction model (conversational vs. visual-spatial) that benefits permanently disabled users, temporarily disabled users, AND situationally impaired users (driving, cooking, hands full). Connectors eliminate the need to navigate complex UIs, which is itself an accessibility barrier.

| # | Use Case | User | Trigger | Connectors | Value |
|---|----------|------|---------|-----------|-------|
| ACC-01 | **Quadriplegic Full Digital Life** — email, calendar, notes via sip-and-puff switch | C4 SCI | Dictate (switch) | Gmail + Calendar + Obsidian | Bridges assistive switches ↔ full desktop, no complex UI navigation |
| ACC-02 | **Vision-Impaired Voice-In/Voice-Out Loop** — researcher | Blind/low-vision | Dictate + TTS | Obsidian + Gmail + Calendar | Direct voice loop bypasses visual UI entirely |
| ACC-03 | **ADHD Capture Before It Vanishes** — sub-5-second capture | ADHD | Dictate (burst) | Calendar + Obsidian + Webhook→Todoist | No context switch, thought captured + routed in 3 seconds |
| ACC-04 | **Dyslexic Professional Email Refinement** | Dyslexia | Select + Refine | Gmail (draft) | Full rewrite + TTS read-back plays to auditory strengths |
| ACC-05 | **RSI/Tremor Keyboard Replacement** — full workday by voice | RSI, tremor | Dictate (primary input) | Custom Folder + Gmail + Calendar | Eliminates repetitive mouse-clicking between apps (itself an RSI trigger) |
| ACC-06 | **Elderly Simplified Digital Life** — one interaction to learn | Elderly (78+) | Dictate | Gmail + Calendar + Custom Folder | One button replaces learning Gmail + Calendar + file management |
| ACC-07 | **Post-Surgery Temporary Disability** — 8 weeks with both wrists in casts | Broken wrists | Dictate (foot pedal) | Discord + Obsidian + Calendar + Gmail | Full keyboard replacement for recovery period |
| ACC-08 | **Autistic Social Communication Drafting** | Autism spectrum | Dictate + Refine | Gmail (draft) + TTS | Raw intent → LLM handles "social translation", user stays authentic |
| ACC-09 | **Voice Desktop Control** via Streamer.bot | Any motor impairment | Dictate | Streamer.bot → Win32 APIs | Launch apps, arrange windows, navigate — all by voice |
| ACC-10 | **Voice Spreadsheet Data Entry** | Cannot use keyboard | Dictate | Webhook → Zapier → Google Sheets | Structured data entry without any keyboard use |

---

## 9. Privacy-Sensitive Use Cases

> **Key insight**: Local mode is not a feature — it's a market. An entire segment (attorneys, clinicians, corporate R&D, government, journalists, therapy journaling) is structurally locked out of cloud AI tools by regulation, policy, or personal risk tolerance. Fully local processing is a hard requirement that most competitors CANNOT meet.

| # | Use Case | Privacy Requirement | Pipeline | Connector | Why Cloud Fails |
|---|----------|-------------------|----------|-----------|----------------|
| P-01 | **Attorney Case Notes** | Attorney-client privilege | Ghost, fully local | Obsidian (encrypted) | Cloud transmission could waive privilege if subpoenaed |
| P-02 | **HIPAA Clinical Notes** | PHI protection | Ghost, fully local | Custom Folder (on-premise) | Requires BAA with any cloud provider processing PHI |
| P-03 | **Corporate R&D IP** — pre-patent trade secrets | Corporate policy | Ghost, fully local | Obsidian + Custom Folder (air-gapped NAS) | Apple/Samsung/Intel ban cloud AI for R&D |
| P-04 | **Therapy/Personal Journal** — deepest personal content | Emotional privacy | Ghost, fully local | Custom Folder (VeraCrypt) | Cloud = data breach risk for most sensitive content |
| P-05 | **Whistleblower / Journalist Source Protection** | Source safety | Ghost, fully local, telemetry OFF | Custom Folder (encrypted USB) | Ghost = no metadata, no record dictation occurred |
| P-06 | **Government Classified (SCIF-adjacent)** | Security clearance | Ghost, air-gapped | Custom Folder (classified FS) | Zero network capability, models pre-loaded via sneakernet |
| P-07 | **GDPR Data Subject Compliance** — EU HR notes | GDPR Chapter V | Balanced, local LLM | Custom Folder (EU-hosted) | Local avoids international data transfer issues |
| P-08 | **Local n8n Automation** — privacy-conscious power user | No cloud dependency | Any local mode | Webhook → n8n (localhost) | Entire chain on LAN, zero cloud, self-hosted |

---

## 10. Key Patterns & Strategic Insights

### 10.1 Five Patterns That Drive Value

**Pattern 1: "Hands-Busy" Context**
The majority of unique use cases share one trait: the user's hands are occupied (cooking, gardening, inspecting, driving, holding binoculars, covered in grease, holding a dog leash, chalked at the gym, gloved in a lab). Voice is not just convenient — it's the **only viable input method**.

**Pattern 2: "Capture Window" Timing**
Many use cases have a narrow window where information is available: dream recall upon waking (5 min), post-therapy insight (30 min), post-meeting memory (1 hour), mid-observation birdwatching (seconds), adrenaline-fresh incident reports (minutes). Dictation captures at peak fidelity because it has the lowest activation energy.

**Pattern 3: "Pull-Before-Push" Intelligence**
The most valuable Google use cases read data first, then act on it. The LLM mediates between "what exists" and "what should happen next." Calendar conflict checking before event creation, email search before composing a reply — this is the core differentiator over simple voice commands.

**Pattern 4: "Broadcast" Distribution Elimination**
Cross-connector workflows eliminate the labor of manually posting to 3–5 different systems. Information that currently requires opening each app, formatting appropriately, and posting individually happens from a single dictation. The value is combinatorial.

**Pattern 5: "Draft-Not-Send" Trust Building**
For every email use case, the draft model provides a review checkpoint. This is essential for legal/medical professionals, multilingual composition, and accessibility users who need to verify voice recognition accuracy. It builds trust incrementally.

### 10.2 The LLM Is the Differentiator

Without the LLM, connectors are just voice wrappers around APIs. With the LLM, users get:

| LLM Capability | Example |
|----------------|---------|
| **Natural language parsing** | "Schedule meeting next Thursday at 2" → structured event |
| **Cross-source reasoning** | Calendar attendees × email history = meeting prep package |
| **Urgency detection** | "Anything urgent?" filters by keywords, sender importance |
| **Language translation** | Dictate in Spanish → event created in English or Spanish |
| **Tone adjustment** | Raw intent → diplomatically worded professional email |
| **Template structuring** | Rambling dictation → SOAP note, ADR, tasting note, bug report |
| **Metadata extraction** | Auto-tags, frontmatter, categories, priorities, assignees |
| **Wiki-link generation** | Suggests `[[connections]]` that build the knowledge graph passively |
| **Conditional logic** | "If no reply, remind me Monday" — cross-connector workflow |

### 10.3 Three Markets, One Product

| Market | Size | What They Pay For | dIKta.me's Pitch |
|--------|------|-------------------|-----------------|
| **Privacy Market** | Attorneys, clinicians, govt, R&D, journalists | Local processing, no cloud | Only AI dictation tool that's fully air-gappable with integrations |
| **Productivity Market** | Knowledge workers, managers, writers, students | Time saved, friction removed | Voice → structured notes/emails/events in seconds, not minutes |
| **Automation Market** | Streamers, DevOps, home automation, power users | Multi-system orchestration | One voice command → 5 systems updated simultaneously |

### 10.4 Competitive Moat

1. **Local-first + integrations**: No competitor bridges both camps
2. **Windows exclusivity**: Superwhisper is Mac-only
3. **LLM pipeline**: Cloud transcription tools have no LLM formatting layer
4. **File system access**: Cloud tools fundamentally cannot write to Obsidian vaults
5. **Zero marginal cost for local connectors**: Can undercut $18–30/mo cloud pricing
6. **Composable presets**: Per-preset connector routing is a novel architecture no one has

---

## Appendix: Use Case Count Summary

| Category | Count |
|----------|-------|
| Tier 1 — Google Calendar + Gmail | 44 |
| Tier 2 — Obsidian / Folder / Notion | 81 |
| Tier 3 — Webhook / Discord / Streamer.bot | 65 |
| Cross-Connector Workflows | 10 |
| Accessibility | 10 |
| Privacy-Sensitive | 8 |
| **Total (deduplicated)** | **218** |
