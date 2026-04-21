# Dev Handoff

## Session Summary: 2026-04-20 (Journey 1 Local path dogfooding)

### Plan progress
- **~40% through MANUAL_TEST_PLAN.md** (127 / 507 steps checked)
- Source of truth: `MANUAL_TEST_PLAN.md` + `MANUAL_TEST_LOG.md`. Older `MANUAL_TEST_LOG_2026-04-14.md` is archived and not trusted.

### Shipped this session (3 commits, not pushed)
1. `c595623` **fix(ui)**: LLM=OFF pill now writes `UseLlm=false` on all profile slots (BUG-014)
2. `21a0591` **fix(ui)**: Wizard Ready page shows TTS provider; wizard persists downloaded Kokoro variant (`int8`) to settings so Settings TTS page opens with matching pulldown (BUG-013 + BUG-016)
3. `7d3ed5d` **docs(tests)**: §1.3 rewritten to match current Settings > General UI; §1.7 rewritten to reflect orthogonal Logging Intensity × PII Scrubber model (post BUG-024 decision); BUG-017..024 logged

### Bugs logged today
| ID | Sev | Area | Status |
|----|-----|------|--------|
| BUG-013 | Low | Wizard Ready page missing Kokoro | **FIXED** |
| BUG-014 | High | LLM=OFF pill ignored | **FIXED** |
| BUG-015 | — | silent failure | closed as resolved-by-BUG-014 |
| BUG-016 | Medium | Kokoro variant mismatch wizard↔Settings | **FIXED** |
| BUG-017 | Medium | `AdditionalKey` has no UI | Open |
| BUG-018 | High | Auto-Start toggle writes JSON but no OS registration | Open |
| BUG-019 | Low | Vision hotkey row missing from Settings UI (feature works) | Open |
| BUG-020 | Medium | Hotkey Record capture fires bound pipeline instead of capturing | Open |
| BUG-021 | Low | Quick Chat no mic button (cosmetic — global Dictate hotkey already works inside) | Open |
| BUG-022 | **Critical** | Esc in Quick Chat → `this.Close()` → zombie window → hard crash on reopen. **One-line fix**: `QuickChatWindow.xaml.cs:116` → `this.AppWindow.Hide()` | Open |
| BUG-023 | High | Settings API Keys: no Test Connection button; validator accepts ≥30-char garbage as Deepgram/Gemini keys | Open |
| BUG-024 | Medium | `HistoryManager.cs:117-122` Full-level override ignores `PiiScrubEnabled`. Remove override so controls are orthogonal; update PII Scrubber copy | Open |

### Observations (OBS-001..007)
Wizard UX (defer Ollama to Settings post-wizard), download progress needs live %/MB indicator, Ollama install spawns its own window mid-wizard (needs upfront notice), STT-only latency benchmark captured (avg 720ms rec-end → injected across 6 samples), Ollama pull progress missing, Auto-Collapse default should be ON, Always-On-Top default should be ON.

## Next Session — Start Here

### Priority 1 (code fixes, low effort, high value)
1. **BUG-022 Critical** — one-line fix in `QuickChatWindow.xaml.cs:116`. Change `this.Close()` → `this.AppWindow.Hide()`. Prevents zombie-window crash.
2. **BUG-024 Medium** — delete `if (level == PrivacyLevel.Full)` override at `HistoryManager.cs:117-122`. Update `Settings_Privacy_PiiScrubber_Description` copy (EN + ES).

### Priority 2 (manual test runs, ~1.5 hr total)
3. **§1.7 Data & Privacy (13 steps)** — just rewritten to the orthogonal model. Runnable now.
4. **§1.8 + §1.9 System Integration + Performance (15 steps)** — closes Journey 1 cloud-style coverage.
5. **§3.3 + §3.4 + §3.2 remainder** — closes Journey 3 Local (~13 steps).

### Priority 3 (larger feature pass)
6. **§CT.7 Vision (17 steps)** — self-contained, good next-day session.
7. **§7.2–§7.5 TTS** (playback, ReadSelection, notification TTS, prefs).

### Not yet started (separate sessions each)
- Journey 2 Gemini Audio (~19 steps, needs Gemini-only wizard cycle)
- Journey 4 Hybrid Skip-LLM (~17 steps, BUG-014 now unblocks this)
- Journey 6 Wallet + License (~29 steps, needs Wallet-mode dictation runs to verify billing)
- §10 Audio Feeder automation (~16 steps, requires Python→PowerShell port first)

## Known deferred
- BUG-008 (license slot burn on repeated wipes) — keep watching, may recur
- BUG-009..012 from archived 2026-04-14 log — re-verify against current code in a dedicated pass, do not trust verbatim
- Wizard UX redesign (defer Ollama install to post-wizard Settings, with Settings auto-open on first run) — spec-worthy, not a quick fix
- Test plan structure cleanup pass (§1.3 + §5 duplicate settings coverage; "per-journey vs per-feature" drift)

## How to resume
Short prompt for fresh thread: *"Continuing manual testing. Read MANUAL_TEST_PLAN.md, MANUAL_TEST_LOG.md, and DEV_HANDOFF.md to pick up where we left off — ~40% through. Priority: [§1.7 / §1.8+1.9 / BUG-022 + BUG-024 fixes first]."*
