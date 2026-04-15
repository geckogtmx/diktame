## Session Summary: 2026-04-14 (Evening — Manual Testing + Wizard Rework)

### Completed
- **Wizard license gate UX**: BYOK/Local disabled for unlicensed users with info text. Wallet is default. "I Have a Key!" red button opens activation page. License activates → returns to Get Started with all options enabled.
- **Features showcase page**: Wallet path shows Power License benefits (Local AI, BYOK, Vision) — not free pipelines. "Get yours now — $20" link. "No rush" footer.
- **Wallet AuthMode fix**: `StartWalletAsync` now sets `AuthMode = Wallet` so dictation works immediately after sign-in.
- **WebSocket singleton fix**: `WalletStreamingSTTProxy.ConnectAsync()` resets all state (WS, flags, buffers, TCS) for reuse across dictations. Fixed critical bug where 2nd+ dictation failed.
- **Wizard lane filtering**: Removed `StartLocalAsync` shortcut. Both BYOK and Local go through STT → LLM → TTS steps. BYOK shows only cloud options, Local shows only local options.
- **Inline API key entry**: STT and LLM pages now have API key field + Test button + skip warning for BYOK path. Separate API Keys step (6) always skipped.

### Bugs Found (12 total, 5 fixed this session)
See `MANUAL_TEST_LOG.md` for full details and `memory/project_testing_bugs.md` for tracker.

### Next Session Priority (Start Here)

#### 1. BYOK Wizard — Provider Selection + Key Entry (BUG-010, BUG-011)
The wizard BYOK path needs proper provider dropdowns on each page:

**LLM page (BUG-011):**
- Add provider dropdown: Anthropic, Gemini, OpenAI, OpenRouter, Requesty
- Each provider needs its own API key field
- Current text is vague ("Cloud AI") — needs clearer copy explaining what the LLM does
- Currently hardcoded to Gemini key only

**TTS page (BUG-010):**
- Add provider dropdown: Deepgram, OpenAI, Gemini, Inworld
- Each has its own API key — does NOT share the LLM key (wrong assumption in current code)
- Same pattern: dropdown + key field + Test button + skip warning

#### 2. Wizard Layout (BUG-012)
- Last wizard page (Ready) crops text at bottom — reduce top padding

#### 3. Wizard Completion Timing (BUG-009)
- `WizardCompleted` is set too early in `StartWalletAsync` (before sign-in completes)
- If user closes wizard window before signing in, next launch skips wizard
- Fix: only set `WizardCompleted = true` after final wizard step or after successful sign-in callback

#### 4. Remaining from Test Plan
- Add informational note to Get Started page ("You can change these settings after the wizard")
- Continue Journey 1 testing from step 1.2 (Core Dictation)
- Test Local wizard path end-to-end
- License re-activation slot burning (BUG-008)

### Key Context
- **Files heavily modified**: `WizardViewModel.cs`, `WizardGetStartedPage.*`, `WizardSttPage.*`, `WizardLlmPage.*`, `WizardTtsPage.*`, `WizardWindow.*`, `PipelineFactory.cs`, `WalletStreamingSTTProxy.cs`
- **New files created**: `WizardFeaturesPage.*`, `WizardActivatePage.*`
- **Tests**: 1218 passing, 0 failing
- **Build**: 0 warnings, 0 errors
- **Test log**: `MANUAL_TEST_LOG.md` (repo root)
