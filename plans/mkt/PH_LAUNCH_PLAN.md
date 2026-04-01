# Product Hunt Launch Plan — dIKta.me

## Context
dIKta.me is a Windows-native AI voice dictation app (WinUI 3, C#) with 8 workflow modes, local-first architecture, $20 one-time pricing, and MIT open source. The PH AI dictation category is Mac-dominated (Wispr Flow at 2,095 upvotes, AudioPen at 873, Aqua Voice at 585) — no Windows-native tool has meaningful PH presence. This is the gap.

Existing marketing assets are strong (97KB launch content doc, brand book, competitor pages, 7-day social batch EN+ES, website live). What's missing: visual assets, pre-launch audience, and execution timing corrections.

---

## Gap Analysis: What Exists vs What's Missing

### Already Done
- `plans/mkt/LAUNCH_CONTENT.md` (97KB) — PH page draft, maker comment, Show HN, Reddit posts, 7-day social batch, video outlines
- `plans/mkt/LAUNCH_WEEK_TIMELINE.md` — Day-by-day execution T-7 through Day 7
- `plans/mkt/BRAND_BOOK.md` — Full brand guidelines
- `.agents/product-marketing-context.md` — Positioning, personas, objection handling
- `plans/mkt/COMPETITOR_PAGES.md` — vs Wispr Flow
- Website live with hero, features, comparison, pricing, FAQ

### Critical Gaps (P0)

| Gap | Why It Matters |
|-----|----------------|
| No animated GIF thumbnail (240x240) | Static thumbnails get 40-60% fewer clicks |
| No gallery images (1270x760, need 5-6) | PH gallery is the storefront |
| No 60-second demo video with captions | 70%+ of top launches have video |
| No email list / pre-launch audience | 400+ subscribers = 3-5x better odds for top 5 |
| Timeline starts T-7, needs T-8 weeks | No audience-building phase exists |
| Day 0 timing wrong | Current plan starts 6 AM CT (4 AM PST). PH resets at 12:01 AM PST — first 4 hours weighted heaviest |
| No PH "Coming Soon" page | Free follower collection feature, not leveraged |
| No comment reply templates | 1 quality comment ≈ 40-50 upvotes in algorithm weight |

---

## Key Research Findings

### Algorithm
- Only **10% of launches get featured** since Jan 2024. Must spike on Useful, Novel, High Craft, or Creative.
- Points ≠ votes. Established PH accounts count ~10x more than new ones.
- **1 quality comment ≈ 40-50 upvotes** in algorithm weight. Comments are king.
- First 4 hours (12:01 AM - 4 AM PST) weighted heaviest. Anti-gaming: vote spikes, new accounts, direct PH links trigger spam detection.

### Competitors
- **Wispr Flow**: 2,095 upvotes, $12-15/mo, cloud-only, Mac-first (launched Windows separately for 814 more upvotes)
- **AudioPen**: 873 upvotes, solo dev, "messy thoughts → clear text" positioning
- **Aqua Voice**: 585 upvotes, speed-focused
- All Mac-first. Windows is wide open.

### Positioning Gaps to Own
1. **Windows-native** — every top competitor is Mac-first
2. **AI post-processing pipeline** — competitors sell "accurate transcription," we have configurable LLM modes
3. **No subscription + local-first** — $20 once vs $15/mo cloud
4. **Dual local/cloud toggle** — competitors are one or the other

### Realistic Target: 300-600 upvotes

---

## Launch Date: Wednesday, April 15, 2026

## Pre-Launch Sprint (March 31 — April 14)

### Week 1: Assets (March 31 — April 6)
- [ ] **Mon Mar 31**: PH Coming Soon page live. Add email capture to dikta.me.
- [ ] **Mon-Wed (Mar 31 - Apr 2)**: Record + edit 60-second demo video with captions. Upload to YouTube.
  - 0-5s: Problem ("You type at 60 wpm. You think at 150.")
  - 5-20s: Dictate mode in VS Code
  - 20-30s: Refine mode in Word
  - 30-40s: Vision mode
  - 40-50s: Local AI toggle + privacy
  - 50-60s: $20 pricing + CTA
- [ ] **Wed-Fri (Apr 2-4)**: Produce 6 gallery images (1270x760) + animated GIF thumbnail (240x240)
  1. Hero: 8 modes, any app, any model
  2. Local vs Cloud toggle split-screen
  3. Pricing comparison ($20 vs $180/yr)
  4. Vision mode screenshot
  5. Architecture: STT→LLM→TTS pipeline
  6. Stats: 1,134 tests, MIT, sub-1.2s, $20
- [ ] **Daily**: LinkedIn/X build-in-public posts

### Week 2: Upload + Polish (April 7 — April 14)
- [ ] **Mon Apr 7**: Upload all assets to PH listing (GIF, gallery, video, description, links)
- [ ] **Mon-Wed (Apr 7-9)**: Final UI polish + text fixes in the app
- [ ] **Thu Apr 10**: Test trial signup on 2+ clean machines (Win 10 + Win 11)
- [ ] **Fri Apr 11**: Brief supporters via personal DMs. Share dikta.me (NOT PH link).
- [ ] **Sun Apr 13**: GitHub Release drafted. All social posts pre-written and ready to paste.
- [ ] **Mon Apr 14**: Final PH listing review. Set alarm for 11:30 PM PST / 1:30 AM CT.

### Skipped (do post-launch)
- ~~Hunter outreach~~ → self-hunt
- ~~Influencer seeding~~ → post-launch week 2+
- ~~Reddit account warming~~ → post day-of from existing accounts

---

## Launch Day Playbook

**CRITICAL**: Must be active by 12:01 AM PST. Not 6 AM CT.

| Time (PST) | Action |
|------------|--------|
| 11:30 PM night before | Confirm listing is live, verify all links |
| 12:01 AM | Post maker first comment immediately |
| 12:01-1:00 AM | Send email to list (product link, not PH link). Post X/Twitter. DM 10-15 closest supporters. |
| 1:00-4:00 AM | Monitor + respond to every comment within 10 min. This is the highest-weight window. |
| 4:00-8:00 AM | LinkedIn announcement (EN). Post Show HN. |
| 8:00 AM-12:00 PM | LinkedIn (ES). Post to r/selfhosted + r/LocalLLaMA. |
| 12:00-6:00 PM | Post to r/productivity. Respond to ALL PH comments. |
| 6:00 PM-midnight | Final sweep. Check for critical bugs. Compile Day 0 numbers. |

### Launch Day Rules
1. Respond to EVERY comment. Comments > votes in the algorithm.
2. Never share direct PH voting links. Share dikta.me.
3. Never ask for upvotes. Ask people to "try it and share your experience."
4. Be honest about limitations. Fix bugs live and post "just shipped a fix."
5. Ask questions back to commenters → drives threaded engagement.

---

## Maker First Comment (Revised)

The existing comment in LAUNCH_CONTENT.md needs restructuring. Key elements:

1. **Open with the problem** — $71/mo across 5 AI subscriptions, still copy-pasting
2. **Show the product** — hotkey in any app, 8 modes with 1-line descriptions
3. **Three numbered differentiators** — local (sub-1.2s), $20 once, MIT open source (1,134 tests)
4. **Honest backstory** — marketing exec from Mexico, not a software engineer, first desktop app
5. **Clear CTA** — try free at dikta.me, $20 for local AI
6. **Invite questions** — "I read every comment. Ask me anything."

---

## Tagline Options

1. **"Stop typing at your AI. Just talk."** — Direct, punchy (recommended primary)
2. **"The AI dictation app that runs on your hardware, not your credit card."** — Subscription fatigue angle (social media variant)
3. **"Local-first AI dictation. 8 modes. Any model. No subscription."** — Feature-dense alternate
4. **"Your voice to any AI, in any app, on your GPU."** — Technical/HN crossover

---

## Post-Launch (Days 1-7)

Existing LAUNCH_WEEK_TIMELINE.md covers this well. Additions:
- **Day 1**: "Thank you + real numbers" email. Post metrics publicly even if modest.
- **Day 2-3**: If feature requests appeared on PH, reply with "shipped it" for quick wins.
- **Day 4**: Indie Hackers post with revenue transparency.
- **Day 7**: "What I learned launching on Product Hunt" post (extends launch tail by weeks).
- **Days 8-30**: Monitor PH page for late comments for 2 full weeks.

---

## Multi-Launch Strategy (Wispr did this 3x)

| Launch | Timing | Angle | Category |
|--------|--------|-------|----------|
| 1. V2.0 Main | Current plan | Local-first AI dictation for Windows | AI Dictation Apps |
| 2. Flash (free tool) | T+3-4 months | Free voice-to-cursor for Windows | Productivity |
| 3. V2.1 Features | T+4-6 months | "dIKta.me now replaces Grammarly too" | Writing Tools |
| 4. Builder Story | T+6-8 months | Non-engineer built 1,134-test app with AI coding tools | Open Source |

Min 3 months between launches. Different category each time = fresh audience.

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| PH spam filter | Never share direct PH links. Never ask for upvotes. Established accounts only. |
| Low comments | Substantive maker comment + 10 pre-written reply templates + respond <10 min |
| SmartScreen blocks install | Document workaround on download page with screenshots + FAQ |
| Critical bug on launch day | Test on 3+ clean machines. Hotfix branch ready. Post "just shipped a fix" on PH. |
| Website traffic spike | Verify Vercel handles 10K visitors. Static fallback ready. |
| Mac-dominated PH audience | Heavy Reddit/HN crossposting. Frame Windows-first as deliberate (underserved market). |

---

## Critical Path Summary

| When | What |
|------|------|
| T-8 to T-6 | PH Coming Soon + email capture + community seeding |
| T-5 to T-4 | GIF, 6 gallery images, 60s demo video |
| T-3 to T-2 | Hunter outreach + beta testers + influencer seeding |
| T-1 week | Assets uploaded, Tuesday/Wednesday scheduled, reply templates ready |
| Day 0 | Execute playbook from 12:01 AM PST |
| Days 1-7 | LAUNCH_WEEK_TIMELINE.md + additions |
| Days 8-30 | Sustainable cadence + plan Launch 2 |

---

## Files to Update
- `plans/mkt/LAUNCH_CONTENT.md` — revise maker comment structure, add comment reply templates
- `plans/mkt/LAUNCH_WEEK_TIMELINE.md` — fix timing to midnight PST, add T-8 week pre-launch phase
- `website/` — add email capture component
- New: `plans/mkt/PH_LAUNCH_PLAN.md` — this plan as the canonical reference

## Verification
- Check PH Coming Soon page is collecting followers
- Email capture form works end-to-end (signup → confirmation → list)
- All 6 gallery images render correctly at 1270x760
- Demo video plays with captions on mute
- Trial signup flow works on clean Windows machine
- Website handles load (Vercel analytics)
