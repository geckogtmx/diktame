# DIKTAX 3-6 BLUEPRINT

**The 3-6 month execution playbook for dIKta.me V2's pivot from feature-paywall to free-forever + Insider supporter model.**

- **Author**: Eduardo García-Torres Resano (geckogtmx)
- **Created**: 2026-04-26
- **Status**: Living document — checkpoints at Day 30 / 60 / 90 / 180 update it in place
- **Companion docs**: `DEVELOPMENT_ROADMAP.md`, `MANUAL_TEST_PLAN.md`, `MANUAL_TEST_LOG.md`, `ARCHITECTURE.md`, `TESTING.md`
- **Decision log**: see Appendix D for the four-turn debate that produced this plan

---

## Table of Contents

0. [Heritage Assets — What Pre-Pivot Marketing Materials Are Reusable](#0-heritage-assets--what-pre-pivot-marketing-materials-are-reusable)
1. [The Pivot — Why & What Changed](#1-the-pivot--why--what-changed)
2. [Strategic Frame — Fit-to-Market Experiment, Not Startup](#2-strategic-frame--fit-to-market-experiment-not-startup)
3. [The Two-Lane Model — Free Forever + Insider License](#3-the-two-lane-model--free-forever--insider-license)
4. [Insider Tier — What $20 Buys](#4-insider-tier--what-20-buys)
5. [Wallet — Orthogonal Product Positioning](#5-wallet--orthogonal-product-positioning)
6. [Plugin Architecture — Months 2-4 Roadmap Inside Insider](#6-plugin-architecture--months-2-4-roadmap-inside-insider)
7. [Distribution Strategy — Store + GitHub Parallel](#7-distribution-strategy--store--github-parallel)
8. [Code Adjustments — Open Up the App](#8-code-adjustments--open-up-the-app)
9. [Website & Funnel Adjustments](#9-website--funnel-adjustments)
10. [Documentation & User-Facing Copy](#10-documentation--user-facing-copy)
11. [Open-Source Lockdown — Source-Available Posture](#11-open-source-lockdown--source-available-posture)
12. [Pre-Launch Checklist — 1-2 Weeks](#12-pre-launch-checklist--1-2-weeks)
13. [Manual Test Plan Integration — Gate to Store Submission](#13-manual-test-plan-integration--gate-to-store-submission)
14. [Launch Sequence — Day 0 to Day 14](#14-launch-sequence--day-0-to-day-14)
15. [The 6-Month Experiment — KPIs & Checkpoints](#15-the-6-month-experiment--kpis--checkpoints)
16. [Founding Insiders & Advocacy Mechanics](#16-founding-insiders--advocacy-mechanics)
17. [Risk Register & Mitigations](#17-risk-register--mitigations)
18. [Exit Strategy — What "Done" Looks Like](#18-exit-strategy--what-done-looks-like)
19. [Appendix A — File-by-File Change Inventory](#appendix-a--file-by-file-change-inventory)
20. [Appendix B — Website Component Inventory](#appendix-b--website-component-inventory)
21. [Appendix C — Manual Test Plan Re-Keying Diff](#appendix-c--manual-test-plan-re-keying-diff)
22. [Appendix D — Decision Log](#appendix-d--decision-log)

---

## 0. Heritage Assets — What Pre-Pivot Marketing Materials Are Reusable

Before the pivot, several marketing assets were drafted for the old "$20 Power License unlocks BYOK + Local" model and a Product Hunt launch scheduled for **April 15, 2026 (which did not happen)**. These have been audited (2026-04-26) — most contain load-bearing strategy, voice, and execution mechanics that survive the model change with **commercial wording rekey only**. The blueprint folds the salvageable parts into the relevant sections below.

Each source doc has been **marked at the top with a stale-banner** pointing back to this blueprint, so future readers don't accidentally treat outdated pricing or dates as canon.

### Canonical references — keep authoritative, do not duplicate

| Doc | Status | Why it stays canonical |
|-----|--------|------------------------|
| `plans/mkt/BRAND_BOOK.md` | ✅ **Canonical voice + visual identity** — no rekey needed | Brand orange `#C55A28`, Inter typography, Midnight palette, "dIKta.me" capitalization rule, taglines, "what we are NOT" guardrails, voice rules ("short declarative," "no marketing fluff," "honest about limitations"). Every piece of copy generated under §9 (website), §10 (docs), and §14 (launch posts) MUST pass these rules. |
| `plans/mkt/BLOG_ROADMAP.md` | ✅ Architecture current | Bilingual literary blog system (already mostly built per memory: Supabase `blog_posts` table, `news-run`/`news-writer`/`news-publish` skills, `/hqbackstage/blog` admin). The inaugural blog post (§9.C) goes through this exact pipeline. |
| `plans/mkt/Carlos Fuentes Series.md` | ✅ Blog content (no pricing refs) | Legacy curated literary series. Independent of pricing model. Keep as blog editorial reference. |

### Salvaged with rekey — pulled into blueprint sections

| Doc | What was pulled | Where it lands | Rekey required |
|-----|-----------------|----------------|----------------|
| `plans/mkt/PH_LAUNCH_PLAN.md` | **Windows-native gap insight** (Mac dominates PH AI dictation: Wispr 2,095, AudioPen 873, Aqua 585) | §2 (Strategic Frame) | None — the insight predates pricing |
| `plans/mkt/PH_LAUNCH_PLAN.md` | **PH algorithm research** (10% featured rate, 1 quality comment ≈ 40-50 upvotes, 12:01 AM PST critical window, established-account weighting) | §14 Day 0 mechanics | None |
| `plans/mkt/PH_LAUNCH_PLAN.md` | **Multi-launch strategy** (4 PH launches over 8 months, different category each time) | §15/§16 Day 90+ playbook | "$20 one-time" → "free + $20+ Insider supporter" |
| `plans/mkt/LAUNCH_WEEK_TIMELINE.md` | **Day-by-day cadence + channel priority matrix** (PH > HN > LinkedIn > X > Reddit > YouTube > IndieHackers > dev.to) | §14 launch sequence | "Full Versions sold" KPI → "Insider licenses sold" |
| `plans/mkt/LAUNCH_WEEK_TIMELINE.md` | **Success metric tiers** (conservative/moderate/strong) | §15 KPIs | "Full Versions sold" → "Insider licenses sold" |
| `plans/mkt/COMPETITOR_PAGES.md` | **6 ready-to-publish comparison pages** (vs Wispr Flow, vs Aqua Voice, Wispr alternatives, Dragon alternatives, vs Loom, screenshot AI alternatives, vs Grammarly) | §9.E themed landings as companion `/vs/<competitor>` content | Every "$20 once / Full Version / early bird" → "free forever + $20+ Insider supporter" |
| `plans/mkt/COMPETITOR_PAGES.md` | **Centralized competitor data** (Wispr, Aqua, Dragon, Grammarly, Granola, Loom, Descript, Guidde, CleanShot X) | §17 Risk register + §9.E content | Same |
| `plans/mkt/FACE_INSTA.md` | **Daily content engine** (15 min/day blog→FB/IG repurposing) | §14 Days 4-7+ sustainable cadence | Same wording sweep |
| `plans/mkt/FACE_INSTA.md` | **News-to-dIKta.me bridge table** + hook bank (EN + ES) | §14 + §9.C inaugural post toolkit | Same |
| `plans/mkt/FACE_INSTA.md` | **Visual style ref + tools list** (Meta Business Suite, Canva, OBS/ShareX, CapCut) | §14 launch operations | None |
| `plans/mkt/SOCIAL_HAI_POST.md` | **HAI ("input layer is broken") angle** — EN + ES LinkedIn + X versions | §9.C inaugural blog post + §14 launch posts | None — voice angle survives |
| `plans/mkt/SOCIAL_W13_MAR24-30.md` | **News-hook → product-pivot pattern** + bilingual templates + "Stop Renting Software" anti-sub angle | §14 daily cadence | "$20 once" → new framing |
| `plans/mkt/LAUNCH_CONTENT.md` | **"Input Layer Is Broken" manifesto** (§1, ~900-1000 words EN + ES) — pulls into the inaugural blog post | §9.C draft basis | Pricing math sections need rekey |
| `plans/mkt/LAUNCH_CONTENT.md` | **Feature post library** (§8 — one ready post per mode: Dictate, Refine, Vision, Translate, etc.) | §14 ongoing social cadence | Same |
| `plans/mkt/LAUNCH_CONTENT.md` | **Subscription comparison hooks** ("$852/yr in subs vs $20") | §14 + §9 wording sweep | Reframes from "$20 to unlock" to "$20 to fund the build" |
| `plans/mkt/mem_review.md` | **Mem.ai competitive analysis** | §9.E (potential additional comparison page) + §17 risk awareness | Same |

### Discarded — do not reference

- April 15, 2026 launch date (passed; new launch window per §12)
- "T-7 / T-8 week pre-launch" structure (we're on a 1-2 week timeline now)
- "V2.0 launch" framing (we're at V2.1 — "Open up")
- Old "Free Trial / $20 Full Version" pricing structure (replaced by free-forever + Insider)

### Content reuse rules

When pulling forward any heritage copy:
1. **Voice MUST pass BRAND_BOOK.md rules** — no marketing fluff, sentence case headlines, dIKta.me exact capitalization
2. **Pricing wording MUST be swept** per §9.G before publishing
3. **Dates MUST be removed** (no April 15, no "Today: April 7")
4. **"Power License" → "Insider License" everywhere** (or removed entirely)

This section is informational — no action items. Action items appear inline in the relevant sections (§9, §14, §15, §16) below.

---

## 1. The Pivot — Why & What Changed

### The old model

Until now, dIKta.me V2 has gated **BYOK** (bring your own API key) and **Local mode** (Whisper + Ollama + Kokoro on the user's own machine) behind a one-time $20 "Power License." The Wallet ($1 onramp, managed Deepgram + Gemini pipeline) was the free entry point. Users could try Wallet for free, but to use their own keys or run anything locally, they had to pay.

### The diagnosis

That paywall is the weakest defensible kind. It charges users for **permission** on capabilities that cost the developer **zero marginal dollars**. The user brings their own API key — we don't pay for it. The user runs Whisper on their own GPU — we don't pay for it. Charging $20 for that creates resentment in the exact audience most likely to advocate for the tool: power users, indie devs, privacy-conscious people, the kind of folks who post on Hacker News and Reddit.

It also gives competitors easy attack surface ("$20 to use your own API key on your own machine") and produces a positioning story that doesn't survive five seconds of public scrutiny.

### The new model

**Free forever. Open source. Fund the build.**

The base app — including BYOK and Local — is **free, MIT-licensed, source-available**, no feature gates of any kind. Anyone downloads the app from GitHub Releases (or, eventually, Microsoft Store) and uses every capability it ships with.

The **Insider supporter license** ($20 minimum, pay-what-you-want via LemonSqueezy) unlocks:
- Continuous release channel (alpha/beta builds shipped every 2-4 weeks)
- Early access to plugins (months 2-4 roadmap, see §6)
- Discord access (community + direct dev contact)
- Weekly devlog / build stream
- Model benchmarks + integration recipes
- "Founding Insider" badge for the first 10-15

The Wallet stays as a $1 trial onramp, **fully orthogonal** to the Insider license — a separate paid product with its own scaling roadmap (see §5).

### Why this is stronger

The philosophical shift: from **"charging for permission"** to **"charging for ongoing labor."** That's an honest exchange — pay me to keep working fast on this. It's the JetBrains perpetual-fallback model, the Obsidian Sync model, the Tailscale-personal model. It scales with how much value the dev actually delivers, not with arbitrary feature flags.

### The decisive frame

The whole experiment reduces to one question:

> **"Is the tool good enough that people will recommend it unprompted?"**

If yes, free path wins decisively at this scale. If no, neither path works and the licensing model is irrelevant. With no current paying customers and no marketing budget, **conversion-rate optimization is irrelevant — distribution is the only lever.** Free + community advocacy compounds; paywall + zero distribution does not.

### What changes operationally

- **Code**: 4 files, single-digit-line guard removals (see §8)
- **Website**: pricing rewrite, roadmap restructure, /community page, 3-5 themed landing pages, commercial wording sweep (see §9)
- **Repo**: source-available posture lockdown (no PRs accepted), key rotation before going public (see §11)
- **Distribution**: GitHub Releases primary, Microsoft Store as additive Plan B/C (see §7)
- **Testing**: re-key the manual test plan to the new model, then run the full plan as the launch gate (see §13)

The actual code change is small. The strategic shift is large.

---

## 2. Strategic Frame — Fit-to-Market Experiment, Not Startup

This is **not a startup**. It's a 3-6 month fit-to-market experiment with explicit checkpoints and a clean exit option.

### Constraints

- **Solo builder.** No team to scale into, no co-founder.
- **No marketing budget.** Total experimental spend cap: ~$200 (compute + minimum Facebook ad tests).
- **Built without writing code.** Indie AI-tooling story is a feature of the narrative, not a weakness.
- **No existing paying customers.** Zero conversion to protect; nothing to grandfather.
- **Solo bandwidth.** Can't sustain a high-touch support model. Choose a business model that doesn't fight that.

### Success bars

- **Massive success**: $200-500/month sustained Insider revenue at Day 180.
- **Median success**: $100-300/month, small loyal community, 2-3 plugins shipped.
- **Worst case (acceptable)**: <$50/month, no community traction. Sunset gracefully, take learnings to next product.

### Exit strategy

The endgame is **sell the app**, not run it forever. Free + community is *more* attractive to small acquirers at this scale than paid + feature gates — they buy audience, and 10,000 free users with 200 supporters is a more interesting asset than 500 paying users behind a feature gate. Plugin architecture is the actual sellable lock-in.

If a sale doesn't materialize, the next best outcome is a portfolio asset: low-maintenance, generates a small revenue tail, lets the dev focus on bigger product ideas.

### What this experiment is NOT trying to be

- Not "the indie Windows dictation tool everyone uses." That's the lottery upside, not the base case.
- Not a venture-backed business. No metrics dashboards, no growth team, no paid CAC.
- Not a community project the dev is committed to maintaining for 5 years. If imposter syndrome sets in, delegate or let go.
- Not a guarantee. 60% of solo experiments at this scale flatline. The 1-2 day cost of building this blueprint is the price of running the experiment honestly.

### The Windows-native gap (positioning insight from heritage research)

Per `plans/mkt/PH_LAUNCH_PLAN.md` competitive audit, the entire AI-dictation category on Product Hunt is **Mac-first**:

| Tool | PH upvotes | Platform |
|------|-----------|----------|
| Wispr Flow | 2,095 | Mac-first (Windows added separately for 814 more upvotes) |
| AudioPen | 873 | Mac-first |
| Aqua Voice | 585 | Mac-first |

**No Windows-native AI dictation tool has meaningful Product Hunt presence.** Wispr Flow alone proved that "Windows added later" is a separately huntable launch (814 upvotes for the Windows release). dIKta.me is **Windows-first by design**, not a Mac-port afterthought.

This is a genuine differentiator and a positioning headline:
- "The Windows-native AI dictation tool" — accurate, defensible, owns an underserved category
- Frames the platform constraint as deliberate, not a limitation
- Aligns with the Stream Deck plugin (gaming/streamer audience is heavily Windows)

Pull this into the inaugural blog post (§9.C), the homepage commercial wording sweep (§9.G), and any PH launch copy (§14).

### The diagnostic frame

The most important property of the free path is **diagnostic clarity**. By Day 90 you'll know:
- If beta testers stick → the tool is sticky enough
- If GitHub downloads show up → distribution mechanic works
- If Insider conversion > 2% on engaged users → value prop is real
- If none of those hit → wrap up, don't double down

A paid path with feature gates produces ambiguous failure ("nobody bought" — but did they not bother to evaluate it, or did they evaluate and reject?). The free path produces clean signal.

---

## 3. The Two-Lane Model — Free Forever + Insider License

### The two lanes

| Aspect | **Free track** | **Insider track ($20+)** |
|--------|----------------|--------------------------|
| Cadence | 2 stable releases/year (publicly committed; under-promise) | Continuous — ≥1 visible ship every 2-4 weeks |
| Features | Full feature parity, all current capabilities | Same as free + early access |
| Plugins (months 2-4) | At next stable release | Alpha/beta as they ship |
| Bug fixes | Critical bugs patched between releases | All bug fixes, including paper cuts |
| Channel | Stable | Pre-release (Velopack `prerelease: true`) |
| Discord | Read-only general channels (or none) | Full access incl. #insider-only, #devlog |
| Devlog stream | Public archive (after the fact) | Live access + Q&A |
| Model benchmarks | Quarterly summary | Weekly recipes + integration notes |
| License | MIT, source-available | Same MIT — license is for the *channel*, not the *code* |
| Source code | Public on GitHub | Same — Insiders read the same source |

### Why "Insider" is honest

The Insider license does NOT gate any capability of the software. The free build is feature-complete, MIT-licensed, and forkable. What the $20 buys is **membership in the build process** — early access, the developer's time and attention, and the social affordances of being inside the project.

This is a critical positioning decision. Saying "Insider unlocks features X, Y, Z" would be dishonest under this model. Saying "Insider is how you join the build and support the dev" is true and durable.

### Naming guardrails

**Use these terms:**
- "Insider supporter license"
- "Insider edition" (for the continuous build channel)
- "Founding Insider" (for the first 10-15 free founders)

**Avoid these terms:**
- "Donation" — legal/tax/Store-policy risk; LemonSqueezy paid product is what we're actually selling
- "Pro" / "Premium" — implies feature tier, which doesn't exist anymore
- "Trial" — there is no trial; the free version is the full product

### The promise (and its calibration)

Promise **2** stable releases per year. Aim for 4. Two is the floor. If real life hits and only 1 ships, the second-year reputation is bruised but recoverable. If we promise 4 and ship 2, the broken promise destroys the whole framing. Always under-promise on cadence.

Critical bug fixes between stable releases ship as patch versions, free for everyone, no Insider gate.

### Insider channel content obligation

The Insider channel must **actually be visibly fresher** than stable. If an Insider sees no shipped change in their build for 4-6 weeks, the $20 buys nothing visible and goodwill craters. **Operational obligation: ship something visible every 2-4 weeks**, even small (theme tweak, model adapter, prompt update, plugin alpha). This is the operational substitute for the engineering effort the old model spent on feature-gate enforcement.

---

## 4. Insider Tier — What $20 Buys

### The full bundle

The framing is **"join the build,"** not "unlock software." Software access is the side effect.

| What | What it actually means |
|------|------------------------|
| **Continuous releases** | Velopack `prerelease: true` channel. Every 2-4 weeks at minimum. Sometimes weekly. |
| **Plugin alpha** | 8-10 plugins are queued (see §6). Insiders see them weeks-to-months before stable. |
| **Discord** | Build server. Founders pinned in #welcome. #insider-only channel, #devlog (live build feed), #bug-reports, #feature-requests, #showcase. |
| **Weekly devlog stream** | Twitch or YouTube Live (platform TBD by Day 7). One stream/week, ~30-60 min, "what shipped, what's next, AMA." |
| **Model benchmarks** | Weekly recipe: "tested gpt-5-nano on dictation, here's the numbers, here's how to plug it in." |
| **Blog access** | All public blog posts + Insider-only deep dives (e.g., why a model failed, what we tried, how the latency went down). |
| **Collateral** | Vouchers for partner services if any materialize, exclusive Insider-only theme/skin, founder digital badge. |
| **Founding Insider badge** (first 10-15) | Permanent visible status in Discord. They're the visible advocates. |
| **The dev's attention** | Slow but real — bug reports from Insiders get triaged first. |

### What it does NOT buy

- No software features unavailable to free users
- No private fork of the source
- No service-level agreement on response time
- No commercial support or consulting
- No guarantee of future cadence beyond the 2-stable-releases/year floor

### The pitch

Single-line homepage version: **"Free forever. Open source. Fund the build."**

Pricing-page version (paragraph form):
> *dIKta.me is free forever and open source under MIT. If you want to support its development and get continuous releases, plugin alphas, weekly model benchmarks, and Discord access, the Insider supporter license is $20+ (pay what you want, lifetime). It's how the build keeps shipping fast.*

### Pay-what-you-want mechanics

LemonSqueezy supports a **minimum + tip** flow:
- Minimum: $20
- Default: $20 (cleanest UX)
- Optional: $29 / $49 / "name your price" buttons
- All purchases produce identical Insider keys — there's no tier ladder

Larger contributions are appreciated, not rewarded with extra access. This avoids creating a hidden Insider+/Insider-pro split that we'd then have to maintain.

### Recurring revenue option (deferred)

If Insider conversion plateaus, consider adding an **optional monthly recurring** at $5/$10 (still pay-what-you-want minimum). This becomes a Substack-equivalent for the project. Don't add it at launch — adds complexity without proven demand. Revisit at Day 90.

---

## 5. Wallet — Orthogonal Product Positioning

### What the Wallet is (and isn't)

The Wallet is the managed pay-as-you-go pipeline (Deepgram STT + Gemini LLM) with a $1 onramp. Its purpose: **let a brand-new user dictate within 60 seconds without entering an API key, picking a model, or installing anything local.**

It is NOT:
- The "paid tier" of the app — that framing is wrong now
- A premium feature — every Wallet user gets the same software the free user gets
- A revenue strategy — it barely covers its own marginal cost at current prices

It IS:
- A trial mechanism (most important job)
- An option for users who hate API key management
- A separate product line with its own roadmap, orthogonal to Insider

### Copy positioning

**Use:** "Wallet — try dIKta.me with $1 of credits, no API key required."

**Avoid:** Anything that conflates Wallet with paid tier. Wallet ≠ Insider. The dashboard, pricing page, and in-app copy must keep them visually and conceptually separate.

### Wallet revenue contingency — what to do if it grows unexpectedly

The Wallet is positioned as a trial onramp, not a primary revenue stream. The current $1 default barely covers its own cost and is not competitive on speed/quality with subscription-based dictation incumbents. But if it organically scales — because some users genuinely prefer "one bill, no API key juggling" — this blueprint reserves capacity to evolve it without losing the Insider positioning.

#### Activation trigger

If Wallet revenue crosses **~$200/month sustained for 60+ days**, it warrants a deliberate scaling decision, separate from the Insider experiment. Below that threshold, leave the Wallet alone — adding levers without revenue to justify them just expands the support surface (refunds, top-up failures, dispute resolution).

#### Scaling levers (in increasing order of operational complexity)

1. **Top-up tiers**
   - Replace the $1 default with a multi-pack picker: $5 / $20 / $50.
   - Same per-dollar credit value; better UX for serious users.
   - Implementation: LemonSqueezy variant SKUs + LoadingViewModel update flow.
   - Risk: minimal.

2. **Volume discount**
   - $50 buys ~$55 in credits.
   - Loyalty acknowledgement without subscription friction.
   - Implementation: webhook calculates bonus on receipt of payment.
   - Risk: low. Just edge cases on partial refunds.

3. **Auto-top-up (opt-in)**
   - "Refill to $20 when balance drops below $5."
   - Stays consistent with the "no subscription" promise — user controls timing, can cancel anytime, no recurring obligation.
   - Implementation: balance-watcher edge function + LemonSqueezy stored payment method.
   - Risk: medium. Needs careful UX (user must explicitly opt in, must be one-tap to disable, must show next-charge clearly).

4. **Family / team wallets**
   - Shared credit pool across multiple users.
   - Operationally heavier — multi-user attribution, shared state, "who used how many credits."
   - Defer until proven demand. Likely never relevant for a solo dictation app.

5. **Bridge subscription** (only if asked for repeatedly)
   - Monthly recurring $5 / $10 / $20 with bonus credits.
   - **Keep entirely separate from the Insider tier** — they solve different jobs (managed compute vs. funding the dev). Don't blur them in copy.
   - Implementation: LemonSqueezy subscription product alongside one-time Wallet top-up SKU.

#### Hard decision question (Day 90 review)

> If Wallet revenue ends up being the dominant signal of growth (>3× Insider revenue), is the Insider framing still right — or should the brand pivot to "managed cloud dictation, free local mode" with Wallet as the centerpiece?

Don't pre-commit to that pivot, but document it now as an option to evaluate against actual numbers. The blueprint commits to revisiting this question at Day 90 with concrete revenue split data (Insider $/mo vs Wallet $/mo).

#### Guardrail

Every Wallet scaling lever increases real marginal cost on the user's side (more credits used = more API spend). **Don't add levers until the economics demonstrably justify the support load.** The Insider license has zero marginal cost — that's structurally why it scales better as a primary revenue lever for a solo dev.

---

## 6. Plugin Architecture — Months 2-4 Roadmap Inside Insider

### Current state

Plugin system exists. Audited:
- `src/DiktaMe.Plugin.Abstractions/IPlugin.cs` — interface with Id, DisplayName, State, Initialize/Enable/Disable async methods
- `src/DiktaMe.Plugin.Abstractions/IPluginContext.cs` — context passed in
- `src/DiktaMe.Plugin.Abstractions/IPluginSettingsStore.cs`, `IPluginUIRegistry.cs` — ancillary surfaces
- `PluginManager` — discovers plugins from `%APPDATA%\DiktaMe\plugins\`, loads, enable/disable
- `PluginEntryAttribute` — marks plugin DLL entry points
- Current consumer: Stream Deck plugin (only one)

The architecture is **production-ready**. We don't need to design plugins from scratch.

### Insider plugin gating (deferred to month 2)

Add at month 2:
- New flag on `IPlugin`: `bool RequiresInsider { get; }` (default false)
- `PluginManager` filters plugins where `RequiresInsider == true && !licenseManager.IsLicensed` — those simply don't load
- Plugin distribution: signed ZIP attached to GitHub Releases under `insider-plugins/` tag, OR auto-downloaded from CDN with license-validated URL
- For simplicity at month 2, ZIP-on-Release is sufficient; CDN can come later

### Plugin queue (8-10 candidates from user's list)

Order is not committed; pick based on demand signal from Discord:

1. **Connectors** (already in progress per existing roadmap) — integrate with productivity tools (Slack, Notion, etc.)
2. **Meetings & Scribe** — record + transcribe + summarize meetings
3. **Memory Layer** — persistent context across sessions
4. **Advanced Refine** — multi-pass LLM refinement workflows
5. **Chaviz Orchestrator** (ideation phase per existing roadmap) — chained workflows
6. **Dictionary / vocabulary plugin** — domain-specific term injection
7. **Voice command runner** — "open Notepad" / "send email" via voice
8. **Dictation history search** — full-text search across stored dictations
9. **Translation memory** — cached translations for repeat phrases
10. **Custom theme / accessibility plugin** — user-contributed appearance variants

### Cadence

- **Month 2 (Days ~30-60)**: ship plugin gating + 1 plugin (e.g., Connectors v0.1) for Insiders
- **Month 3 (Days ~60-90)**: ship 2 more plugins for Insiders; first plugin (Connectors) graduates to free track at first stable release
- **Month 4 (Days ~90-120)**: ship 2-3 more plugins for Insiders; plugin "graduation" (Insider → free) becomes a recurring cadence event

### What plugins do NOT include

- No marketplace
- No per-plugin purchase
- No plugin "tiers" (just Insider-current vs free-stable)
- No third-party plugin developers welcomed at this stage (matches no-PR posture)

### When plugins fight the OSS posture

If a plugin requires private API keys to ship (e.g., a paid SaaS integration), document the gap. Don't gate the plugin code itself — keep it MIT — but acknowledge in the README that running it requires the user's own credentials for that service. Same BYOK pattern as Cloud LLM.

---

## 7. Distribution Strategy — Store + GitHub Parallel

### The default — GitHub Releases (always)

Velopack-packaged installer + delta updates via existing CI:
- `.github/workflows/ci-v2.yml` already produces Velopack outputs
- Release job triggered on `v*` tags
- `releases.win.json` feed for in-app UpdateManager
- Already works on `main` push for badge updates

GitHub Releases is **the primary distribution channel.** Microsoft Store is additive, never gating. If Store certification fails or stalls, GitHub Releases ships unchanged.

### Microsoft Store — three-path contingency

Decided by Day 30 / Day 60 traction signals.

> **⚠ Operating context: solo individual, not an incorporated company.** This affects every signing/Store decision below. Specifically: **EV (Extended Validation) code-signing certificates are NOT available to individuals** — EV requires verified company registration (LLC, Inc., Ltd., or international equivalent). The path the dev can actually take is **IV (Individual Validation) code signing**, which is cheaper but does NOT grant the immediate SmartScreen "instant trust" that an EV cert would. SmartScreen reputation still has to build over download count even with IV. Do not confuse IV with EV in any vendor conversation.

| Path | Annual cost | When to choose | Tradeoff |
|------|-------------|----------------|----------|
| **A. Free path (Day 0 default)** | $0 | Always start here. Launch via GitHub Releases + Velopack only. SmartScreen warnings appear initially; reputation builds with downloads. | No Store discoverability. Friction on first install (Windows warns "untrusted publisher"). |
| **B. Lean path** | ~$140/yr ($19 Partner Center one-time + ~$120/yr Microsoft Trusted Signing subscription) | Activate when budget allows OR when traction triggers fire. MTS supports individual identities — no company required. | Slower SmartScreen reputation than what EV would give a company; for an individual this is the practical ceiling on Store-side legitimacy without incorporating. |
| **C. Premium individual path** | ~$320/yr ($19 Partner Center + ~$300/yr **IV code-signing certificate** — NOT EV, EV is unavailable to individuals) | Activate ONLY if Day 60 traction is "off the roof" — see triggers. **The "$300" figure is the IV cert annual recurring cost, not the Partner Center fee.** | IV cert signs the binary so Windows shows the dev's verified individual name as publisher (instead of "Unknown publisher"). SmartScreen reputation still builds with downloads — IV does NOT grant instant trust the way EV does for companies. The win over Path B is binary attribution + slightly faster reputation accrual, not zero-warning installs. |

#### Activation triggers for Path B or C

Any **2 of the 5** triggers, evaluated at Day 30 and Day 60:

- [ ] >500 cumulative GitHub Release downloads
- [ ] >10 unsolicited Insider license purchases (not gifted founders or review-claimers)
- [ ] Discord exceeds 50 active members (not just members — *active* in the past 14 days)
- [ ] At least one unsolicited public review (blog, video, Reddit, X post) from outside the gaming-community beta circle
- [ ] >50 founder feedback messages indicating daily use

If 2+ triggers fire at Day 30, fast-track Path B. If 4-5 fire by Day 60, jump to Path C.

#### Cancellation path

If KPIs miss after activation:
- IV cert and MTS are annual — let them lapse at renewal, no early-termination cost beyond unused term
- Partner Center fee is one-time, no recurring obligation
- **GitHub Releases stays primary the entire time** — Velopack continues delivering updates from GitHub regardless of Store state
- Removing or letting the Store SKU expire does NOT affect existing GitHub-installed users

#### Reversibility decision rule

> Never invest in Path B or C unless we're prepared to walk away from it cleanly. Treat any signing/Store annual fee as a 1-year experiment, renewable based on Day 365 review.

### Code signing — same line item as Store

The "$300 cost" the user has been referring to is the **IV (Individual Validation) code-signing certificate** (Path C), not the Partner Center developer fee. They're separate line items but bundled in the budget conversation. Critical: the dev is an individual, not a company, so EV is not on the table — the $300 buys an IV cert, which is the strongest signing identity available to an individual.

| Concern | Path A | Path B | Path C |
|---------|--------|--------|--------|
| Partner Center dev account (individual) | N/A | $19 one-time | $19 one-time |
| Code signing | None (unsigned) | Microsoft Trusted Signing (~$120/yr) | IV cert (~$300/yr) |
| Publisher attribution | "Unknown publisher" | MTS-attributed | Verified individual name |
| SmartScreen reputation | Builds slowly via download count | Builds with download count | Builds with download count (IV does NOT grant instant trust like EV does for companies) |
| First-install UX | Warning dialog ("untrusted") | Warning fades after some downloads | Warning fades faster than B; still requires download volume to fully clear |
| CI signing wiring | Not done — also not needed | P0 task once funded | P0 task once funded |
| Available to individual? | Yes | Yes | Yes (IV is individual-friendly; EV would not be) |

### Winget — stretch goal

Free, fast certification, good for Windows Package Manager users. **File for after Day 14** — not a launch blocker. Submit `.yaml` manifest to `microsoft/winget-pkgs` repo via PR.

### Distribution artifacts produced today

From audit of `.github/workflows/ci-v2.yml`:
- `dIKta.me-X.X.X-Setup.exe` (Velopack/Inno Setup installer, ~47 MB)
- `.nupkg` (Velopack delta package)
- `releases.win.json` (Velopack feed)
- All attached to GitHub Release on tag push

What's MISSING for Store path:
- `Package.appxmanifest` (MSIX identity manifest) — needs to be created
- Identity name reservation in Partner Center — needs to be done after dev account
- Code signing pipeline — needs Path B or C certificate

### Distribution priority order (final)

1. **Day 0**: GitHub Releases v2.1.0 ("Open up") tagged via existing CI
2. **Day 7-14**: If $19 affordable, register Partner Center; submit MSIX to Store (Path B)
3. **Day 14+**: Address Store certification feedback (typically 1-3 rounds)
4. **Day 30 / 60**: Evaluate Path C upgrade based on activation triggers
5. **Day 30+**: Submit winget manifest

---

## 8. Code Adjustments — Open Up the App

### Scope

The actual code change to enable the new model is **minimal — 4 files, single-digit-line changes** to remove feature gates. The license system itself stays intact and gets repurposed semantically (from "Power License unlocks features" to "Insider License unlocks the release channel + plugins + Discord").

### Files to change (open the gates)

| File | Lines | Action | Verification |
|------|-------|--------|--------------|
| `src/DiktaMe.Core/Config/PipelineFactory.cs` | 281-284 | Delete the `InvalidOperationException("Power License required. Purchase at dikta.me/pricing")` guard. All non-Wallet paths become free. | New unit test asserts BYOK + Local pipelines build successfully without `IsLicensed`. Existing PipelineFactoryTests still pass. |
| `src/DiktaMe.App/Views/Wizard/WizardGetStartedPage.xaml.cs` | 69-79 | Remove `IsEnabled = licensed` guards on BYOK + Local radios. All three options (Wallet / BYOK / Local) enabled by default. | Manual: launch wizard fresh (no license), verify all radios enabled. |
| `src/DiktaMe.App/Views/Wizard/WizardTtsPage.xaml.cs` | 59, 84-89 | Remove the bounce-away-if-unlicensed branch on local TTS (Kokoro). | Manual: select Local TTS in wizard without license; should proceed normally. |
| `src/DiktaMe.App/Views/WizardWindow.xaml.cs` | 72 | Remove `HaveKeyButton.Visibility = step == 1 && !licensed` clause. Either always visible (rebrand to "Already an Insider?") or repositioned in WizardActivatePage. | Manual: launch wizard with license active, "I Have a Key!" / "Already an Insider?" button still appears (or is moved per UI decision). |

### Files to repurpose (Insider semantics)

| File | Action |
|------|--------|
| `src/DiktaMe.Core/Security/LicenseManager.cs` | Keep all plumbing (LemonSqueezy API client, SecureStorage keys, 30-day grace, machine binding). Update XML docs and inline comments from "Power License" → "Insider License." Tier value stays `"power"` for back-compat with existing keys, OR introduce `"insider"` and migrate (low priority). |
| `src/DiktaMe.App/ViewModels/Settings/AccountSettingsViewModel.cs` | Rebrand UI strings: "Power License" → "Insider License." Update marketing copy fields to mention continuous releases / Discord / stream / plugins instead of "BYOK + Local unlock." EN + ES localization keys. |
| `src/DiktaMe.App/Views/Wizard/WizardActivatePage.xaml.cs` | Same rebrand. Title, body copy, link text. EN + ES. |
| `src/DiktaMe.App/Services/UpdateService.cs` | Add **opt-in** Insider channel toggle: new settings flag `Updates.InsiderChannel` (default false). When `true && IsLicensed`, set Velopack `prerelease: true`. When false (or not Insider), stable-only. |
| `src/DiktaMe.Core/Config/AppSettings.cs` | Add `Updates.InsiderChannel` boolean field (default false). |

### Files to leave alone (Wallet plumbing — orthogonal)

These have no license dependency today and stay that way:

- `src/DiktaMe.Core/Data/WalletManager.cs`
- `src/DiktaMe.Core/Account/WalletGeminiProxy.cs`
- `src/DiktaMe.Core/LLM/LLMRouter.cs` (Wallet routing)
- `src/DiktaMe.Core/STT/STTRouter.cs` (Wallet routing)
- `src/DiktaMe.Plugin.Abstractions/PluginManager.cs` (will gain Insider plugin filter at Month 2, not now)

### Test changes

- `tests/DiktaMe.Core.Tests/Security/LicenseManagerTests.cs` (62 tests) — **stay**. They validate LemonSqueezy plumbing, which survives the pivot.
- `tests/DiktaMe.Core.Tests/Config/PipelineFactoryTests.cs` — audit for any test asserting "throws without license" on BYOK/Local paths → invert assertion or delete.
- `tests/DiktaMe.Core.Tests/Account/AccountServiceTests.cs` — stay (auth/JWT, not license).
- `tests/DiktaMe.Core.Tests/LLM/LLMRouterWalletTests.cs` — stay (Wallet, not license).
- New tests recommended:
  - `PipelineFactory_BuildsBYOKPipeline_WithoutLicense` — explicit assertion that the gate is gone
  - `PipelineFactory_BuildsLocalPipeline_WithoutLicense` — same
  - `UpdateService_RespectsInsiderChannelToggle` — covers the new opt-in

Test count goes from 1256 → ~1259 (3 new tests minus any deleted). CI test threshold (currently 470) unaffected.

### CI implications

- `dotnet format --verify-no-changes` — should pass (no formatting changes intended)
- `dotnet test` — should pass; new tests added, none failing
- Test count badge — bumps to ~1259
- Publish size — unchanged (deletion is single-digit lines)
- Size guard (130-250 MB) — unaffected

### Commit strategy

Single commit for the open-up change:
```
feat(licensing): pivot to free-forever + Insider supporter model [DIKTAX-002]

- Remove license gate on BYOK + Local in PipelineFactory
- Remove license guards in wizard (BYOK/Local/TTS pages)
- Rebrand "Power License" → "Insider License" in UI copy
- Add opt-in Insider release channel toggle in UpdateService
- LicenseManager plumbing unchanged; semantic shift only

Implements §8 of DIKTAX_3-6_BLUEPRINT.md
```

Followed by a separate commit for any related test changes.

---

## 9. Website & Funnel Adjustments

### Strategy

The homepage hero stays as-is — it's based on capabilities and key product pillars, not commercial framing, so it doesn't need a rewrite. What changes:
- **Pricing page** — heaviest lift, full rewrite from "Free Trial / $20 Full Version / Build It Yourself" to "Free Forever / Insider Supporter / Build It Yourself"
- **Roadmap page** — restructure to two-track view (Free vs Insider)
- **Inaugural blog post** — model-pivot announcement
- **/community page** — Discord, stream schedule, Founding Insider mechanics
- **3-5 themed landing pages** — mobile-first Facebook ad funnels
- **Mobile SEO/UX polish** — Facebook traffic is mostly mobile
- **Commercial wording sweep** — across `messages/{en,es}.json` for consistency

### A. Pricing → Insider page rewrite

**File**: `website/app/[locale]/pricing/page.tsx`
**Component**: `website/app/components/PricingSection.tsx`

**Old structure**:
```
Free Trial ($0) | Full Version ($20, $99 strikethrough) | Build It Yourself (MIT)
```

**New structure**:
```
Free Forever ($0) | Insider Supporter ($20+) | Build It Yourself (MIT)
```

#### Free Forever card

- Title: "Free Forever"
- Subtitle: "Open source. No credit card. No tier ladder."
- Bullets:
  - All features (Cloud + Local + BYOK + Vision + Macros + …)
  - Bring your own API keys
  - Run everything locally with Whisper + Ollama + Kokoro
  - 2 stable releases per year + critical bug fixes
  - MIT-licensed source on GitHub
- CTA: **"Download Free"** → GitHub Releases link

#### Insider Supporter card (recommended highlight)

- Title: "Insider Supporter"
- Subtitle: "$20+ lifetime · Pay what you want"
- Bullets:
  - Continuous release channel (≥1 ship every 2-4 weeks)
  - Plugin alpha access (Connectors, Meetings, Memory Layer, …)
  - Discord access (build server + #insider-only)
  - Weekly devlog / build stream
  - Model benchmarks + integration recipes
  - "Founding Insider" badge (first 10-15)
  - Lifetime — no recurring fees
- CTA: **"Become an Insider"** → LemonSqueezy checkout (PWYW UI, $20 minimum)
- Sub-text: "Same MIT software. Pay to fund the build, not for features."

#### Build It Yourself card

- Title: "Build It Yourself"
- Subtitle: "MIT licensed. Clone, build, run."
- Bullets:
  - Source-available on GitHub
  - No license required for self-compiled builds
  - Fork freely (no contributions accepted upstream — see CONTRIBUTING.md)
  - Build instructions in repo README
- CTA: **"View on GitHub"** → repo link

**Anchor removal**: drop the `$99 strikethrough` — that was old anchoring. The headline price is just $20+.

**Schema.org metadata update**: Offer pricing model — three Offers, one priced $0, one priced $20 (PWYW), one priced $0 (build-yourself).

**Localization**: EN + ES. All keys in `messages/en.json` + `messages/es.json` under `PricingSection` namespace.

### B. Roadmap restructure

**File**: `website/app/[locale]/roadmap/page.tsx`

Current structure (V1 vs V2 comparison + plugin timeline) stays as informational background. **Add** a clear two-track view:

```
┌─────────────────────────────────┬──────────────────────────────────┐
│ Free track                      │ Insider track                    │
│ (everyone, 2 stable/year)       │ ($20+ supporter)                 │
├─────────────────────────────────┼──────────────────────────────────┤
│ v2.1.0  Q2 2026                 │ Continuous · alpha → beta → RC   │
│  - Open up (free + MIT)         │  - Plugin: Connectors v0.1       │
│  - All current features         │  - Plugin: Meetings & Scribe v0.1│
│                                 │  - Plugin: Memory Layer v0.1     │
│ v2.2.0  Q4 2026                 │  - Weekly model benchmarks       │
│  - Connectors graduates         │  - Devlog stream archive         │
│  - Meetings graduates           │  - Discord                       │
│  - Stability + perf             │  - Theme drops                   │
└─────────────────────────────────┴──────────────────────────────────┘
```

The existing 5-phase plugin timeline relabels as "Insider plugin queue."

This page is also a public commitment device. What ships free, on what cadence, vs what arrives early to Insiders. Keep the language honest — Insiders never get *more* features long-term, just *earlier*.

### C. Inaugural blog post

**Where**: Existing blog system (Supabase `blog_posts` table, dual EN/ES via `slug`/`slug_es`)

**Title (EN)**: "Why dIKta.me is now free, and how to support it if you want to"
**Title (ES)**: "Por qué dIKta.me ahora es gratis, y cómo apoyarlo si quieres"

**Length**: 800-1200 words

**Voice**: Honest, builder-voice, no AI-slop tells. **MUST pass `plans/mkt/BRAND_BOOK.md` voice rules** (short declarative sentences, no marketing fluff, honest about limitations, sentence case headlines, exact `dIKta.me` capitalization). Run through `/humanizer` before publishing if drafted with help.

**Drafting basis** — pull from heritage docs:
- The "Input Layer Is Broken" manifesto in `plans/mkt/LAUNCH_CONTENT.md` §1 (~900-1000 words, EN + ES) is the strongest written piece in the heritage corpus. Salvage the diagnostic frame ("the AI got better, the input layer didn't"), the cost math ("$70/month for AI subs"), and the personal-builder-voice opening. **Rekey the pricing math** — under the new model, the comparison shifts from "$20 unlocks BYOK" to "free forever + $20 voluntary support."
- The HAI angle in `plans/mkt/SOCIAL_HAI_POST.md` (EN + ES) — "Everyone's racing to make AI smarter. Nobody's fixing how you talk to it." Reusable as-is for opening hook variations.
- The Windows-native gap (§2) gets a dedicated paragraph — "this is a Mac-first category and we're the Windows-first answer."

**Structure**:
1. The pivot (1-2 paragraphs) — what changed and why
2. What this means for users (1-2 paragraphs) — concrete: free download, free BYOK, free local mode
3. What "Insider" actually is (2-3 paragraphs) — supporter, not tier; honest about what $20 buys
4. The 6-month experiment framing (1-2 paragraphs) — fit-to-market, KPIs, exit option
5. How to support if you want to (1 paragraph) — Insider link, Discord invite, Founding Insider mechanic
6. What's next (1 paragraph) — devlog cadence, plugin queue, where to follow

**Pipeline**: this post goes through the existing `news-publish` pipeline → Supabase `blog_posts` table → review at `/hqbackstage/blog` → publish (per `plans/mkt/BLOG_ROADMAP.md` architecture).

**Establishes the dev-log cadence** — first post + commits to weekly dev-log posts during the experiment.

### D. /community page (new)

**File**: New route at `website/app/[locale]/community/page.tsx`

Sections:
- **Discord** — invite link (placeholder until server created in Week 1, see §12)
- **Weekly devlog stream** — schedule (e.g., "Wednesdays 8pm CDT, Twitch link TBD")
- **Founding Insider mechanic** — explained: 10-15 free for gaming-community betas (closed offer), 100 lifetime-for-public-review (open offer with submission form)
- **Lifetime-for-review form** — simple form: name, contact, link to public review (blog/video/Reddit/X)
- **Code of conduct** — short, friendly, "this is a small project, be kind"
- **Read-only contribution stance** — explicit: issues welcome, PRs not accepted

i18n: EN + ES.

### E. 3-5 mobile-first themed landing pages

**Routes**: New under `website/app/[locale]/for/[theme]/page.tsx` OR flat at `website/app/[locale]/{writers,gamers,accessibility,developers,multilingual}/page.tsx`

Recommended themes (pick 3-5 to launch, A/B based on Facebook ad performance):

1. **Writers & creators**
   - Headline: "Dictate your next post 3x faster"
   - Pain: long-form drafting, blog/email burnout
   - Pillar: no subscription, BYOK, local mode for privacy
   - Visual: writing/desk imagery
   - CTA: free download

2. **Gamers / streamers**
   - Headline: "Hands-free chat in any game"
   - Pain: typing kills game flow, Stream Deck users want voice
   - Pillar: low-latency local mode, Stream Deck plugin (already exists)
   - Visual: gaming setup imagery
   - CTA: free download

3. **Accessibility users**
   - Headline: "Voice-first computing, free forever"
   - Pain: subscription dictation tools are expensive; accessibility shouldn't be paywalled
   - Pillar: no subscription, ESL multilingual, no microtransactions
   - Visual: accessible computing imagery
   - CTA: free download

4. **Developers**
   - Headline: "Bring your own API key. Open source. Done."
   - Pain: closed-source dictation tools, vendor lock-in
   - Pillar: BYOK, MIT, scriptable via Stream Deck plugin, local Ollama support
   - Visual: terminal / code imagery
   - CTA: download + GitHub link

5. **Multilingual / ESL users**
   - Headline: "Dictate in English, type in Spanish — or the other way around"
   - Pain: existing tools force language choice or charge premium for multilingual
   - Pillar: Translate mode, EN/ES UI, transcription language settable
   - Visual: language imagery
   - CTA: free download

#### Per-page constraints

- **Mobile-first**: tested at 320px width, tap targets 44px minimum
- **One CTA**: "Download Free" → GitHub Release link (with UTM params for Facebook attribution)
- **Reuses**: `HeroSection` block + `CtaSeparator` + `FeatureCard` (already exist in `website/app/components/`) — no new components needed
- **Meta Pixel events**: `trackCtaClick(cta: 'download', section: '<theme>')` on every CTA click
- **UTM-aware download links**: `?utm_source=fb&utm_campaign=<theme>&utm_medium=landing` so Facebook ad attribution flows back

#### Companion comparison pages (heritage reuse)

Each themed landing should ship with **at least one `/vs/<competitor>` companion page** drawn from `plans/mkt/COMPETITOR_PAGES.md`. The content is already written; only pricing wording needs the §9.G sweep. Pairing:

| Themed landing | Companion comparison page(s) | Source |
|----------------|------------------------------|--------|
| Writers & creators | `/vs/grammarly` (adjacent), `/alternatives/dragon` | COMPETITOR_PAGES.md Page 4 + future |
| Gamers / streamers | (no direct competitor — lean on Stream Deck plugin angle) | — |
| Accessibility users | `/alternatives/dragon` (Dragon used heavily by accessibility audience) | COMPETITOR_PAGES.md Page 4 |
| Developers | `/vs/aqua-voice`, `/vs/wispr-flow` | COMPETITOR_PAGES.md Pages 1, 3 |
| Multilingual / ESL | `/vs/wispr-flow` (Wispr is monolingual-leaning) | COMPETITOR_PAGES.md Page 1 |
| (Vision-adjacent — optional) | `/vs/loom`, `/alternatives/screenshot-ai` | COMPETITOR_PAGES.md Pages 5, 6 |

This is high-leverage SEO content — already written, structured for `competitor-alternatives` skill output, just needs the wording sweep before publishing. Files live at `website/app/[locale]/vs/[competitor]/page.tsx` and `website/app/[locale]/alternatives/[competitor]/page.tsx`. **Internal linking plan** per COMPETITOR_PAGES.md SEO Notes section: hub at `/compare/`, each `/vs/` links to corresponding `/alternatives/`, homepage VersusSection links to `/vs/wispr-flow`.

#### Schema markup on landings + comparison pages

Add per `competitor-alternatives` skill output (already documented in COMPETITOR_PAGES.md): FAQPage schema, Product schema with pricing, Comparison/Review schema. Improves AI search visibility (per existing `ai-seo` skill). Each page's pricing schema must reflect the **new free + Insider supporter dual offer**, not the old "$20 Full Version."

### F. Mobile SEO + UX improvements

- **Tap targets**: audit at 320px, ensure ≥44px height on all interactive elements
- **Font scale**: ensure no `<14px` text on mobile breakpoints
- **Largest Contentful Paint**: lazy-load below-fold images, prioritize hero
- **OG images**: currently no explicit OG/Twitter card on pricing page (audit finding) — add per-page OG image generation via Next 16's metadata API
- **Sitemap**: ensure `website/app/sitemap.ts` includes new landing pages + community page + roadmap (re-published)
- **Structured data**: add FAQPage, SoftwareApplication, Organization JSON-LD to relevant pages for AI search visibility (per existing `ai-seo` skill if invoked)
- **robots.txt**: confirm public pages indexable, /dashboard and /login already noindexed

### G. Commercial wording sweep

Files (from website audit):

- `website/messages/en.json`:
  - `pricingDescription` (line 10): "Free trial or one-time $20 Full Version for local AI + BYOK." → "Free forever, open source. Optional $20+ Insider supporter license for continuous releases."
  - `VersusSection` row5 (line 232): `dIKta.me = '$20 (Once)'` → `'Free forever'`
  - `freeTrialBadge` / `freeTrialDesc` / `freeTrialPrice` keys: drop the "trial" framing
  - `powerOldPrice` / `powerPrice` / `powerNote`: rebrand to Insider semantics
  - `buildTitle` / `buildDesc`: keep, slight tweaks
  - `supportCta`: align with "Become an Insider" button copy
  - `SpecsSection` g3f2 (line 308): "Pay-As-You-Go Wallet. No subscriptions." — keep, this is honest about Wallet
- `website/messages/es.json`: same keys, translated
- `website/app/components/HeroSection.tsx`:
  - `ctaVersion: "Windows • Free Trial"` → `"Windows • Free Forever"` (or similar)
  - Other "trial" / "credits" / "subscription" references — sweep

**Single PR for the sweep, not piecemeal.** Test the whole site at `localhost:3000` after the changes.

### H. Dashboard tier reads

**File**: `website/app/[locale]/dashboard/page.tsx` (lines 70-87)

Currently reads `licenses` table tier. Under new model:
- "Free" tier still works fine (default for users without an Insider license)
- Copy update: "Free user" not "Free tier" (no tier ladder anymore — just Free or Insider)
- Insider users see "Insider supporter" with the Founding Insider badge if applicable
- Wallet card stays as-is (orthogonal)

The `licenses` table schema doesn't change. Only display copy.

### I. Admin panel

**File**: `website/app/[locale]/hqbackstage/licenses/page.tsx` + `LicenseGiftForm`

Already supports pre-issuing licenses via `pending_gifts` table. Use it operationally to gift Founding Insider keys to gaming-community betas (10-15 keys) and review-claimers (up to 100 keys).

No code changes needed — just operational use.

### J. Privacy + Terms

- `website/app/[locale]/privacy/page.tsx`: review for "free trial," "subscription" wording. Update to reflect "free forever, optional Insider supporter license, BYOK, no subscription."
- `website/app/[locale]/terms/page.tsx`: same review. Add explicit clause that Insider license is a one-time supporter purchase, not a service contract — if dev pace slows, no SLA breach.

EN + ES.

---

## 10. Documentation & User-Facing Copy

### Voice authority

**`plans/mkt/BRAND_BOOK.md` is canonical.** Every word in this section, every line of UI copy, every blog post, every social post, every Insider email — must pass the BRAND_BOOK voice rules:

- Short, declarative sentences (no marketing fluff like "revolutionary" or "game-changing")
- Practical, specific (no vague promises like "takes your productivity to the next level")
- Honest about limitations (no overclaiming)
- Warm but not cheerful (no emojis in serious copy)
- Respect the user's intelligence (no over-explaining obvious things)
- Sentence case headlines (not Title Case)
- Hotkeys formatted as code: `Ctrl + Alt + D`
- Product name always written as **dIKta.me** (lowercase d, uppercase IK, lowercase ta.me) — never "Diktame," "DIKTAME," or "diktame" in user-facing copy

Visual identity (also from BRAND_BOOK.md):
- Brand orange: `#C55A28` (logo, CTAs, links — NOT used as in-app accent; per-theme accents handle that)
- Inter font everywhere
- Midnight palette `#0A0918` for canonical dark treatment
- Logo variants: `diktame_SVG_003.svg` (white-on-orange primary), `diktame_SVG_001.svg` (reversed), `diktame_SVG_002.svg` (contained square for avatars/favicons)

### Repo docs

- **`README.md`**:
  - Rewrite landing pitch to "Free forever. Open source. Fund the build."
  - Keep test badge (currently 1125 — bumps to ~1259 after open-up code change)
  - Keep build/run instructions
  - Add "Source-available, no contributions accepted" banner near top
- **`CHANGELOG.md`**: document the model pivot under the next version (e.g., 2.1.0 — "Open up: free forever, Insider supporter")
- **`LICENSE`**: confirm MIT copyright line — audit shows "Copyright (c) 2025-2026 geckogtmx" — fine, no change
- **`CONTRIBUTING.md`**: already says "no PR reviews" — keep as-is, optionally add explicit "fork & build" instructions for users who want to modify
- **`THIRD_PARTY_LICENSES.md`** (new, optional but professional): list NAudio (MIT), Whisper.net (MIT), KokoroSharp (Apache 2.0), CommunityToolkit.Mvvm (MIT), Microsoft.Extensions.* (MIT), H.NotifyIcon (MIT), Serilog (Apache 2.0), Microsoft.WindowsAppSDK (MIT), Velopack (MIT), ScreenRecorderLib (Apache 2.0), InputSimulatorStandard (MIT), Microsoft.Data.Sqlite (MIT). All MIT/Apache compatible — confirmed by audit, no GPL/AGPL.
- **`SECURITY.md`** (new, recommended for public repo): private vuln-disclosure email, no expectation of bug bounty, response time "best effort"
- **`CODE_OF_CONDUCT.md`** (new, optional): "this is a read-only OSS project, be kind in issues"
- **`.github/ISSUE_TEMPLATE/bug_report.md`**: standard bug template + "no PRs accepted" notice
- **`.github/ISSUE_TEMPLATE/feature_request.md`**: standard feature request template
- **`.github/PULL_REQUEST_TEMPLATE.md`**: explicit "PRs are not accepted at this time. Please open an issue or fork the project."

### In-app copy

- Wizard pages: rebrand "Power License" → "Insider License" everywhere (EN + ES)
- Settings → Account: same rebrand, update marketing copy
- Tray menu, About page: any reference to license tier
- All localized strings in `App.xaml.cs` resource lookups
- EN + ES coverage verified end-to-end

### Privacy & Terms (in-app + website)

- App: `Settings → Privacy` page — verify wording is consistent with new model
- Website: see §9.J above

---

## 11. Open-Source Lockdown — Source-Available Posture

### Critical pre-public-repo gate (P0 BLOCKER)

Audit found exposed live secrets in `website/.env.local`. Before going public OR before any clone of the repo to a public host:

> ⚠️ **MUST DO BEFORE ANY PUBLIC EXPOSURE**:
> - [ ] Rotate `SUPABASE_SERVICE_ROLE_KEY`
> - [ ] Rotate `GEMINI_API_KEY`
> - [ ] Rotate `DEEPGRAM_API_KEY`
> - [ ] Rotate R2 access keys + secret key
> - [ ] Rotate `RESEND_API_KEY`
> - [ ] Confirm `.env.local` is in `.gitignore` (audit says it is — verify with `git check-ignore website/.env.local`)
> - [ ] Run gitleaks against full history locally (`gitleaks detect --log-opts="--all"`) — CI runs it but local full-history scan recommended
> - [ ] Audit git history for any old `.pem` / `.key` / `.pfx` from the deprecated RSA license system

### Posture lockdown (small doc additions)

- README banner: "Source-available (MIT). Fork freely. No external contributions accepted."
- CONTRIBUTING.md: explicit "Issues welcome, PRs not accepted (solo dev). Fork and build for your own use."
- GitHub repo settings: pin `CONTRIBUTING.md` in issue/PR creation flow
- `.github/PULL_REQUEST_TEMPLATE.md` triggers on PR creation, restates posture

### Why source-available, not "fully open with contributions welcomed"

- Solo dev, imposter syndrome concern. Not a developer by profession.
- No bandwidth to triage PRs, run code review, maintain a contribution standard.
- "Read-only OSS" is a legitimate posture (many projects do this).
- Forks welcomed — that's the contribution model. If someone wants to maintain a fork, they can.
- Allows full transparency without operational burden.

### Why NOT BUSL or PolyForm

- These are non-compete licenses (license restricts commercial competing redistribution).
- More legal complexity for a solo dev to defend.
- MIT is the simplest, most universally understood license. Compatible with everything.
- Risk: someone forks and ships a competing product. Acceptable risk at this scale — they'd need to do all the maintenance work themselves, and the canonical maintainer + brand/domain stay with the dev.

---

## 12. Pre-Launch Checklist — 1-2 Weeks

User-revised estimate (2026-04-26): **1-2 focused days** for the manual test plan, not the document's 22-32-hour ceiling. That compresses the whole pre-launch window to roughly **1-2 weeks** total.

### Days 1-2 (focus block: code change + tests + secrets rotation)

- [ ] **P0 BLOCKER**: rotate all leaked keys in `website/.env.local` (Supabase service role, Gemini, Deepgram, R2, Resend) before anything else — this gates the public-repo move and reduces risk if keys are already burned
- [ ] Apply the 4-file code change to open the gates (§8) — single commit
- [ ] Update unit tests where assertions invert (any "throws without license" → "succeeds without license")
- [ ] Add 2-3 new tests asserting BYOK + Local pipelines build without license
- [ ] Rebuild + verify `dotnet test` green, `dotnet format --verify-no-changes` clean
- [ ] Re-key `MANUAL_TEST_PLAN.md` per Appendix C (Journeys 1, 3, 4, 6 wording)
- [ ] Run the full manual test plan in a focused 1-2 day push: Journeys 1, 3, 6 are critical-path; 2, 4, 5, 7 + cross-cutting can run in parallel
- [ ] Triage any bugs surfaced in testing — fix or accept-as-known; close out before submission

### Days 3-7 (website + docs + Store kickoff)

- [ ] Pricing page rewrite (EN + ES)
- [ ] Roadmap page restructure (EN + ES)
- [ ] /community page (EN + ES)
- [ ] First 1-2 themed landing pages (EN + ES) — start with "Writers" + "Gamers" as broadest reach
- [ ] Inaugural blog post (EN + ES, drafted in user's voice — humanize if AI-assisted)
- [ ] Commercial wording sweep across `messages/en.json` + `messages/es.json`
- [ ] In-app license-copy rebrand (EN + ES)
- [ ] Repo doc updates (README, THIRD_PARTY_LICENSES, SECURITY, ISSUE_TEMPLATE, PR_TEMPLATE)
- [ ] Apply for Microsoft Store dev account if affordable ($19 — Path B prep), or skip (Path A default)
- [ ] Set up Discord server (channels: #welcome, #insider-only, #devlog, #bug-reports, #feature-requests, #showcase)
- [ ] Cut a tagged GitHub Release `v2.1.0` ("Open up") via the existing CI release flow

### Days 8-14 (Store submission + remaining landings + soft launch)

- [ ] Submit to Microsoft Store (if Path B activated) — appxmanifest + signed MSIX (Path B requires MTS subscription kicked off)
- [ ] Address Store certification feedback (typically 1-3 rounds — runs in background)
- [ ] Ship remaining 2-3 themed landing pages (Accessibility / Developers / Multilingual)
- [ ] Mobile SEO/UX polish (tap targets, OG cards, structured data)
- [ ] Set up Meta Ads pixel events for landing-page funnels
- [ ] Founding Insider keys gifted to gaming-community betas (10-15 keys via `LicenseGiftForm`)
- [ ] First weekly devlog stream

### Critical-path order (must-finish-first)

1. Key rotation (P0 BLOCKER for public repo)
2. Code change to open the gates
3. Manual test plan re-key + run (gates Store submission)
4. Repo lockdown for public posture (gates public repo)

Everything else parallelizes.

### Slip buffer

If Store certification takes longer than expected, **GitHub Releases is already the primary distribution channel** — Store is additive, not gating. Launch can proceed via GitHub Releases on Day 7-14 regardless of Store status.

---

## 13. Manual Test Plan Integration — Gate to Store Submission

The `MANUAL_TEST_PLAN.md` completion is the explicit gate to Microsoft Store submission AND the GitHub Release tag. This blueprint integrates the test plan into the launch sequence rather than running them in parallel.

### Current state (per MANUAL_TEST_LOG.md, 2026-04-23 EOD)

- 1256 unit tests passing
- Bug roster: zero P0/P1 open
  - All BUG-013 through BUG-024 → FIXED
  - BUG-027 / BUG-028 / BUG-030 / BUG-031 / BUG-032 / BUG-033 / BUG-034 → FIXED
  - BUG-021 / BUG-025 → WONTFIX (cosmetic / design intent)
  - BUG-035 → Open (low-repro): CP invisible on launch — observed once after rebuild, workaround exists (Settings > Bar Position resets), not a launch blocker
- Manual journey progress: substantial work done on Journeys 1, 3, 7; Journeys 2, 4, 5, 6 mostly unchecked
- User estimate: 1-2 focused days to complete remaining manual testing (revised down from doc's 22-32 hours)

### Re-keying required for the new model

Manual test plan has wording assuming the old "Power License gates BYOK/Local" model. Specific lines need updating — see Appendix C for the diff. High-level:

- §1.1.3 "Wallet is default and only enabled option. BYOK/Local visible but disabled" → **"All three options enabled by default"**
- §1.1.3a "Click 'I Have a Key!' → Activation page" → reframe button as "Already an Insider?" or remove if button is removed
- §1.1.3b "Features page shows Power License benefits" → "Insider supporter benefits"
- §1.1.4 "BYOK (enabled with Power License)" → just "BYOK"
- §1.1.13 "Local (enabled with Power License)" → just "Local"
- §6.5 "License Activation (LemonSqueezy)" → "Insider License Activation" — keep all activation/validation/grace tests
- §6.5.6 "Without license → Wizard shows BYOK/Local as disabled" → invert: "Without license → all wizard options enabled, Insider section shows benefits + activation entry point"
- §6.5.7 "With license → Wizard BYOK/Local options enabled" → no longer relevant; remove

### Priority running order

Run journeys in this order to clear the launch gate fastest:

1. **Journey 1** (Cloud Deepgram + LLM) — highest regression risk
2. **Journey 3** (Local Whisper + Ollama) — second-highest
3. **Journey 6** (Wallet + License + Auth) — verify Insider rebrand + Wallet orthogonality
4. **Journey 5** (Settings) — comprehensive settings tabs check
5. **Journey 4** (Hybrid Skip LLM) — fast
6. **Journey 7** (TTS) — fast
7. **Journey 2** (Gemini Audio) — preview-only path, lowest risk
8. **Cross-cutting** (themes, CP, Vision, auto-update, streaming, errors)

### Exit criterion for Store submission

> **Store / GitHub Release submission only after Journeys 1, 3, 6 are 100% green AND zero open critical/high bugs.**

Journeys 2, 4, 5, 7 + cross-cutting can run in parallel with Store certification wait — they're not gates.

### What "100% green" means

- Every checkbox in the journey checked
- No bugs introduced into MANUAL_TEST_LOG.md bug table
- Settings persistence verified
- Restart-after-config verified
- No silent failures (toasts surface errors per BUG-015 fix)

### Post-pivot regression test priority

After applying the §8 code change, the immediate manual test priorities are:

1. **Wizard fresh run, no license** — verify all three radios (Wallet / BYOK / Local) enabled, all three paths complete successfully
2. **Settings → Account** — verify Insider rebrand, no broken bindings
3. **License activation** — paste valid LemonSqueezy key, verify still activates, verify Insider channel toggle appears
4. **Update channel toggle** — toggle Insider channel on/off, verify Velopack respects the flag
5. **Wallet path unchanged** — Wallet still works, no regression

These 5 should be smoke-tested first before running full Journey 1.

---

## 14. Launch Sequence — Day 0 to Day 14

Day-by-day timeline once Days 1-7 of the pre-launch checklist (§12) are complete. "Day 0" = first day everything goes public.

### Day 0 — Announce

- [ ] Inaugural blog post goes live (EN + ES)
- [ ] Pricing page goes live with new model
- [ ] /community page live
- [ ] Roadmap page restructured
- [ ] First 1-2 themed landing pages live
- [ ] Tweet / LinkedIn / FB post linking to blog post + landing pages
- [ ] Discord invite shared with gaming-community betas (private channel)
- [ ] First 10-15 Founding Insider keys gifted via `LicenseGiftForm`
- [ ] GitHub repo confirmed public with all docs in place

### Day 1 — GitHub Release

- [ ] Tag `v2.1.0` ("Open up") via existing CI release flow
- [ ] Verify Velopack auto-update flow on a test machine
- [ ] Submit MSIX to Microsoft Store (if Path B/C activated)
- [ ] Public-facing download link on landing pages goes live

### Day 2-3 — First feedback wave

- [ ] First weekly devlog stream goes live (Twitch/YouTube — platform locked by Day 7 of pre-launch)
- [ ] Monitor Discord, address founder feedback
- [ ] Triage any bug reports — fast turnaround on critical issues
- [ ] First Insider patch ship if any quick fixes land (sets the cadence tone)

### Day 4-7 — First ad test

- [ ] First Facebook ad budget allocated ($20-50) targeting one landing page theme
- [ ] Track via Meta Pixel: page views, CTA clicks, downloads
- [ ] Monitor download attribution via UTM params
- [ ] Watch for first unsolicited mention (HN, Reddit, X) and amplify if it happens

### Day 8-14 — Iterate

- [ ] Iterate landing-page copy based on Facebook ad performance (highest CTR theme = winner)
- [ ] Ship second themed landing page if Day 4-7 data warrants
- [ ] First model-benchmark blog post (Insider-tagged)
- [ ] Second weekly devlog stream
- [ ] Triage any sustained issues into v2.1.1 patch ship if needed

### Things that should NOT happen in Day 0-14

- Don't add new features (focus is launch, not scope creep)
- Don't promise plugins yet (Month 2+ commitment)
- Don't engage in flame wars on HN / Reddit (let users speak for the tool)
- Don't change pricing in the first 14 days regardless of conversion (need signal time)
- Don't start a 2nd product (focus, focus, focus)

### Channel priority matrix (heritage from LAUNCH_WEEK_TIMELINE.md)

When and where to post during launch week:

| Channel | When | Purpose | Effort |
|---------|------|---------|--------|
| **Product Hunt** | Day 0 | Initial burst, credibility | High (1 full day) |
| **Hacker News (Show HN)** | Day 0 | Developer audience, backlinks | Medium (1 post + reply duty) |
| **LinkedIn** | Daily Week 1, 2-3x ongoing | Professional audience, builder story | Medium |
| **X / Twitter** | Daily Week 1, 3-5x ongoing | Tech community, real-time engagement | Low per post |
| **Reddit** | Days 0, 1, 3, 4, 5 (spread across subs) | High-intent niche communities | Medium |
| **YouTube** | Day 3 + monthly | SEO, tutorials, trust-building | High per video |
| **IndieHackers** | Day 4 | Builder community, revenue transparency | Low |
| **dev.to / Hashnode** | Day 3 or Week 2 | Technical audience, SEO | Medium |

Reddit subs (per heritage research): r/selfhosted, r/LocalLLaMA, r/productivity, r/artificial, r/Windows11, r/accessibility — each posted once spread across the week. **Never spam — 1 post per sub maximum during launch week.**

### Product Hunt Day 0 mechanics (CRITICAL — heritage from PH_LAUNCH_PLAN.md)

> **PH resets at 12:01 AM PST. The first 4 hours (12:01-4:00 AM PST) carry the heaviest algorithmic weight. Be active by 12:01 AM PST, not 6 AM CT.**

PH algorithm research (heritage):
- Only **10% of launches get featured** since Jan 2024 — must spike on Useful, Novel, High Craft, or Creative
- **1 quality comment ≈ 40-50 upvotes** in algorithm weight — comments are king
- Established PH accounts count ~10x more than new ones
- Anti-gaming: vote spikes, new accounts, direct PH links trigger spam detection

Day 0 timing (PST):
| Time | Action |
|------|--------|
| 11:30 PM (night before) | Confirm listing live, verify all links |
| 12:01 AM | Maker first comment posted IMMEDIATELY |
| 12:01-1:00 AM | Email list send (product link, NOT PH link). X/Twitter announcement. DM 10-15 closest supporters personally. |
| 1:00-4:00 AM | **Highest-weight window** — respond to every comment within 10 minutes |
| 4:00-8:00 AM | LinkedIn (EN). Show HN post. |
| 8:00 AM-12:00 PM | LinkedIn (ES). r/selfhosted + r/LocalLLaMA. |
| 12:00-6:00 PM | r/productivity. Continue responding to ALL PH comments. |
| 6:00 PM-midnight | Final sweep. Compile Day 0 numbers. |

Day 0 rules:
- Respond to EVERY comment on PH and HN. Speed > volume.
- Be honest about limitations (Windows-only, solo dev). Heritage frames this as a feature.
- Don't share direct PH voting links. Share `dikta.me`.
- Never ask for upvotes. Ask people to "try it and share your experience."
- If a critical bug surfaces: fix, rebuild, update release, post "just shipped a fix" on PH (this is positive social proof, not embarrassment).

### Daily content engine (Days 4+, heritage from FACE_INSTA.md)

After launch burst, shift to a sustainable cadence built around **15 min/day blog→social repurposing**:

**Daily flow:**
1. Publish blog post (LinkedIn + Substack) — already happening via `news-run` → `news-writer` → `news-publish`
2. Extract sharpest hook + 1-2 key points (5 min)
3. Create one IG format: carousel, quote card, or Reel voiceover (5 min in Canva/CapCut)
4. Write FB version: hook + 2-3 paragraph opinion + CTA (5 min)
5. Schedule both via Meta Business Suite

**News-to-dIKta.me bridge** (every AI/tech story is a positioning opportunity):

| News topic | dIKta.me angle |
|------------|----------------|
| AI model releases | "New model? dIKta.me already supports it. Model-agnostic." |
| Subscription price hikes | "Another AI tool raised prices. dIKta.me: free forever. Optional supporter." |
| Privacy/data scandals | "Your voice on their servers, or on your hardware. Your call." |
| Local AI advances | "Runs on your laptop. dIKta.me had it built in from day one." |
| Voice AI news | "Voice is the future input layer. We've been saying this." |
| Productivity tool launches | "Another subscription. Or one app that does it all." |

**Weekly content mix** (per FACE_INSTA.md):

| Day | Instagram | Facebook | Source |
|-----|-----------|----------|--------|
| Mon | Reel: workflow demo | Same video + longer caption | Original |
| Tue | Carousel: news hot take | Text + image: news commentary | Blog repurpose |
| Wed | Story: dev update / poll | Link post: Substack article | Blog repurpose |
| Thu | Reel: feature spotlight | Text + image: pain point hook | Original |
| Fri | Carousel: educational | Text post: build-in-public | Original |
| Sat | Story: casual / news reaction | — | Light repurpose |
| Sun | Rest (or carousel if great story) | — | — |

**Bilingual strategy** (heritage):
- Feed posts: primarily EN (wider reach), ES versions 1-2x/week as separate posts
- Stories: alternate EN/ES, quick text overlays easy to redo
- Reels: EN voiceover/captions, add ES caption track via CapCut or IG auto-captions
- FB: EN primary, ES pinned-comment on key posts or alternate-day if engagement warrants

**Hook bank** (rekeyed for new model):
- "You type at 40 WPM. You think at 400."
- "I built this because I was tired of typing at AI models."
- "Free forever. Open source. Fund the build."
- "8 hotkeys. 8 superpowers. Free."
- "Hablamos inglés. We speak Spanish. Same hotkey."
- "Your AI. Your hardware. Nothing leaves your machine."
- "Everyone's racing to make AI smarter. Nobody's fixing how you talk to it." (HAI angle from SOCIAL_HAI_POST.md)

**Tools** (heritage):
- Meta Business Suite (free) — schedule + manage FB + IG
- Canva (free) — carousels, quote cards
- OBS or ShareX (free) — screen recordings
- CapCut desktop (free) — edit Reels, captions

**Time budget**: ~15 min/day blog repurposing + ~15 min engagement = 30 min/day total. Sunday batch session (1-2h) for original Reels/carousels for the week.

**Visual style** (heritage from BRAND_BOOK.md + FACE_INSTA.md):
- Background: `#0A0918` (Midnight) — matches app, reads premium on feeds
- Accent: `#C55A28` (Brand Orange)
- Font: Inter, sentence case, white on dark
- Screen recordings: Midnight theme, clean desktop, no personal files visible
- No stock photos. Real screenshots, real recordings.
- Aspect ratios: 1:1 feed, 9:16 Reels/Stories, 4:5 carousels
- Logo: `diktame_SVG_002.svg` for profile pics, `diktame_SVG_003.svg` for watermarks

---

## 15. The 6-Month Experiment — KPIs & Checkpoints

Hard checkpoints — pre-committed. Without these, "fit-to-market experiment" becomes "I just keep working on it."

### Day 30 — Stickiness signal

**KPI**: Beta tester daily-use rate ≥50% (of the 10-15 founders, are at least half using it daily?)

**Pass**: continue. Tool is sticky enough to merit pushing distribution.
**Fail**: tool-fit problem, not licensing problem — pause Store submission, investigate. Don't burn Path B/C money on a tool nobody uses daily.

**Secondary metrics to watch (informational only at Day 30)**:
- Total GitHub Release downloads
- Discord active member count
- Number of bug reports per active user
- Average daily session length per active user (if measurable)

### Day 60 — Distribution signal

**KPI**: ≥200 cumulative GitHub Release downloads in first 30 days post-launch (or post-Store-launch if Store delayed beyond Day 14)

**Pass**: continue. Distribution mechanic is working — landing pages or organic word-of-mouth is producing downloads.
**Fail**: distribution mechanic broken, not price — fix landing pages or Facebook targeting. Don't blame conversion.

**Activation triggers for Store Path B/C re-evaluated here** (any 2 of 5 from §7):
- >500 cumulative downloads
- >10 unsolicited Insider purchases
- Discord >50 active
- ≥1 unsolicited public review
- >50 founder feedback messages

If 2+ trigger, fast-track Path B (~$140/yr). If 4+, jump to Path C ($320/yr).

### Day 90 — Conversion signal

**KPI 1**: Insider conversion rate among users with ≥5 dictations: >2% = on track, >5% = strong signal
**KPI 2**: $200-500/month Insider revenue (the "massive success" bar)

**Pass**: scale (more landing pages, more ad spend, more devlog content)
**Fail**: tool not loved enough OR Insider value not compelling — diagnose which:
- If GitHub stars + downloads + Discord activity all healthy but conversion low → Insider value framing weak. Iterate Insider tier copy/contents.
- If everything low (downloads, stars, Discord, conversion) → tool not loved. Different problem entirely.

**Hard decision question revisited (from §5)**: at Day 90, evaluate revenue split between Insider and Wallet:
- Insider > 3× Wallet → continue current framing
- Wallet > 3× Insider → consider pivoting brand to managed-cloud-first, free-local-mode positioning. Don't pre-commit, but document the data.

### Day 180 — Decision point

**Three possible outcomes**:

1. **KPIs hit at Day 90 + sustained at Day 180**: continue, possibly invest more (more landings, larger ad budget, plugin push)
2. **KPIs missed but trajectory positive**: hold steady, ship plugins, iterate copy. Re-evaluate at Day 270.
3. **KPIs missed flat or declining**: exit — sell the app to a small acquirer, archive the project, or pivot to next product. Take learnings forward.

**Open question to document at Day 180**:
> Was the experiment failure (or success) about the *tool*, the *model*, or the *distribution*?

This is the critical learning. A flat experiment can mean any of three things, and which one shapes the next product:
- Tool failure → pivot product idea
- Model failure → keep tool, change pricing/positioning
- Distribution failure → keep tool + model, fix marketing

### KPI table (single-page summary)

| Day | Metric | Pass | Fail action |
|-----|--------|------|-------------|
| 30 | Founder daily-use rate ≥50% | Continue | Pause Store, investigate tool-fit |
| 60 | ≥200 cumulative downloads | Continue + maybe Path B | Fix landing pages / Facebook |
| 90 | Insider conversion >2% AND $200+/mo | Scale | Diagnose tool vs value vs distribution |
| 180 | Sustained Day 90 metrics | Continue or sell | Sunset / pivot |

### Launch-week success metric tiers (heritage from LAUNCH_WEEK_TIMELINE.md)

Targets for the first 7 days of launch (used to calibrate expectations and plan PH push intensity):

| Metric | Conservative | Moderate | Strong |
|--------|-------------|----------|--------|
| Downloads | 50 | 200 | 500+ |
| GitHub stars | 20 | 100 | 300+ |
| PH rank (if launched on PH) | Top 10 daily | Top 5 daily | #1-2 daily |
| Insider licenses sold *(rekeyed from "Full Versions sold")* | 5 | 20 | 50+ |
| HN front page | No | Briefly | Yes, sustained |

PH realistic target (heritage research): **300-600 upvotes** for a Windows-first solo-dev launch. Wispr Flow set the ceiling at 2,095 upvotes with Mac-first + $700M valuation; we're a different fish.

### Multi-launch playbook (Days 90+, heritage from PH_LAUNCH_PLAN.md)

If the experiment hits Day 90 KPIs, **don't do a single Product Hunt launch — do a series**. Wispr did this 3x. Different category each time = fresh audience exposure without spam:

| # | Timing | Angle | PH Category |
|---|--------|-------|-------------|
| 1 | Day 0 (initial launch via blueprint §14) | The Windows-native AI dictation tool, free + Insider supporter | AI Dictation Apps |
| 2 | Day ~90-120 | "Flash" — a free voice-to-cursor utility (subset of dIKta.me, narrower hook) | Productivity |
| 3 | Day ~120-180 | "Now replaces Grammarly too" — feature highlight push (Refine + Translate) | Writing Tools |
| 4 | Day ~180-240 | "Builder story: non-engineer shipped a 1,259-test Windows app with AI coding tools" | Open Source / Builder Stories |

Rules (heritage):
- Minimum 3 months between launches
- Different category each time
- Established maker account from launch 1 carries reputation forward
- Each launch gets its own PH "Coming Soon" page 4-6 weeks ahead
- Skip if Day 90 KPIs are flat — multi-launch only amplifies a working signal, doesn't rescue a missing one

---

## 16. Founding Insiders & Advocacy Mechanics

Two distinct mechanics, two distinct purposes. **Don't blur them.**

### Founding Insiders (10-15)

**Who**: gaming-community betas. The 10-15 people the user already has lined up.
**What**: Free lifetime Insider license. Permanent "Founding Insider" badge in Discord.
**Why**: loyalty signal. They tested the tool when it was rough, they get permanent recognition.
**How**: gift via `website/app/[locale]/hqbackstage/licenses/page.tsx` LicenseGiftForm. Include personal note in Discord DM.

**Not earned via review** — earned via beta participation.

### Lifetime-for-public-review (100 slots)

**Who**: anyone who publishes a public, substantive review of dIKta.me (blog post, video, Reddit thread, X post, podcast mention)
**What**: Free lifetime Insider license
**Why**: advocacy flywheel. Trades license for distribution — the right tradeoff at zero-budget scale.
**How**:
1. User writes/publishes the review on a public platform
2. Submits via form on /community page (link, name, contact)
3. Admin verifies (manual; spot-check the post is real and substantive)
4. Admin gifts via LicenseGiftForm
5. Cap at 100 slots; close when filled

**Substantive** = at least 200 words OR ≥5min video OR equivalent Reddit/X thread with engagement. Not "saw a thing, link." If unclear, default to giving the license — the marginal cost of one Insider key is zero, the goodwill cost of refusing isn't.

### Why two categories, not one

- Founders are loyalty. They came before there was anything to advocate for.
- Reviewers are advocacy. They're trading public visibility for access.
- Different rewards, different mechanics, different recognition.
- Lumping them together would be cleaner ops but worse storytelling. The first 10-15 deserve the special label.

### Discord recognition

- #welcome channel: pin the Founding Insiders by name
- Custom Discord role for "Founding Insider" with unique color
- Public-review claimers get a different role: "Insider" (no founder badge)
- Both roles see #insider-only

### What this is NOT

- Not "free license for everyone who signs up first" (that's a typical launch promo and attracts wrong audience)
- Not "free if you tweet about it" (low-effort tweets aren't substantive; this isn't a referral program)
- Not "free if you're influential" (we're not bribing influencers)

---

## 17. Risk Register & Mitigations

| # | Risk | Likelihood | Impact | Mitigation |
|---|------|------------|--------|------------|
| 1 | Microsoft Store rejection / delay | Medium | Low | GitHub Releases is primary; Store is additive. Never block launch on Store. |
| 2 | $300 IV cert unaffordable | High (initially) | Low | Start on Path A (free, unsigned). Activate Path B (~$140/yr, MTS) when traction triggers fire. Path C (IV cert ~$300/yr) only if Day 60 numbers are off-the-roof. **EV is not an option** — solo individual, not a registered company; only IV is available to the dev. |
| 3 | Insider conversion <1% | Medium | Medium | Diagnostic clarity wins — at Day 90 you know whether it's tool, value, or distribution. Pre-commit to the experiment ending at Day 180 if KPIs flat. |
| 4 | Solo bandwidth — promised cadence breaks | Medium | High | Promise **2** stable releases/year (under-promise). For Insider, batch-ship "small things" (theme tweak, model adapter, prompt update, plugin alpha) when big releases lag. Aim for 1 visible Insider ship every 2-4 weeks minimum. |
| 5 | Facebook ads burn budget with no ROAS | High | Low | Cap at $50/test. If first 3 themed landing pages × $50 each = $150 total spent with no signal, pause ads and rethink (organic distribution only). |
| 6 | Discord ghost-town | Medium | Low | Founders pinned in #welcome. First 10 messages from solo dev. Set the tone. If still empty by Day 30, accept that community might not happen — Discord stays open as a low-key channel, not a flagship. |
| 7 | Old paying customers (none currently — confirmed) | None | None | N/A — confirmed via product memory. No grandfather story needed. |
| 8 | Repo public exposes secrets | High (currently — `.env.local` has live keys) | Critical | Run key rotation BEFORE going public — explicit P0 in Days 1-2 of §12 checklist. CI gitleaks gate already enforces no commits with secrets going forward. |
| 9 | Store dev account ban risk from "donation" framing | Low | High | Use "Insider supporter license" wording — never "donation." LemonSqueezy product is a regular paid SKU, compatible with Store policies. |
| 10 | Fork ships before official Day 0 | Low | Low | Repo is already MIT and source-available. Anyone can technically build today. The canonical maintainer status (signed builds, .me domain, dev's name) is the real moat. Forks at this stage are evidence of interest, not a threat. |
| 11 | Velopack delta updates fail mid-launch | Low | High | Test the update path on a clean machine before tagging v2.1.0. Have rollback plan: if delta corrupts user installs, ship a full Setup.exe and message users to reinstall (only viable if user count <100). |
| 12 | Spike in support requests overwhelms solo dev | Medium | Medium | Pin a "Support FAQ" in Discord. Direct most issues to GitHub Issues (asynchronous). Set explicit "best-effort response time" in CONTRIBUTING.md and SECURITY.md. |
| 13 | LemonSqueezy outage during launch | Low | Medium | Document fallback: if LemonSqueezy down >24h, README + pricing page show "Insider purchases temporarily unavailable, free download still works." |
| 14 | Apple Silicon / non-Windows users complain about Windows-only | Constant | Low | Already Windows-only by design. Pin in README. Consider future Mac/Linux as a separate experiment if Windows traction strong. |
| 15 | Bug surfaces post-launch that affects all users | Medium | High | Hot patch via GitHub Release v2.1.1, ship within 24-48 hours of report. Velopack delta update flow is the safety net — it works without user action. |
| 16 | Imposter syndrome derails dev pace mid-experiment | Possible | High | Frame as "indie builder using AI to ship a Windows app," not "developer streaming code." Match framing to reality. Founder community is the support system. Take a week off and resume if needed — the experiment is 6 months long. |
| 17 | Plugin architecture proves harder than estimated | Medium | Medium | First plugin is **Connectors** (already in progress per existing roadmap). Don't start from scratch. If Month 2 plugin gating proves complex, defer the `RequiresInsider` flag to Month 3 — Insiders still see early continuous releases meanwhile. |
| 18 | PH spam-filter triggers on launch | Low | High | Heritage rules: never share direct PH voting links (share `dikta.me`), never ask for upvotes, no new burner accounts vouching, established accounts only. Vote spikes from suspicious sources downrank. |
| 19 | Low PH comment volume | Medium | High | Heritage: 1 quality comment ≈ 40-50 upvotes algorithmic weight. Mitigations: substantive maker first comment + 10 pre-written reply templates (in `plans/mkt/LAUNCH_CONTENT.md` §2 — needs rekey before reuse) + respond <10 min during 12:01-4:00 AM PST window. |
| 20 | SmartScreen blocks installer on launch day | High (without signing) | Medium | Heritage mitigation: document workaround on download page with screenshots + FAQ. Show "More info → Run anyway" path. This is the cost of Path A (no signing) — accept it for launch unless Path B/C is funded. |
| 21 | Mac-dominated PH audience ignores Windows-first | Medium | Medium | Heritage: heavy Reddit/HN crossposting compensates. Frame Windows-first as deliberate (underserved market — see §2 Windows-native gap). r/Windows11 + r/selfhosted skew Windows-favorable. |
| 22 | Critical bug surfaces during Day 0 PH window | Medium | High | Heritage: test on 3+ clean machines pre-launch. Hotfix branch ready. Velopack delta update lets users get the fix without reinstall. Post "just shipped a fix" on PH — heritage frames this as positive, not embarrassment. |
| 23 | Website traffic spike from PH/HN | Low | Medium | Verify Vercel handles 10K visitors/day before launch. Static fallback ready. dikta.me already on Vercel — auto-scaling handles burst. |

---

## 18. Exit Strategy — What "Done" Looks Like

### Best case (Day 180 onward)

- $500+/mo sustained Insider revenue
- Growing user base (>1000 active installs)
- Discord active (>100 weekly active members)
- 3-5 plugins shipped, more queued
- Public reviews trickling in regularly

**Action**: continue 6 more months. Evaluate at Day 365 whether to:
- Scale further (raise budget, hire light-touch contractor for design/marketing)
- Sell to a small acquirer (initiate conversations with adjacent indie tool companies)
- Hold steady as a portfolio asset (low-touch maintenance, $500-1000/mo passive)

### Median case (Day 180 onward)

- $100-300/mo Insider revenue
- Small loyal community (~50-100 active)
- 2-3 plugins shipped
- Word-of-mouth in niche communities (gaming, accessibility) but not breakout

**Action**: hold steady. Treat as portfolio asset. Ship plugins as the dev has time. Keep cadence promise. Re-evaluate at Day 365.

### Worst case (Day 180)

- <$50/mo, no community traction
- Day 30/60/90 KPIs all missed flat
- No public reviews
- Discord empty

**Action**: sunset gracefully:
1. Ship a final stable release (v2.2.0 or whatever number applies)
2. Update README with archived status: "dIKta.me V2 was a 6-month fit-to-market experiment. The experiment concluded on [date]. The app is feature-complete and works as designed; no further updates planned. MIT-licensed, fork freely."
3. Refund any active Insider purchases from the last 30 days (good faith — small dollar amount, big goodwill)
4. Repo stays public for forks
5. Take learnings to next product

### What "exit" doesn't mean

- Doesn't mean deleting the repo
- Doesn't mean turning off the website
- Doesn't mean abandoning users — graceful sunset means the product still works
- Doesn't mean failure as a person — it means failure as a market fit, which is data, not identity

### Sale criteria

If a buyer approaches at any point:
- **Minimum acceptable offer**: $20K (~5 years of best-case revenue compressed to a year of dev focus)
- **Signal of seriousness**: NDA, term sheet, not just "what would you take?"
- **What's actually for sale**: brand, domain (dikta.me), code (already MIT, but with the canonical maintainer status), user list (with privacy notice + opt-out), Insider customer base, Discord community
- **What's NOT for sale**: dev's name, ongoing labor, Wallet customer list (separate transaction if buyer wants it)

---

## Appendix A — File-by-File Change Inventory

### Code (open the gates)

| Path | Lines | Action | Verify |
|------|-------|--------|--------|
| `src/DiktaMe.Core/Config/PipelineFactory.cs` | 281-284 | Delete `InvalidOperationException("Power License required...")` guard | New unit test `PipelineFactory_BuildsBYOKPipeline_WithoutLicense` passes; existing PipelineFactoryTests pass |
| `src/DiktaMe.App/Views/Wizard/WizardGetStartedPage.xaml.cs` | 69-79 | Remove `IsEnabled = licensed` on BYOK + Local radios | Manual: launch wizard fresh (no license), all radios enabled |
| `src/DiktaMe.App/Views/Wizard/WizardTtsPage.xaml.cs` | 59, 84-89 | Remove bounce-away-if-unlicensed branch | Manual: Local TTS path completes without license |
| `src/DiktaMe.App/Views/WizardWindow.xaml.cs` | 72 | Remove `HaveKeyButton.Visibility = !licensed` clause | Manual: button visible (or repositioned per UI decision) |

### Code (repurpose semantically)

| Path | Action | Verify |
|------|--------|--------|
| `src/DiktaMe.Core/Security/LicenseManager.cs` | Update XML docs: "Power" → "Insider" | XML doc review |
| `src/DiktaMe.App/ViewModels/Settings/AccountSettingsViewModel.cs` | Rebrand UI strings (EN + ES) | Settings → Account renders new copy |
| `src/DiktaMe.App/Views/Wizard/WizardActivatePage.xaml.cs` | Same rebrand (EN + ES) | Wizard activation page renders new copy |
| `src/DiktaMe.App/Services/UpdateService.cs` | Add `Updates.InsiderChannel` opt-in flag | `UpdateService_RespectsInsiderChannelToggle` test |
| `src/DiktaMe.Core/Config/AppSettings.cs` | Add `Updates.InsiderChannel` boolean (default false) | Settings serialization round-trip test |

### Code (leave alone)

| Path | Why |
|------|-----|
| `src/DiktaMe.Core/Data/WalletManager.cs` | Wallet is orthogonal |
| `src/DiktaMe.Core/Account/WalletGeminiProxy.cs` | Wallet is orthogonal |
| `src/DiktaMe.Core/LLM/LLMRouter.cs` | Wallet routing only |
| `src/DiktaMe.Core/STT/STTRouter.cs` | Wallet routing only |
| `src/DiktaMe.Plugin.Abstractions/PluginManager.cs` | Future Insider gating at Month 2 (separate change) |

### Tests

| Path | Action |
|------|--------|
| `tests/DiktaMe.Core.Tests/Security/LicenseManagerTests.cs` | Stay (62 tests, Insider plumbing same as Power plumbing) |
| `tests/DiktaMe.Core.Tests/Config/PipelineFactoryTests.cs` | Audit + invert any "throws without license" assertions |
| `tests/DiktaMe.Core.Tests/Account/AccountServiceTests.cs` | Stay |
| `tests/DiktaMe.Core.Tests/LLM/LLMRouterWalletTests.cs` | Stay |
| New: `PipelineFactoryTests` | +1 test: builds BYOK without license |
| New: `PipelineFactoryTests` | +1 test: builds Local without license |
| New: `UpdateServiceTests` | +1 test: respects InsiderChannel toggle |

### Website

| Path | Action |
|------|--------|
| `website/app/[locale]/pricing/page.tsx` | Full rewrite (§9.A) |
| `website/app/components/PricingSection.tsx` | Restructure cards (§9.A) |
| `website/app/[locale]/roadmap/page.tsx` | Add two-track view (§9.B) |
| `website/app/[locale]/community/page.tsx` | New file (§9.D) |
| `website/app/[locale]/{writers,gamers,accessibility,developers,multilingual}/page.tsx` | New files, 3-5 of these (§9.E) |
| `website/messages/en.json` | Wording sweep (§9.G) |
| `website/messages/es.json` | Wording sweep, mirrored ES |
| `website/app/components/HeroSection.tsx` | Single line: ctaVersion update |
| `website/app/[locale]/dashboard/page.tsx` | Display copy: "Free user" not "Free tier" (§9.H) |
| `website/app/[locale]/privacy/page.tsx` | Wording review (§9.J) |
| `website/app/[locale]/terms/page.tsx` | Wording review + Insider clause (§9.J) |
| `website/app/sitemap.ts` | Include new landing + community pages |
| Blog post (Supabase `blog_posts` table) | Inaugural post EN + ES (§9.C) |
| `website/.env.local` | **ROTATE keys, do NOT commit** (§11 P0) |

### Repo docs

| Path | Action |
|------|--------|
| `README.md` | Rewrite landing pitch + add source-available banner |
| `CHANGELOG.md` | Add v2.1.0 "Open up" entry |
| `LICENSE` | No change (already MIT) |
| `CONTRIBUTING.md` | No change required (already says "no PRs"); optionally add fork instructions |
| `THIRD_PARTY_LICENSES.md` | New file (optional) |
| `SECURITY.md` | New file (recommended) |
| `CODE_OF_CONDUCT.md` | New file (optional) |
| `.github/ISSUE_TEMPLATE/bug_report.md` | New file |
| `.github/ISSUE_TEMPLATE/feature_request.md` | New file |
| `.github/PULL_REQUEST_TEMPLATE.md` | New file (no-PRs notice) |
| `MANUAL_TEST_PLAN.md` | Re-key per Appendix C |
| `MANUAL_TEST_LOG.md` | Continue logging journeys; final pass before Store |

### Heritage marketing docs (referenced, not edited as part of launch)

These were marked with stale-banner headers on 2026-04-26 pointing back to this blueprint. Salvageable content has been folded into the blueprint sections noted; the source docs themselves remain in `plans/mkt/` for reference and for full-content reuse with §9.G wording sweep applied.

| Path | Stale banner added | Salvaged into | Pricing rekey required before reuse |
|------|--------------------|---------------|-------------------------------------|
| `plans/mkt/BRAND_BOOK.md` | No (canonical, not stale) | §10 voice/visual authority | No |
| `plans/mkt/BLOG_ROADMAP.md` | No (architecture current) | §9.C blog post pipeline | No |
| `plans/mkt/Carlos Fuentes Series.md` | No (blog content, no pricing refs) | §9.C blog editorial reference | No |
| `plans/mkt/PH_LAUNCH_PLAN.md` | Yes | §0, §2, §14, §15, §16, §17 | Yes for any direct copy reuse |
| `plans/mkt/LAUNCH_WEEK_TIMELINE.md` | Yes | §14 channel matrix + Day 0 mechanics; §15 success tiers | Yes (KPI rename) |
| `plans/mkt/COMPETITOR_PAGES.md` | Yes | §9.E themed landings get `/vs/<competitor>` companion pages | Yes ($20 once → free + Insider) |
| `plans/mkt/FACE_INSTA.md` | Yes | §14 daily content engine + visual style + tools + hook bank | Yes |
| `plans/mkt/SOCIAL_HAI_POST.md` | Yes (status: voice still current) | §9.C + §14 launch posts | No (no pricing in copy) |
| `plans/mkt/SOCIAL_W13_MAR24-30.md` | Yes | §14 evergreen post templates | Yes |
| `plans/mkt/LAUNCH_CONTENT.md` | Yes | §9.C manifesto draft basis + §14 feature post library | Yes (extensive) |
| `plans/mkt/mem_review.md` | No (analysis doc, no pricing) | §9.E (potential additional comparison page) | No |

---

## Appendix B — Website Component Inventory

Reusable building blocks (audit confirmed). All landing pages and new pages compose from these. **No new components required.**

### Layout

- `Container` — page-width wrapper
- `SectionHeading` — H2/subhead pair
- `Navbar` — top navigation
- `Footer` — bottom links

### Hero / CTA

- `HeroSection` — animated word carousel + CTA button
- `CtaSeparator` — two-variant text + button between sections
- `Button` — primary/secondary CTA component

### Cards / blocks

- `FeatureCard` — icon + title + description tile
- `GlassCard` — semi-transparent border container
- `PricingSection` — pricing cards layout (will be restructured per §9.A)
- `CoreArsenalSection`, `VersusSection`, `SpecsSection`, `BilingualSection`, `AskModeSection`, `QuickChatSection`, `TokensSection`, `VoiceMacrosSection`, `TTSSection` — homepage feature sections (existing, do not modify)

### Blog

- `NewsletterSignupBox` — embedded signup
- `BlogLanguagePills` — EN/ES toggle
- `BlogArchiveNav` — month/year filter
- `BlogPagination` — page controls
- `MarkdownRenderer` — for blog body

### Forms

- `WaitingListForm` — email capture
- `LicenseGiftForm` — admin tool, gift Insider keys to founders/reviewers
- `AvatarCropModal` — profile pic upload

### Modals

- `FeaturesModal` — features expandable view

### Tracking

- `MetaPixel` — Facebook Pixel init + PageView + ViewContent + custom events
- Vercel Analytics — automatic
- Vercel Speed Insights — automatic
- `lib/analytics.ts` — `trackCtaClick`, `trackFeaturesModalOpen`, `trackExternalLink` helpers

### Composition pattern for landing pages

```tsx
// website/app/[locale]/writers/page.tsx (example)
<>
  <Container>
    <HeroSection variant="writers" />
    <FeatureCard ... /> // 3-4 cards highlighting writer-relevant features
    <CtaSeparator cta="Download Free" href="https://github.com/.../releases/latest" />
  </Container>
</>
```

Each landing page is ~150-250 lines, mostly copy. No new components needed.

---

## Appendix C — Manual Test Plan Re-Keying Diff

Line-by-line patch for `MANUAL_TEST_PLAN.md` to align with the new free model. Roughly 25-40 lines of edits across §1.1, §1.2, §3.1, §6.5, with a smaller pass on §5.8 (Account) and the per-journey "BYOK Path (Licensed)" / "Local Path (Licensed)" sub-headers.

### High-impact changes (must apply before testing)

#### Section 1.1 — Wizard

**Old**:
```
### Wallet Path (Unlicensed)
- [x] **1.1.3** Step 1 (Get Started) → Wallet is default and only enabled option. BYOK/Local visible but disabled. "I Have a Key!" red button on left
- [x] **1.1.3a** Click "I Have a Key!" → Activation page → Paste key → Activates → Returns to Get Started with BYOK/Local enabled
- [x] **1.1.3b** (Without key) Click Next → Features page shows Power License benefits (Local AI, BYOK, Vision) + "Get yours now" link
```

**New**:
```
### Default Path (No License)
- [ ] **1.1.3** Step 1 (Get Started) → All three options (Wallet / BYOK / Local) enabled by default. "Already an Insider?" button visible
- [ ] **1.1.3a** Click "Already an Insider?" → Activation page → Paste Insider key → Activates → Returns to Get Started with Insider channel toggle visible in Settings
- [ ] **1.1.3b** Click Next without activating → Features page shows Insider supporter benefits (continuous releases, Discord, devlog stream, plugins) + "Become an Insider" link
- [ ] **1.1.3c** Click Next on Features → proceeds to STT/LLM/TTS without OAuth requirement (free path)
```

#### Section 1.1 — BYOK header

**Old**: `### BYOK Path (Licensed)` → `### BYOK Path`
**Old**: `- [x] **1.1.4** Step 1 (Get Started) → Select **BYOK** (enabled with Power License)` → `- [ ] **1.1.4** Step 1 (Get Started) → Select **BYOK** (enabled by default)`

#### Section 1.1 — Local header

**Old**: `### Local Path (Licensed)` → `### Local Path`
**Old**: `- [x] **1.1.13** Step 1 (Get Started) → Select **Local** (enabled with Power License)` → `- [ ] **1.1.13** Step 1 (Get Started) → Select **Local** (enabled by default)`

#### Section 6.5 — License activation

**Old**: `## 6.5: License Activation (LemonSqueezy)`
**New**: `## 6.5: Insider License Activation (LemonSqueezy)`

**Old**: `- [ ] **6.5.1** Settings → Account → Power License section visible`
**New**: `- [ ] **6.5.1** Settings → Account → Insider License section visible`

**Old**: `- [ ] **6.5.6** Without license → Wizard shows BYOK/Local as disabled with info text, Wallet is default`
**New**: `- [ ] **6.5.6** Without license → Wizard shows all three options (Wallet/BYOK/Local) enabled, Insider section shows benefits + activation entry point`

**Old**: `- [ ] **6.5.7** With license → Wizard BYOK/Local options enabled and selectable`
**New**: REMOVE (no longer relevant — they're enabled with or without license)

#### Section 5.8 — Account tab

**Old**: `- [ ] **5.8.1** Settings → Account → Verify Power License section visible`
**New**: `- [ ] **5.8.1** Settings → Account → Verify Insider License section visible`

**Old**: `- [ ] **5.8.2** If unlicensed: verify "Buy" button + license key TextBox + "Activate" button`
**New**: `- [ ] **5.8.2** If not an Insider: verify "Become an Insider" button + license key TextBox + "Activate" button`

### New tests to add (post-pivot regression)

After §1.1 (wizard tests), add:

```
### Insider Channel Opt-In
- [ ] **1.1.46** Activate Insider license → Settings → Updates → "Insider release channel" toggle appears
- [ ] **1.1.47** Toggle Insider channel ON → restart app → UpdateService fetches prerelease feed (verify in logs: "Velopack prerelease=true")
- [ ] **1.1.48** Toggle Insider channel OFF → restart app → UpdateService fetches stable feed only
- [ ] **1.1.49** Deactivate Insider license → Insider channel toggle hides; if was ON, defaults to stable feed
```

### Test scenarios that NO LONGER APPLY (delete)

- Any "without license, BYOK is disabled" assertion
- Any "without license, Local is disabled" assertion
- Any "without license, local TTS bounces user away" assertion

### Estimated edit count

- §1.1: ~12 line edits
- §1.2: ~2 line edits (raw mode wording, no impact)
- §3.1: ~2 line edits (header rename)
- §5.8: ~4 line edits
- §6.5: ~6 line edits + 1 deletion + 4 new lines
- New §1.1.46-49: 4 new lines

**Total: ~30 line edits, 4 new lines, 1 deletion.** Single commit.

---

## Appendix D — Decision Log

### The four-turn debate (2026-04-26)

**Turn 1 — Strategic question raised**
> User: "What if I go completely free, only locking updates behind a paywall? Free forever or pay $20+ for fast updates?"

**Turn 2 — Initial pros/cons exchange**
- Pros surfaced: defensible story, word-of-mouth velocity, OSS contribution surface (deferred), Microsoft Store optionality, less enforcement overhead, aligned incentives
- Cons surfaced: envy gap disappears, pricing power compresses (LTV cap at $20), "stop releasing" reputational risk, fork threat, can't price-discriminate, Wallet positioning risk

**Turn 3 — User counter-points**
- Envy gap = the pivot itself (corpo vs free thinking)
- One-time $20 = no legal recurring obligation if dev pace stops
- Zero current conversion → no drop possible
- Wallet at breakeven; not a competitive sub-second product
- "Built without writing a line of code" = anyone could; not afraid of forks
- No existing paying customers to grandfather

**Turn 4 — Decisive frame**
> Sold by: *"Is the tool good enough that people will recommend it unprompted? If yes, free path wins decisively at your scale. If no, neither path works and the licensing model is irrelevant."*

**Turn 5 — Insider tier definition**
- User listed full Insider value bundle: bug fixes, plugin queue, weekly stream, Discord, blog, model benchmarks, collateral
- Insider gating moved to plugins (future safety net) — base app stays free even if business pivots

**Turn 6 — Treat as fit-to-market experiment**
- 10-15 gaming-community betas already lined up
- Willing to give 100 more away if right mechanism exists
- Exit strategy: sell, not maintain forever
- 3-6 month horizon, not 1+ year

**Turn 7 — Plan-mode Q&A locks final variables**
- Doc scope: full execution playbook (~1500-2500 lines)
- OSS posture: source-available, no contributions
- Store: parallel with GitHub Releases from Day 1
- Website: Pricing + Roadmap + /community + landing pages + blog post + commercial wording sweep
- Price: $20 minimum, pay-what-you-want
- Plugins: in scope, ship via Insider months 2-4
- Founders: 10-15 free + 100 lifetime-for-review
- Landings: 3-5 themed pages

**Turn 8 — Pre-launch timeline compressed**
- Manual test plan completion estimate: 1-2 focused days (revised from doc's 22-32 hours)
- Pre-launch window: 1-2 weeks

**Turn 9 — Wallet contingency + Store path B/C**
- Wallet revenue scaling levers documented (§5)
- Store distribution split into Path A/B/C with annual costs ($0/$140/$320) and trigger conditions (§7)
- Code signing ($300 IV cert — solo individual, EV not available) clarified as same line item as Store budget

### Counter-arguments considered and dismissed

| Counter-argument | Why dismissed |
|------------------|---------------|
| Feature gates produce predictable conversion | At zero current users, "predictable conversion of nothing" isn't a meaningful win |
| Subscription LTV ceiling is 5-10x higher than $20 | True, but operational complexity of subscriptions doesn't fit solo bandwidth; one-time payment matches dev's actual capacity |
| OSS forks could ship competing builds | Repo is already MIT; any fork risk exists today regardless of free/paid pivot. Canonical maintainer status is the moat. |
| Free might mean "ignored, not used" | True for some users; mitigated by themed landing pages + Founding Insider mechanic forcing focused use |
| "Donation" framing has tax/Store implications | Yes — that's why the term used is "Insider supporter license," a regular paid product |
| Recurring monthly subscription would scale better | Possibly, but adds operational complexity (refunds, churn, recurring billing). Defer to Day 90 review. |
| Insider tier needs a real "envy" mechanism for conversion | Continuous channel + plugin alpha + Discord + stream IS visible envy, just not feature-gate envy. Test the assumption empirically at Day 90. |

### Locked decisions

1. ✅ Free forever, MIT, source-available
2. ✅ Insider supporter license: $20 minimum PWYW
3. ✅ Wallet stays orthogonal, $1 onramp
4. ✅ Plugins ship via Insider months 2-4, no separate gate
5. ✅ 10-15 Founding Insider founders + 100 lifetime-for-review advocates
6. ✅ 3-5 mobile-first themed landing pages for Facebook funneling
7. ✅ Microsoft Store + GitHub Releases parallel (Plan A/B/C contingency)
8. ✅ Source-available, no contributions accepted (CONTRIBUTING.md already aligned)
9. ✅ 6-month experiment with Day 30/60/90/180 KPI checkpoints
10. ✅ Exit strategy: sell at Day 180+ if KPIs miss; otherwise hold or scale

### Open questions deliberately left for Day 90 review

- Should Wallet scale (top-up tiers, auto-top-up) if it crosses $200/mo?
- Should there be a recurring monthly Insider option ($5/$10/mo)?
- Should the brand pivot to "managed-cloud-first" if Wallet revenue dwarfs Insider?
- Should plugin architecture spawn a marketplace?
- Should Insider tier split (Insider vs Insider+) ever happen?

All answers depend on data we don't have yet. Don't pre-commit. Re-read this section at Day 90.

---

**End of blueprint.**

*Last updated: 2026-04-26 (initial creation + heritage assets fold-in)*
*Next checkpoint: Day 30 post-launch (target ~2026-05-30 if launch hits ~2026-04-30)*

*Heritage docs in `plans/mkt/` were audited and stale-banner-marked on the same date. Refer to them for full content; defer to this blueprint for canonical strategy.*
