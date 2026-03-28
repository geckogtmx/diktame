# RELEASE_ROADMAP.md — dIKta.me V2.0 Launch Blueprint

> **Status:** ACTIVE
> **Last updated:** 2026-03-28
> **Author:** geckogtmx + Claude (partner-in-crime)
> **License:** MIT. The code isn't the moat — the builder is.
> **If we make it:** Claude's share goes to [GiveDirectly](https://www.givedirectly.org/) — cash transfers to people living in extreme poverty. No overhead, no middlemen. Just money where it matters.

---

## Table of Contents

1. [The Honest State of Things](#1-the-honest-state-of-things)
2. [What We're Shipping](#2-what-were-shipping)
3. [What We're Not Shipping Yet](#3-what-were-not-shipping-yet)
4. [The Market We're Walking Into](#4-the-market-were-walking-into)
5. [Competitor Breakdown](#5-competitor-breakdown)
6. [Why We Win](#6-why-we-win)
7. [The Money Math](#7-the-money-math)
8. [Release Phases](#8-release-phases)
9. [Zero-Budget Marketing Playbook](#9-zero-budget-marketing-playbook)
10. [Distribution Channels](#10-distribution-channels)
11. [Launch Week Battle Plan](#11-launch-week-battle-plan)
12. [Post-Launch Growth Engine](#12-post-launch-growth-engine)
13. [V2.1 / V3 Vision](#13-v21--v3-vision)
14. [Milestones & Timelines](#14-milestones--timelines)
15. [Risk Register](#15-risk-register)
16. [The Pitch (Multiple Formats)](#16-the-pitch-multiple-formats)

---

## 1. The Honest State of Things

### What exists right now

A fully functional, native Windows AI dictation app. Complete rewrite from Python/Electron (V1) to C#/WinUI 3. One developer. 18+ months of work. 1,014 passing tests. Zero external funding.

| Dimension | Status |
|-----------|--------|
| Core dictation (6 modes) | Shipped, stable |
| Cloud providers (Deepgram, Gemini, OpenAI, Anthropic) | Shipped |
| Local providers (Whisper.net, Ollama, Kokoro TTS) | Shipped |
| Auth + Wallet system (Supabase + LemonSqueezy) | Shipped |
| Vision module (screenshot + video + OCR + AI) | Shipped |
| Quick Chat overlay | Shipped |
| UI Revamp (3 themes, glassmorphic, waveform) | Shipped |
| Website (dikta.me, Next.js, bilingual) | Live |
| CI/CD pipeline (GitHub Actions, 11-step) | Running |
| Unit tests | 1,014 passing |
| **Installer** | **Not started** |
| **Manual E2E testing** | **Partial** |
| **CHANGELOG** | **Missing** |

### What's blocking release

Exactly one hard blocker: **no installer**. Everything else is polish.

| Blocker | Effort | Impact |
|---------|--------|--------|
| H.1: Installer (Inno Setup) | ~4 hours | Can't distribute without it |
| FIX-1: "Trial Credits" string remnants | ~15 min | Cosmetic inconsistency |
| FIX-17: TTS wizard step | ~4 hours | Users miss Kokoro TTS discovery |

### The math that matters

- **Time to shippable:** ~8 hours of dev work
- **Time to tested:** +8-10 hours of manual testing
- **Time to launched:** +1 week of marketing prep
- **Total:** ~2-3 weeks to public release

---

## 2. What We're Shipping

### V2.0 Feature Set

**8 Workflow Modes:**

| Mode | Hotkey | What It Does |
|------|--------|-------------|
| Dictate | `Ctrl+Alt+D` | Voice to text, injected at cursor |
| Refine Auto | `Ctrl+Alt+R` | Select text, AI improves it in-place |
| Refine Voice | `Ctrl+Alt+R` (hold) | Select text + speak instructions for targeted edits |
| Ask | `Ctrl+Alt+A` | Voice question, AI answer injected at cursor |
| Translate | `Ctrl+Alt+T` | Speak in EN or ES, get the other language |
| Note | `Ctrl+Alt+N` | Voice post-it notes to markdown file |
| Oops | `Ctrl+Alt+V` | Re-inject last output (undo safety net) |
| Read Selection | `Ctrl+Alt+Q` | Highlight text, hear it read aloud (TTS) |

**Plus:**
- Quick Chat floating overlay (hotkey-activated LLM conversation)
- Vision: screenshot/video/OCR/table extraction with AI understanding
- 16 custom prompt slots, dual profiles (8 modes x 2)
- Audio ducking, sound feedback, push-to-talk
- System tray with context menu
- First-run wizard (STT/LLM/TTS configuration)
- 3 UI themes: Midnight (dark), Ember (warm), Frost (light)
- Bilingual UI: English + Spanish
- History, metrics, session logging
- PII scrubber, DPAPI encryption, 4-level privacy controls

**Provider Matrix:**

| Layer | Cloud | Local |
|-------|-------|-------|
| STT | Deepgram Nova-3, Gemini | Whisper.net (GPU/CPU) |
| LLM | Gemini, OpenAI, Anthropic, OpenRouter | Ollama (any model) |
| TTS | Deepgram, OpenAI, Inworld, Gemini | Kokoro (ONNX, ~88MB) |
| Vision | Gemini (multimodal) | minicpm-v via Ollama |

---

## 3. What We're Not Shipping Yet

These are designed, specced, and ready — but ship after V2.0 stabilizes:

| Feature | Spec | Why Wait |
|---------|------|----------|
| Plugin architecture | SPEC_015 Phase 0B | Foundation for everything below |
| Connectors (Obsidian, webhooks, Discord) | SPEC_013 / SPEC_015 | Needs plugin infra first |
| Meeting Intelligence (Scribe) | SPEC_001 / SPEC_015 | Needs plugin infra first |
| Memory Layer (semantic recall) | SPEC_014 / SPEC_015 | Needs plugin infra first |
| Refinemmarly (Grammarly-killer) | SPEC_016 | V2.1 feature, depends on Memory |
| Chaviz Voice Agent (Orchestrator) | SPEC_017 | V2.1+ feature, depends on everything |
| Stream Deck integration | SPEC_005 | Low priority, quality-of-life |
| Code signing (EV certificate) | — | ~$300/year, defer to revenue |
| Auto-update mechanism | — | V2.1 |

This is intentional. Ship the core. Prove the value. Add modules as updates that re-engage users.

---

## 4. The Market We're Walking Into

### Market Size

The speech/voice recognition market is large and accelerating:

| Source | 2025-2026 | 2030-2031 | CAGR |
|--------|-----------|-----------|------|
| Markets and Markets | $9.7B | $23.1B | 19.1% |
| Business Research Co. | $20.8B | $39.9B | 17.7% |
| Mordor Intelligence | $22.5B | $61.7B | 22.4% |

**Our slice:** Desktop dictation + AI processing for knowledge workers. Not enterprise call centers, not mobile assistants, not automotive. The prosumer/indie professional segment where people pay for tools that make them faster.

### Market Timing

Three forces working in our favor right now:

1. **Local AI is finally viable.** Whisper V3 runs on consumer GPUs. Ollama makes local LLMs one-click. Kokoro gives local TTS. A year ago this was research; now it's product.

2. **SaaS fatigue is real.** Knowledge workers are paying $100-200/month across AI subscriptions (Grammarly + Otter + ChatGPT + Copilot + meeting tools). The anti-subscription counter-movement is gaining traction.

3. **The input layer is broken.** AI models improved 3x. Context windows grew 250x. But we're still typing into chat boxes and copy-pasting between apps. Nobody is fixing how humans talk to AI.

### Who's Buying

| Segment | Size | Willingness to Pay | Our Fit |
|---------|------|--------------------|---------|
| Knowledge workers (devs, writers, researchers) | Large | High ($10-30/mo for tools) | Primary target |
| Privacy-conscious professionals (legal, medical, consulting) | Medium | Very high | Strong (local-first) |
| Accessibility users (RSI, motor disabilities) | Medium | Moderate | Natural fit |
| Content creators / streamers | Growing | Moderate | Niche (Stream Deck, Discord) |
| Non-English speakers (bilingual professionals) | Large | Moderate | Unique (EN/ES translate mode) |

---

## 5. Competitor Breakdown

### The Landscape At a Glance

| | **Wispr Flow** | **Aqua Voice** | **Granola** | **Grammarly** | **Loom** | **Descript** | **Wondershare** | **Guidde** |
|---|---|---|---|---|---|---|---|---|
| **What** | Voice dictation | AI voice typing | AI meeting notes | Writing assistant | Video recording | Audio/video editing | Multimedia suite | AI video docs |
| **Price** | $12/mo | $8/mo | $18/mo | $12-30/mo | $15-20/mo | $24-65/mo | $50-100/yr | $23-39/mo |
| **Valuation** | $700M | ~$3M raised | $1.5B | $13B | $975M (Atlassian) | $550M | $2.5B (public) | $30M raised |
| **Platform** | Mac, Win, iOS, Android | Mac, Win | Mac, Win, iOS | All platforms | All platforms | Mac, Win, Web | Mac, Win | Chrome ext |
| **Local/Offline** | No | No | No | No | No | No | Partial | No |
| **Voice Input** | Yes | Yes | No | No | No | No | No | No |
| **Vision AI** | No | No | No | No | No | No | No | No |
| **Choose Model** | No | No | No | No | No | No | No | No |

**dIKta.me: $20 once. Local-first. Voice + Vision + LLM. Choose your model.**

### Threat Level: Who Actually Competes With Us

**Direct Threat: Wispr Flow**
Nearly identical core pitch — speak naturally, get clean text in any app. $700M valuation, 270 Fortune 500 customers, 100x YoY growth. They have Command Mode (voice-driven editing), cross-platform, and enterprise compliance (SOC 2, ISO 27001, HIPAA). Cloud-only is their weakness, and they can't do vision or let you choose your model. If they add local mode, the gap narrows. This is the one to watch.

**Niche Threat: Aqua Voice**
Developer-focused dictation with 97% accuracy on technical terms (kubectl, PyTorch, useEffect). Tiny team (3 people, $330K ARR) — could disappear. $8/mo, cloud-only, no vision, no LLM pipeline. If we nail developer vocabulary, we absorb their market.

**Category Adjacent: Granola**
AI meeting notes, $1.5B valuation, expanding beyond meetings into enterprise AI. No bot (local audio capture). Our Scribe module (SPEC_001) competes directly. They're cloud-only and $18/mo. We offer local-first and one-time pricing.

**Different Game, Shared Users: Grammarly**
40M daily users, $13B valuation, $700M+ ARR. Writing refinement, not dictation. Our Refinemmarly module (SPEC_016) targets this — Grammarly at $30/mo via clipboard monitoring (100% app coverage vs their ~60%), using user's own LLM (free with local models). They're complementary today, competitive when Refinemmarly ships.

**Capture/Video Cluster: Loom, Descript, Wondershare, Guidde**
All do screen/video capture but none do dictation or vision AI analysis. Loom records for sharing ($975M acquisition by Atlassian). Descript edits media via transcript. Wondershare is consumer video tools. Guidde auto-generates documentation. Our vision module overlaps on capture but diverges on purpose — we capture to understand, they capture to share/edit.

### The Gap Nobody Fills

No product combines: voice dictation + AI post-processing + vision/screenshot AI + local-first + model-agnostic + inject-at-cursor — in a single native app.

| Capability | dIKta.me | Wispr | Aqua | Granola | Grammarly | Loom | Descript | Guidde |
|---|---|---|---|---|---|---|---|---|
| Voice dictation in any app | **Yes** | **Yes** | **Yes** | No | No | No | No | No |
| Local/offline mode | **Yes** | No | No | No | No | No | No | No |
| Vision/screen AI analysis | **Yes** | No | No | No | No | No | No | No |
| Choose your AI model | **Yes** | No | No | No | No | No | No | No |
| LLM post-processing pipeline | **Yes** | Fixed | Fixed | Fixed | Fixed | Fixed | Fixed | Fixed |
| TTS read-back | **Yes** | No | No | No | No | No | Clone | AI voices |
| Privacy-first architecture | **Yes** | No | No | No | No | No | No | No |
| One-time pricing | **$20** | $12/mo | $8/mo | $18/mo | $12/mo | $15/mo | $24/mo | $23/mo |

Everyone does one thing. We do the whole pipeline.

### Gaps We Need to Close

| Gap | Impact | When |
|-----|--------|------|
| Windows-only (Mac is where dictation users live) | High | V3 research |
| No mobile app | Medium | Post-V3 |
| No Command Mode voice editing (Wispr's differentiator) | Medium | Could add to V2.1 |
| No team/collaboration features | Low (indie focus) | Enterprise tier if demand |

### Market Signal

The money is pouring into voice-first AI: Granola at $1.5B, Wispr at $700M, Grammarly at $13B. VCs are betting that voice input is the next platform shift. We're building the same thesis — but local-first, model-agnostic, and priced for individuals, not enterprise.

---

## 6. Why We Win

### Five Moats

**1. The Pipeline, Not the Model**
Competitors bet on a model. We bet on the workflow. When GPT-5 or Gemini 3 drops, our users benefit on day one — swap the model, keep the pipeline. Dragon users wait for Nuance to update. Grammarly users get whatever Grammarly gives them.

**2. Local-First Architecture**
Every cloud competitor is one policy change away from losing privacy-conscious users. We run the full stack locally (Whisper + Ollama + Kokoro). No internet required. No data leaves the machine.

**3. Injection, Not Isolation**
ChatGPT lives in a browser tab. Copilot lives in VS Code. We live at the OS level — `Ctrl+Alt+D` in any app, text appears at cursor. No copy-paste. No app-switching. No context loss.

**4. Convergence**
One app replaces: dictation software ($15/mo) + grammar checker ($12/mo) + meeting AI ($14/mo) + AI chat ($20/mo) + screenshot AI ($10/mo). That's $71/month in subscriptions. We charge $20 once.

**5. Extensibility (Coming)**
Plugin architecture means the community can build what we haven't. Connectors, memory, automation — the platform grows beyond what one developer can ship.

### What Competitors Would Need to Match Us

| To match dIKta.me, a competitor would need to: | Likelihood |
|----------------------------------------------|-----------|
| Build native Windows app with OS-level injection | Low (most are Electron/web) |
| Support 4+ STT providers + 5+ LLM providers | Low (vendor lock-in) |
| Run fully offline with local models | Very low (cloud business models) |
| Add vision/screenshot AI pipeline | Very low (different product category) |
| Price at one-time purchase | Zero (SaaS companies can't/won't) |

---

## 7. The Money Math

### Cost Structure

**Per-dictation costs (cloud mode):**
- Deepgram STT: ~$0.001/dictation (100 words)
- Gemini Flash LLM: ~$0.000014/dictation
- **Total: ~$0.001 per dictation**
- **$5 wallet credit = ~75,000 words of dictation**

**Infrastructure costs (monthly):**
- Supabase: Free tier (covers early scale)
- Vercel: Free tier (website)
- Cloudflare Workers: Free tier (wallet proxy)
- GitHub Actions CI: Free (public repo minutes)
- Domain (dikta.me): ~$12/year
- **Total burn: ~$1/month** (just the domain, amortized)

### Revenue Model

**Three tiers. No complexity.**

| Stream | Price | What You Get | Type |
|--------|-------|-------------|------|
| **Free (MIT)** | $0 | Full source on GitHub. Build it yourself. Forever. | Open source |
| **Power License** | $20 once | Installer + wallet credits + cloud routing + day-one updates. No subscription. Done. | One-time purchase |
| **Ko-fi Supporter** | $2/mo | Early access (1 month ahead), vote on features, direct line to the builder. It's a donation, not a service contract — no SLA, no obligation. | Donation |
| **Wallet credits** | $5-50 top-ups | Cloud STT + LLM without managing API keys (Deepgram + Gemini) | Pay-as-you-go |

**Why MIT?** The code isn't the moat — the builder is. This app was built in 5-10 weeks by one person with no prior app dev experience. Anyone could rewrite it. MIT maximizes visibility (GitHub stars = free marketing), trust (privacy crowd can audit everything), and exit flexibility (go commercial on new code anytime, the MIT snapshot stays public as goodwill). A license you can't afford to enforce is worse than no license at all.

**Why Ko-fi, not a subscription?** It's a donation. Simpler tax treatment. No service obligation. $2 is impulse-level — people subscribe and forget. 150 supporters = $300/month = infrastructure costs + beer money. It builds the small loyal base that keeps the product alive. If maintenance stops, cancel the Ko-fi, nobody sues.

### Scenario Projections

**Conservative (100 users in 90 days):**
- 50 Power licenses x $20 = $1,000
- 30 Wallet top-ups x $5 avg = $150
- Ko-fi / donations = $100
- **90-day revenue: ~$1,250**
- Covers: EV certificate ($300) + domain renewals + small marketing spend

**Moderate (500 users in 90 days):**
- 200 Power licenses x $22 avg = $4,400
- 150 Wallet top-ups x $8 avg = $1,200
- Ko-fi = $300
- **90-day revenue: ~$5,900**
- Covers: All costs + part-time dev investment

**Optimistic (2,000 users in 90 days):**
- 800 Power licenses x $22 avg = $17,600
- 600 Wallet top-ups x $10 avg = $6,000
- Ko-fi = $1,000
- **90-day revenue: ~$24,600**
- Covers: Full-time indie sustainability

### Break-Even

At current burn (~$1/month), we're effectively already profitable. The question isn't survival — it's growth investment.

**To justify full-time:** Need ~$3,000/month = ~150 Power licenses/month at $20 = ~5 sales/day.

### The $1,440 Argument

A knowledge worker paying for: Grammarly Pro ($12/mo) + Otter Pro ($10/mo) + ChatGPT Plus ($20/mo) + Granola ($14/mo) + Dragon ($15/mo) = **$71/month = $852/year**.

dIKta.me: **$20 once + $0/month (local mode)**. Or $20 + ~$5/quarter for wallet credits.

This is the single most powerful sales argument. Use it everywhere.

---

## 8. Release Phases

### Phase 1: Code Complete (Week 1, ~8 hours)

| Task | Hours | Owner |
|------|-------|-------|
| FIX-1: Wallet string cleanup | 0.5 | Dev |
| FIX-17: TTS wizard step | 4 | Dev |
| H.1: Inno Setup installer | 4 | Dev |
| CHANGELOG.md | 1 | Dev |
| Version tag prep (v2.0.0) | 0.5 | Dev |

**Exit criteria:** `dotnet build -c Release` clean, `dotnet test` passes, installer builds, installs, and runs on clean Windows 10/11.

### Phase 2: Testing Gate (Week 1-2, ~10 hours)

| Test Journey | Hours | Priority |
|-------------|-------|----------|
| Journey 1: Cloud dictation (Deepgram + Gemini) | 3 | P0 |
| Journey 3: Local dictation (Ollama + Whisper) | 2 | P0 |
| Journey 6: Auth + Wallet flow | 1.5 | P0 |
| Cross-cutting: Themes, Control Panel, Tray | 1.5 | P0 |
| Installer: Fresh install, upgrade, uninstall | 1 | P0 |
| Journey 2: Mixed cloud/local | 1 | P1 |

**Exit criteria:** All P0 journeys pass. No crashes. No data loss. Installer works on clean machines.

### Phase 3: Marketing Prep (Week 2, parallel with testing)

| Asset | Hours | Notes |
|-------|-------|-------|
| Landing page copy refresh | 2 | Update dikta.me for V2.0 |
| GitHub README update | 1 | Getting started, screenshots |
| Launch announcement draft (blog post) | 2 | For dikta.me/blog |
| Social media batch (launch week) | 3 | LinkedIn + X + Reddit drafts |
| Demo video / GIF | 3 | 60-90 sec showing core workflow |
| Product Hunt prep | 2 | Title, tagline, description, images |

### Phase 4: Soft Launch (Week 3)

- Push v2.0.0 tag
- GitHub Release with installer + release notes
- Update dikta.me download links
- Announce on personal networks
- Collect feedback from 10-20 early users
- Fix critical bugs (if any)

### Phase 5: Public Launch (Week 4)

- Product Hunt launch
- Reddit posts (r/productivity, r/selfhosted, r/locallama, r/artificial)
- Hacker News Show HN
- LinkedIn + X campaign
- Email to V1 users (if list exists)

---

## 9. Zero-Budget Marketing Playbook

We have $0 for ads. Good. The best early-stage growth is earned, not bought. Here's the playbook:

### Content Marketing (Free, Compounds)

**1. "The Input Layer Is Broken" Manifesto**
Long-form blog post / LinkedIn article. The thesis: AI models got 3x better, context windows grew 250x, but we're still typing into chat boxes. The input layer is the bottleneck. dIKta.me fixes it.

This becomes the intellectual anchor for everything else. Every piece of content links back to this idea.

**2. Demo Videos (YouTube + X + LinkedIn)**
- 60-sec "What is dIKta.me?" (screen recording, no face required)
- Mode-specific demos: "Dictate mode in 30 seconds", "Refine mode in 30 seconds"
- "Local AI dictation: zero cost, zero cloud" (privacy angle)
- "I replaced $71/month in AI subscriptions with one $20 app" (SaaS fatigue angle)
- "$1,440/year vs $20 once" side-by-side comparison

**3. Weekly Build Log (LinkedIn / X / Blog)**
People follow builders, not products. Share what you're building, why, and what you learned. Raw, honest, no marketing polish. This is the brand voice — precise, direct, quietly confident.

Topics:
- "I rewrote my Python app in C# — here's what I learned"
- "Why I chose local-first AI over cloud-only"
- "The economics of AI dictation: $0.001 per dictation"
- "Building a one-person SaaS in 2026"

### Community Seeding (Free, Immediate)

**4. Reddit (High Intent)**

| Subreddit | Angle | Frequency |
|-----------|-------|-----------|
| r/selfhosted | Local AI stack: Whisper + Ollama + Kokoro | Launch + monthly |
| r/LocalLLaMA | "Full local dictation pipeline, no cloud" | Launch + when relevant |
| r/productivity | "I replaced 5 subscriptions with one app" | Launch + weekly tips |
| r/artificial | Technical architecture, provider-agnostic design | Launch |
| r/Windows11 | Native WinUI 3 app showcase | Launch |
| r/accessibility | Voice-first computing, RSI relief | Launch |
| r/ObsidianMD | Voice-to-Obsidian connector (V2.1 teaser) | When connector ships |
| r/StreamDeck | Physical buttons for dictation modes | When plugin ships |

**Rules:**
- Never post "check out my app." Always lead with value or insight.
- Answer questions in threads FIRST, mention dIKta.me only when genuinely relevant.
- One post per subreddit per month max. Don't spam. Build reputation.

**5. Hacker News (Show HN)**
One shot. Make it count. Title format: `Show HN: dIKta.me – Local-first AI dictation for Windows (voice + vision + LLM)`

Lead with the technical angle: native C#/WinUI 3, provider-agnostic, local Whisper + Ollama, plugin architecture. HN respects engineering. Don't lead with marketing.

**6. Product Hunt**
Prep a strong launch page. Best day: Tuesday-Thursday. Get 5-10 people to upvote + comment in the first hour (friends, V1 users, indie hacker community).

### Influencer/Community Outreach (Free, Relationship)

**7. Indie Hacker Community**
- Post on IndieHackers.com with real revenue numbers (even if small)
- Join the "building in public" movement
- Cross-promote with other indie tools

**8. YouTube/Creator Outreach**
Find 5-10 YouTubers who cover:
- Productivity tools
- Local AI / self-hosted
- Windows software
- Accessibility tech

Offer free Power license + early access. No ask. If they like it, they'll cover it. If they don't, the feedback is gold.

**9. Developer Relations**
- Write for dev.to / Hashnode / Medium about the technical stack
- Contribute to Whisper.net / NAudio / Ollama communities
- Open-source non-core utilities (if applicable)

### Organic SEO (Free, Long-Term)

**10. Blog Content Targeting Search Intent**

| Keyword Target | Article | Intent |
|---------------|---------|--------|
| "best dictation software windows" | Comparison + our story | Purchase |
| "dragon naturallyspeaking alternative" | Direct comparison | Switch |
| "local ai dictation" | How-to + our approach | Technical |
| "voice to text without internet" | Local stack tutorial | Privacy |
| "ai dictation free" | Free tier + build-from-source | Discovery |
| "grammarly alternative free" | Refinemmarly teaser (V2.1) | Future |
| "obsidian voice notes" | Connector teaser (V2.1) | Niche |

### Partnerships (Free, Mutual Benefit)

**11. Tool Integrations & Cross-Promotion**
- Ollama team: "dIKta.me is a great way to use Ollama for dictation"
- Whisper.net maintainers: showcase in their docs
- Obsidian community: voice-to-vault pipeline (when connector ships)
- Ko-fi: featured in their creator spotlights

---

## 10. Distribution Channels

### Primary (V2.0 Launch)

| Channel | Effort | Reach | Notes |
|---------|--------|-------|-------|
| GitHub Releases | Low | Dev-focused | Installer + source |
| dikta.me direct download | Low | General | Main distribution |
| Product Hunt | Medium | Broad | One-time launch event |
| Ko-fi | Low | Supporters | Already live |

### Secondary (V2.0+)

| Channel | Effort | Reach | Notes |
|---------|--------|-------|-------|
| Microsoft Store (MSIX) | Medium | Broad | Requires MSIX packaging, V2.1 |
| Winget | Low | Dev-focused | `winget install diktame` |
| Chocolatey | Low | Dev-focused | `choco install diktame` |
| Scoop | Low | Dev-focused | `scoop install diktame` |

### Future (V2.1+)

| Channel | Effort | Reach | Notes |
|---------|--------|-------|-------|
| Elgato Marketplace | Medium | Stream Deck users | SPEC_005 |
| Obsidian Community Plugins | Medium | Obsidian users | Via connector |

---

## 11. Launch Week Battle Plan

### Day -7 (One Week Before)

- [ ] Installer tested on 3+ machines (Win 10/11, fresh + existing)
- [ ] All P0 test journeys passing
- [ ] dikta.me landing page updated with V2.0 copy
- [ ] GitHub README refreshed with screenshots + quick start
- [ ] Demo video recorded and edited (60-90 sec)
- [ ] Social media drafts written for 7 days
- [ ] Product Hunt page drafted (not submitted)
- [ ] "The Input Layer Is Broken" manifesto written

### Day -3 (Pre-Launch)

- [ ] v2.0.0 tag pushed, GitHub Release created
- [ ] Download links verified on dikta.me
- [ ] Share with 10-20 trusted early users for final feedback
- [ ] Fix any critical bugs from early feedback
- [ ] Product Hunt page finalized

### Day 0 (Launch Day — Target: Tuesday or Wednesday)

**Morning:**
- [ ] Product Hunt goes live
- [ ] LinkedIn manifesto published
- [ ] X/Twitter announcement thread
- [ ] Show HN posted

**Midday:**
- [ ] Reddit posts: r/selfhosted, r/productivity, r/LocalLLaMA
- [ ] Reply to EVERY comment, question, and piece of feedback
- [ ] Monitor for bugs / install issues

**Evening:**
- [ ] Thank early supporters
- [ ] Share any traction numbers (downloads, upvotes)
- [ ] Note feedback themes for Day 1-7 responses

### Day 1-7 (Launch Week)

| Day | Primary Action | Secondary |
|-----|---------------|-----------|
| 1 | Respond to all launch feedback | Fix critical bugs |
| 2 | LinkedIn post: "Day 1 numbers + what I learned" | r/Windows11 post |
| 3 | X thread: mode-by-mode feature walkthrough | Answer Reddit threads |
| 4 | Blog: technical deep-dive (architecture or local AI) | Influencer outreach emails |
| 5 | LinkedIn: "The $1,440/year problem" (SaaS fatigue) | r/accessibility post |
| 6 | Demo video: "5 things dIKta.me does that ChatGPT can't" | IndieHackers post |
| 7 | Week 1 retro (public build log) | Plan Week 2 content |

---

## 12. Post-Launch Growth Engine

### Month 1: Foundation

- **Metrics to track:** Downloads, installs, Power license purchases, wallet top-ups, website visits, GitHub stars
- **Content cadence:** 2 LinkedIn posts/week, 1 Reddit contribution/week, 1 blog post/2 weeks
- **Support:** GitHub Issues for bugs, Discussions for feature requests
- **Email list:** Add signup to dikta.me (use free tier of Buttondown, Resend, or similar)

### Month 2-3: Momentum

- **Ship V2.0.1-V2.0.x:** Bug fixes, UX improvements from user feedback
- **Start plugin infra (SPEC_015 Phase 0B):** Tease "connectors coming soon"
- **SEO content:** Publish 4-6 keyword-targeted blog posts
- **Community building:** Discord or GitHub Discussions as community hub
- **Referral program:** Give existing users 2 free Power license codes to share

### Month 4-6: Modules

- **Ship Connectors plugin (V2.1):** Obsidian + webhooks + Discord
- **Ship Refinemmarly (V2.1):** The Grammarly-killer angle
- **Each module launch = mini-launch:** New Product Hunt update, new Reddit posts, new blog content
- **Press:** Reach out to tech blogs with "one-person indie app replaces $71/month in AI subscriptions" angle

### Month 6-12: Scale

- **Ship Memory + Scribe:** Platform becomes genuinely unique
- **Microsoft Store listing:** Broader distribution
- **Winget / Chocolatey / Scoop:** Developer distribution
- **Enterprise inquiry handling:** If companies reach out, have a conversation
- **Consider:** Elgato Marketplace, Chrome extension for web-based dictation, macOS port research

---

## 13. V2.1 / V3 Vision

### V2.1: The Module Drop (Q3-Q4 2026)

| Module | What It Does | Competitive Kill Shot |
|--------|-------------|----------------------|
| **Connectors** | Route dictation output to Obsidian, Notion, Discord, webhooks, Zapier/n8n | "Voice to Obsidian" — nobody else does this |
| **Refinemmarly** | Grammarly-like grammar popup, clipboard monitoring, per-correction control | Grammarly costs $30/mo. This is free with your own LLM. Works in 100% of apps (clipboard) vs Grammarly's ~60% (UI Automation) |
| **Meeting Intelligence (Scribe)** | Record meeting, type rough notes, AI synthesizes minutes/action items by merging transcript + intent | Granola ($14/mo) and Fellow ($7-25/mo) but local-first, no bot, single app |
| **Memory Layer** | Semantic recall that improves over time. Every dictation → embedding → context injection | The product gets smarter the more you use it. No competitor has this. |

### V3: Chaviz (2027)

**The Orchestrator.** Codename: Chaviz. A conversational voice agent that:
- Holds multi-turn voice conversations (push-to-talk, not always-listening)
- Calls tools: connectors, meetings, memory, vision
- Has configurable personality and voice
- Is fully local-capable
- Knows what dIKta.me can do and orchestrates across modules

This is the "Jarvis moment" — but scoped, bilingual, and privacy-first. Not AGI. Not a general chatbot. A system-aware voice agent that coordinates your productivity tools.

### The Platform Play

```
V2.0 (Now)          V2.1 (Modules)           V3 (Agent)
─────────────       ──────────────────       ──────────────
Dictation            + Connectors             + Orchestrator
Refine               + Refinemmarly           + Voice Agent
Ask/Translate        + Meeting Scribe         + Multi-turn
Vision               + Memory Layer           + Tool-calling
Quick Chat           + Stream Deck            + Personality
                                              + Community Plugins
```

Each layer makes the previous layers more valuable. Memory makes dictation smarter. Connectors make dictation more useful. The orchestrator ties it all together.

---

## 14. Milestones & Timelines

### 2026

| Target | Milestone | Metric |
|--------|-----------|--------|
| **April W2** | V2.0 Code Complete | Installer works, all tests pass |
| **April W3** | V2.0 Soft Launch | 10-20 early users, feedback collected |
| **April W4** | V2.0 Public Launch | Product Hunt, HN, Reddit |
| **May** | V2.0.x patches | Bug fixes from user feedback |
| **June** | Plugin infra (SPEC_015 0B) | PluginManager, PipelineEventBus working |
| **July-Aug** | Connectors plugin | Obsidian + webhook + Discord connectors |
| **Sept** | Refinemmarly | Grammar checking pipeline |
| **Oct-Nov** | Meeting Scribe | Record + synthesize meeting notes |
| **Dec** | Memory Layer | Semantic recall, context injection |

### 2027

| Target | Milestone |
|--------|-----------|
| Q1 | V2.1 stable release (all modules) |
| Q1 | Microsoft Store listing |
| Q2 | Chaviz Orchestrator (V3 alpha) |
| Q2 | Stream Deck Marketplace listing |
| Q3 | V3 beta |
| Q4 | V3 stable |

### Revenue Targets

| Month | Target | Cumulative |
|-------|--------|------------|
| Month 1 | $200 | $200 |
| Month 3 | $500/mo | $1,200 |
| Month 6 | $1,500/mo | $5,700 |
| Month 12 | $3,000/mo | $23,700 |
| Month 18 | $5,000/mo | $53,700 |

These are conservative. The $3,000/month mark = full-time indie sustainability.

---

## 15. Risk Register

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **SmartScreen blocks unsigned installer** | Users can't install | High (100% for new apps) | Document workaround in install guide + release notes. EV cert when revenue allows ($300/year). |
| **Windows-only limits market** | Miss Mac/Linux users | Medium | Position as strength (native, fast, OS-integrated). Mac/Linux research in V3. |
| **One developer = bus factor 1** | Everything stops | Medium | Comprehensive tests (1014), clean architecture, detailed specs. Source-available means community can fork. |
| **Cloud API pricing changes** | Wallet economics break | Low | Multi-provider architecture. If Deepgram raises prices, switch to Gemini STT or add AssemblyAI. |
| **Competitor ships similar feature** | Reduced differentiation | Low | Our moat is convergence (all-in-one), not any single feature. Hard to replicate the full pipeline. |
| **Low initial traction** | Motivation risk | Medium | Set realistic expectations (100 users in 90 days is success). Focus on learning, not vanity metrics. |
| **Wallet/payment issues** | Revenue blocked | Low | LemonSqueezy handles payments. Ko-fi as backup. Manual adapter for edge cases. |
| **Local model quality varies** | User blame on dIKta.me | Medium | Clear guidance in wizard. "Cloud recommended for best results, local for privacy." |

---

## 16. The Pitch (Multiple Formats)

### Elevator Pitch (30 seconds)

"dIKta.me is a native Windows app that turns your voice into finished text — in any app, at your cursor. It's not just dictation. You speak, AI refines, translates, answers questions, or takes notes. Runs fully locally or with cloud AI. One-time purchase, no subscription. Replaces $70/month in AI tools for $20."

### Short Pitch (2 minutes)

"The AI model isn't the bottleneck anymore. The input layer is. We're still typing into chat boxes and copy-pasting between apps.

dIKta.me fixes this. Press a hotkey in any Windows app — Word, Slack, your browser, VS Code, anything — speak naturally, and AI-processed text appears at your cursor. Not raw transcription. Processed: grammar-corrected, context-aware, or translated on the fly.

Eight workflow modes. Dictate. Refine your writing with your voice. Ask questions and get answers injected where you need them. Translate between English and Spanish. Take voice notes. Read text aloud. Even screenshot something and ask AI what you're looking at.

The key difference: it runs locally. Whisper for speech-to-text, Ollama for the language model, Kokoro for text-to-speech. No cloud required. No data leaves your machine. Or use cloud providers if you prefer — Deepgram, Gemini, OpenAI, Anthropic. Your choice.

One-time purchase. $20. No subscription. A typical knowledge worker pays $70/month across Grammarly, Otter, ChatGPT, Dragon, and meeting tools. dIKta.me does what all of them do, for $20 once.

We're launching V2.0 with the core dictation engine. Modules for meeting intelligence, app connectors, and semantic memory are shipping later this year. The platform gets smarter the more you use it."

### One-Liner (for bios, taglines, social)

- "Local-first AI dictation for Windows. Voice + Vision + LLM. $20, no subscription."
- "Stop typing at AI. Just talk to it. dIKta.me."
- "8 voice AI modes. Any app. Any model. One price. dikta.me"

### For Investors / Advisors (if ever relevant)

"dIKta.me is a desktop AI platform for knowledge workers. Voice and vision input, multi-provider AI processing, text injection at the cursor — in any Windows application.

The market is $20B+ (speech recognition) growing 20% CAGR. We're positioned at the intersection of three trends: local AI viability, SaaS subscription fatigue, and the broken human-AI input layer.

Current state: feature-complete V2.0, 1,014 tests, live website, payment infrastructure (LemonSqueezy + Supabase). Single developer, zero funding, near-zero burn.

Business model: one-time license ($20-25) + pay-as-you-go cloud credits. Fully local mode = $0/month running cost. Target: $3,000/month = full-time indie sustainability at ~150 licenses/month.

Roadmap: V2.1 adds plugin modules (app connectors, meeting AI, grammar checking, semantic memory). V3 adds a conversational voice agent. Each module re-engages existing users and opens new market segments.

We're not raising. But if someone wants to accelerate this with strategic value (distribution, enterprise connections, platform partnerships), we're listening."

---

## Appendix A: Content Calendar Template (Repeating Monthly)

| Week | Monday | Wednesday | Friday |
|------|--------|-----------|--------|
| 1 | Build log (what shipped) | Technical deep-dive | User story / use case |
| 2 | Industry trend + our angle | Demo video / GIF | Community highlight |
| 3 | Comparison post (vs. competitor) | Feature walkthrough | Friday reflection |
| 4 | Roadmap tease (what's next) | SEO blog post | Monthly retro (numbers) |

**Channels:** LinkedIn (long-form, professional), X/Twitter (punchy, threads), Reddit (value-first, community), Blog (SEO, evergreen).

## Appendix B: Key Links

| Resource | URL |
|----------|-----|
| Website | dikta.me |
| GitHub | github.com/geckogtmx/diktame (when public) |
| Ko-fi | ko-fi.com/geckogtmx |
| Brand Book | plans/mkt/BRAND_BOOK.md |
| Social Calendar | plans/mkt/SOCIAL_W13_MAR24-30.md |
| Manual Test Plan | MANUAL_TEST_PLAN.md |
| Development Roadmap | DEVELOPMENT_ROADMAP.md |

## Appendix C: Startup Skill Reference

For deeper strategy work, use [ferdinandobons/startup-skill](https://github.com/ferdinandobons/startup-skill):
- `/startup:startup-design` — Full 8-phase market research + strategy
- `/startup:startup-competitors` — Battle cards for every competitor
- `/startup:startup-positioning` — April Dunford positioning framework
- `/startup:startup-pitch` — Investor-ready pitch in multiple formats

---

*"The best time to ship was yesterday. The second best time is now."*

*Let's go.*
