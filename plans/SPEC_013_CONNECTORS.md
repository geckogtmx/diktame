# SPEC_013: Connectors & App Integrations

> **Status:** DRAFT
> **Date:** 2026-03-13
> **Supersedes:** N/A (Net-new architecture)
> **Goal:** Create a unified framework for dIKta.me to securely interact with external applications (cloud and local) while maintaining a strict privacy-first, local execution model.

---

## 1. Executive Summary

As dIKta.me evolves beyond a simple dictation app into an autonomous, context-aware assistant (via Meeting Intelligence `SPEC_001`, Chat features, etc.), its value grows exponentially with **access to the user's broader digital workspace**. 

The "Connectors" module establishes a standard interface for the app to:
1.  **Ingest Context:** Read calendar events, fetch emails, or retrieve Slack messages to ground LLM prompts ("Draft a reply to the last email from Bob").
2.  **Take Action:** Push generated artifacts (meeting summaries, markdown notes) to destinations like Notion, Obsidian, Jira, or email drafts.

Crucially, this is designed as a **Local-First Integration Hub**. Unlike tools like Zapier, all authentication tokens remain securely encrypted on the user's local machine via DPAPI. Connectors are executed locally.

---

## 2. Architectural Design: The `IConnector` Interface

All integrations operate on a standardized C# interface within `DiktaMe.Core`. The system acts dynamically to load active Connectors into the `LLMRouter` and `Pipelines` (like Chat and Scribe).

### 2.1 Core Interface Definition
```csharp
public interface IConnector
{
    string Id { get; }                  // e.g., "notion", "gmail", "meet"
    string DisplayName { get; }         // e.g., "Notion Workspace", "Google Meet"
    ConnectorCategory Category { get; } // Knowledge, Communication, Scheduling, Task, Video
    bool IsConfigured { get; }          // True if valid auth credentials exist

    // Returns a list of discrete operations this connector supports
    Task<IEnumerable<ConnectorAction>> GetAvailableActionsAsync(); 

    // Executes a specific action (e.g., pushing Markdown or fetching calendar events)
    Task<ConnectorResult> ExecuteActionAsync(ConnectorAction action, Dictionary<string, object> parameters);

    // Context retrieval for RAG or LLM system prompts
    Task<string> GetContextAsync(ConnectorQuery query);
}
```

### 2.2 Local Security & Authentication (`SecureStorage.cs`)
-   **No Cloud Relay**: dIKta.me acts as the OAuth client directly (via local TCP port redirect for auth flows) or accepts Personal Access Tokens (PATs).
-   **Storage**: Credentials are encrypted using Windows Cryptography API: Next Generation (CNG) / DPAPI via the existing `SecureStorage` class.

---

## 3. Priority Applications & Synergies (V1 Focus)

This tier list dictates implementation priority, focused specifically on high-synergy wins with Dictation and the upcoming Meeting Intelligence (Scribe) module.

### Tier 1: The Essentials (Immediate Value)

1.  **Google Meet & Google Calendar** (Scheduling / Video)
    *   *Synergy*: The immediate enabler of `SPEC_001_MEETINGS`. Meeting Intelligence needs to know *what* meeting is happening to contextualize the summary.
    *   *Actions*: `GetUpcomingMeetings()`, `GetMeetingParticipants(meetingId)`.
    *   *Context*: Pre-fill session titles and attendee names for diarization. Provide context to the "Ask this meeting" LLM about the meeting agenda.

2.  **Notion** (Cloud Knowledge Base)
    *   *Synergy*: The standard for modern cloud workers. The ideal destination for Scribe artifacts or complex dictated notes.
    *   *Actions*: `AppendToPage(pageId, markdown)`, `CreateDatabaseEntry(dbId, properties)`.
    *   *Use Case*: "Save this meeting summary to the Customer Discovery Notion database."

3.  **Obsidian** (Local Knowledge Base)
    *   *Synergy*: Philosophically aligned. Local markdown files match dIKta.me's local-first ethos.
    *   *Actions*: `WriteMarkdownFile(path, content)`.
    *   *Implementation*: A simple filesystem connector operating on the user's defined Obsidian vault directory. Zero APIs required.

### Tier 2: Communications

4.  **Gmail / Outlook Email** (Communications)
    *   *Synergy*: Eliminating the friction of writing long, nuanced emails.
    *   *Actions*: `DraftEmail(to, subject, htmlBody)`, `GetRecentEmails(count, threadId)`.
    *   *Use Case*: "Summarize this thread and draft a polite refusal."

5.  **Slack / MS Teams / Discord** (Chat & Community)
    *   *Synergy*: Fast, asynchronous updates, team visibility, and community management.
    *   *Discord Implementation Options*:
        *   **Option A (Webhooks - Simple)**: For one-way posting of meeting summaries or dictations directly to a specific channel. No OAuth or bot user required: the user just pastes a Discord channel Webhook URL into dIKta.me settings. Highly aligned with the local-first ethos as there's no complex cloud auth overhead.
        *   **Option B (OAuth / Bot API - Advanced)**: Requires registering a full Discord Application. Unlocks deep context gathering (e.g., retrieving previous messages to ground Chat context) and dynamic posting capabilities.
    *   *Actions (Advanced)*: `SendDirectMessage(userId, markdown)`, `PostToChannel(channelId, markdown)`, `GetRecentMessages(channelId, limit)`.

6.  **WhatsApp** (Direct Messaging)
    *   *Synergy*: Sending quick updates or summaries directly to clients or personal contacts.
    *   *Caveat (The WhatsApp Dilemma)*: WhatsApp integrations force a choice between two heavily compromised paths:
        *   **Path A (Official Business API)**: Requires a registered business. Severe limitation: outside of a 24-hour reply window, you can ONLY send pre-approved template messages (which cost money and require Meta review). You cannot send free-form dictated text.
        *   **Path B (Unofficial Personal Automation)**: Bypasses the Business API by automating an actual WhatsApp Web session (e.g., via `whatsapp-web.js`, Baileys, or a self-hosted API wrapper). While this allows free-form sending from your personal number, it explicitly violates WhatsApp Terms of Service and carries a **high risk of account bans**.
    *   *Implementation Strategy*: If attempted, Path B (Unofficial) via a self-hosted wrapper API (like WAHA) is the only way to achieve the local-first, free-form dictation workflow dIKta.me requires. However, it must be marked in settings as an "Experimental/Use at your own risk" feature due to ban potential.

### Tier 3: Workflows & Action Items (Post-MVP)

6.  **Jira / Linear / ClickUp / GitHub** (Task Management)
    *   *Synergy*: Translating unstructured thought into structured tickets or GitHub issues.
    *   *Actions*: `CreateTicket(project, title, description, assignee)`, `CreateIssue(repo, title, body)`.

7.  **n8n / Zapier / Make** (Workflow Automation)
    *   *Synergy*: Unlocks thousands of downstream apps by acting as a universal plumbing layer.
    *   *Actions*: `TriggerWebhook(url, payload)`.
    *   *Philosophy Check*: While Zapier is inherently cloud-reliant (conflicting *slightly* with the local-first ethos), **n8n** can be hosted locally, making it the perfect automation partner for dIKta.me. We should build a **Generic Webhook Connector** that users can point to their Zapier Catch Hooks or local n8n Webhook triggers to initiate arbitrary, multi-step cloud/local workflows (e.g., "When a Scribe session ends, send the summary to this n8n webhook").

8.  **Salesforce / HubSpot / Pipedrive** (CRM - Enterprise Focus)
    *   *Synergy*: Pushing meeting notes (Discovery calls, Sales pitches) directly to contact or deal records. Highly requested by power users (core feature of Granola/Fellow).
    *   *Actions*: `UpdateContactNotes(contactId, markdown)`, `AppendToDeal(dealId, markdown)`.

9.  **Workday / BambooHR** (HRIS - Enterprise Focus)
    *   *Synergy*: Logging 1:1 meeting summaries or performance review notes to the employee's internal record.
    *   *Actions*: `LogEmployeeNote(employeeId, markdown)`.

---

## 4. Special Chapter: The Streamer.bot Integration

While initially designed for live streamers, **Streamer.bot** has evolved into a massively powerful, local-first automation engine capable of executing C# code, running complex pipelines (Actions/Sub-actions), and bridging hundreds of platforms via WebSockets, HTTP, and Webhooks.

### 4.1 Why Streamer.bot matters to dIKta.me
Because both applications share a **local-first C# philosophy**. By creating a dedicated, bidirectional `StreamerBotConnector`, dIKta.me can instantly inherit Streamer.bot's massive ecosystem of integrations (OBS, smart lights, local APIs, MIDI devices, Voicemeeter) without having to build them from scratch. 

### 4.2 Synergies & Use Cases
1.  **Voice-Controlled Local Environment**: 
    *   *User dictates*: "Switch the lights to focus mode and mute Spotify."
    *   *dIKta.me Chat/LLMRouter* isolates the intent and calls `StreamerBotConnector.TriggerAction("FocusMode")`. Streamer.bot executes the C# pipeline to alter Philips Hue and manipulate local Windows audio.
2.  **Streaming & Recording Workflows**:
    *   *User dictates*: "Clip that last 30 seconds."
    *   *dIKta.me* triggers a Streamer.bot action that commands OBS to save a replay buffer, while simultaneously logging a timestamped note in the dIKta.me notepad.
3.  **Local Webhook & WebSocket Orchestration**: 
    *   Rather than pointing dIKta.me to cloud-based Zapier, users can point the dIKta.me **Generic Webhook Connector** to a local Streamer.bot HTTP Server instance. This ensures that even complex multi-step automations (e.g., parsing a meeting summary and distributing it to local files and local Chat apps) never leave `localhost`.

### 4.3 Implementation Strategy
*   **Method A (WebSocket)**: Standard bidirectional communication. dIKta.me connects to Streamer.bot's local WebSocket Server to trigger Actions and listen for events.
*   **Method B (HTTP/Webhooks)**: dIKta.me fires simple HTTP POST requests to Streamer.bot's local web server to trigger predefined C# sub-actions.

---

## 5. Workflows & User Experience

### 4.1 Scribe Integration (Meetings)
When a Scribe session ends, the generated artifact (Markdown) UI will present "Export Paths" based on configured Connectors:
*   [Button] Copy to Clipboard
*   [Button] Push to Notion (Select Page)
*   [Button] Save to Obsidian Vault
*   [Button] Draft Email to Participants (requires Google Calendar connector context + Email connector)

### 4.2 Chat Context (LLM Integration)
Connectors can expose standard functions to the `LLMRouter` (using OpenAI or Anthropic tool/function calling syntax).
*   *User*: "What's my next meeting about?"
*   *LLM*: Invokes `GoogleCalendarConnector.GetUpcomingMeetings()`. Reads the description and agenda. Outputs: "You have a sync with Bob at 2 PM regarding the Q3 budget."

### 4.3 Setting Up Connectors
Defined via the new `SettingsView` (as planned in `SPEC_012_SETTINGS_REVAMP.md`).
A "Connectors" tab provides a grid of available integrations. Clicking one opens a flyout for OAuth login or API key/Vault Path entry.

---

## 5. Security & Privacy Guarantees

1.  **Strict Local Execution**: dIKta.me will never host a middleman server to handle webhooks or OAuth token exchanges. The desktop app acts directly as the client.
2.  **Consent Prompts**: Before a Connector executes an action that *mutates* state (e.g., sending an email, writing a file, creating a ticket), the UI must present a confirmation prompt unless explicitly bypassed by a "Trusted Action" user toggle.
3.  **Scope Minimization**: Connectors should request only the minimum required OAuth scopes (e.g., prefer `calendar.readonly` if we only need to view meetings, not edit them).

---

## 6. Implementation Plan / Phases

### Phase 1: Local Foundations & Obsidian
*   Implement `IConnector` interface and `ConnectorManager` singleton.
*   Update `SettingsView` to support a Connectors pane.
*   Implement the **Obsidian Connector** (baseline local filesystem implementation).

### Phase 2: Roster Ingestion (Meet/Calendar)
*   Implement **Google Calendar / Meet Connector** (Read-only OAuth).
*   Integrate with Scribe `SessionManager` to auto-populate meeting metadata based on scheduled times.

### Phase 3: Cloud Export (Notion)
*   Implement **Notion Connector** (Write actions).
*   Add "Export to Notion" capability to the artifact viewer UI.

### Phase 4: Comms & Tools
*   Implement **Gmail** and **Slack** Connectors.
*   Bridge Connectors to the Chat interface via LLM function calling.
