# Developer Handoff

## Current State

| Metric | Value |
|--------|-------|
| **Tests** | 1132 passing locally (479+ on CI — DPAPI/Clipboard/Audio/Whisper tests skipped on runners) |
| **Build** | **PASSES** (0 warnings, 0 errors). All 4 style dictionaries load cleanly at runtime. |
| **CI** | **PASSING** — lint, build, tests, gitleaks, vulnerability audit, publish all green. |
| **Branch** | main (all UI revamp + ACCOUNTS_SIGNIN + auth polish + Stream Deck committed) |
| **Website** | Deployed on Vercel (dikta.me), Root Directory = `website` |
| **Website Build** | **PASSES** — `next build` 0 errors, 15+ API routes, admin dashboard (at `/hqbackstage`), wallet + license system |

## Completed Streams

| Stream | Summary |
|--------|---------|
| **A-E** | Git repo, solution scaffold, publish config, CancellationToken, Config, Data, Security |
| **F** | WinUI 3 UI Layer — all 12 tasks |
| **G** | 689 unit tests + CI/CD pipeline |
| **I** | SnippetManager, AudioDucker, ChatPipeline, OllamaManager |
| **J** | CRUD Dictation Modes — all 7 tasks |
| **K** | OAuth & Trial Credits — K.1-K.7 (open bugs below) |
| **L** | Deepgram Streaming — L.1-L.5 committed. L.6-L.7 (Flux) deferred. |
| **SPEC_007** | Chat Feature Upgrade — 14/14 tasks complete (committed) |
| **SPEC_009** | Local Mode E2E + Wizard Fixes — Phases A-G complete, FIX-1 through FIX-16 (15/17 done; FIX-1 unblocked by SPEC_008, FIX-17 TTS wizard step pending) |
| **SPEC_011** | Ollama Management Hub — Core API, search service, Settings UI, E2E warmup, 22 new tests |
| **DOCS_V2** | Exhaustive User documentation (Features & Settings), integrated natively into the Next.js Website via Markdown |
| **SPEC_003 A–G** | TTS: Core infra, Kokoro local, Read Selection hotkey, pipeline hooks, cloud providers, Settings UI + Control Panel toggle, Phase G polish + E2E bugfixes. 282 new tests. **All 40 tasks complete. E2E verified.** |
| **SPEC_KOKORO_GPU** | **BLOCKED** — DirectML ConvTranspose incompatibility (ONNX Runtime 1.22.0). GPU variant + UI variant reorder kept. NuGet reverted to KokoroSharp.CPU. 5 new tests. |
| **Settings Rework** | Gemini TTS, per-preset trailing space, "When to Speak" relocation, local model selector removal, mute detection, conversational TTS notifications, note context capture. 8 features in one session. |
| **UI Revamp** | Glassmorphic theme system — Phases 0-3 complete. Phase 2 runtime crash (exit 127) **RESOLVED**. CP.1-CP.9 committed (auto-collapse, waveform, tray restore, snap-to-position, VU meter). Bug 3 (nav text contrast) **RESOLVED** — per-item local ThemeResource overrides. Sub-nav contrast also fixed. Nav pane collapse/expand (overlay logo, chevron in footer, CompactPaneLength=68). UserPaneFooter redesigned (avatar circle + display name + green status dot). |
| **ACCOUNTS_SIGNIN** | **COMPLETE** ✅ — All 9 sessions (32 tasks). Website auth, dashboard, admin panel (at `/hqbackstage`), JWT refresh, license provisioning, Ko-fi webhooks. |
| **AVATAR** | **COMPLETE** ✅ — Profile pic upload with circle crop (react-easy-crop), Supabase Storage bucket, C# app sync + display. Migration 009 (avatar_url column). Branded deeplink page. Admin URL obfuscation (`/admin` → `/hqbackstage`). Auth deeplink bugfix (middleware matcher + HTML redirect for custom URL schemes). |
| **UI_REVAMP_SCROLL_CP** | **COMPLETE** ✅ — 3-layer cylinder roll idle animation (status→logo+clock→weather), WeatherService (Open-Meteo + IP geolocation). 1010 tests. |
| **Chat Theming** | **COMPLETE** ✅ — QuickChatWindow migrated to App*Brush theme system (StaticResource + InjectControlBrushes + live switching). MarkdownTextBlock fully themed. Dead `ChatSettings.Theme` property removed. |
| **Auth Reliability** | **COMPLETE** ✅ — JWT refresh on startup before wallet sync (fixes "Session Expired" toast). Timer dueTime→Zero. Avatar sync on every launch via `SyncProfileFromServerAsync()`. Sign-in no longer forces `AuthMode.Wallet` (preserves user setting). Account page layout centered. |
| **SPEC_005** | **SHELVED** — Stream Deck integration. IPC + plugin architecture complete, but button press responsiveness unresolved. Server disabled in App.xaml.cs. See `plans/SPEC_005_STREAMDECK.md` §16. |
| **SPEC_015 Phase 0A** | **COMPLETE** ✅ — STTProviderFactory (18 tests) + LLMProviderFactory (20 tests). Closes test gap for Local Mode provider factories. |
| **SPEC_015 Phase 0B** | **COMPLETE** ✅ — Plugin infrastructure: `DiktaMe.Plugin.Abstractions` project with IPlugin, PipelineEventBus, PluginManager, PluginUIRegistry, JsonPluginSettingsStore. Host wired: DI, 8 pipeline completion sites, Settings nav injection, tray menu injection. 24 tests. |
| **SPEC_015 Phase 0B.19** | **COMPLETE** ✅ — BeforeLlm pipeline hooks in DictationPipeline, AskPipeline, ChatPipeline. Event bus types moved to Core.Pipeline (resolved circular dependency). |
| **SPEC_015 Phase 0C** | **WORKING** — Vision pipeline end-to-end: Cloud (Gemini) + Local (Ollama/moondream). ScreenCapture, ImageProcessor, SnippingOverlayWindow, Ctrl+Alt+S hotkey. CP "VIS" toggle (Cloud/Local). All 5 output modes wired (inject, clipboard, toast, toast+inject, toast+clipboard). AI Engine > Vision (model selection) + Workflows > Vision (pipeline config). 95% accuracy on local vision tests. Next: two-step capture→modal UX. |

## Open Bugs (Stream K) — Updated 2026-03-22

1. ~~**App UI doesn't update after sign-in**~~ ✅ Fixed — `SyncWalletAfterSignInAsync()` syncs wallet + refreshes HUD + shows toast after sign-in (A.3, commit `3785914`).
2. ~~**Website "Sign Up" shows Coming Soon**~~ ✅ Fixed — `NEXT_PUBLIC_COMING_SOON` deleted from Vercel dashboard (W.12).
3. ~~**Trial counter page blank**~~ ✅ Fixed — Trial system replaced by wallet. `/api/trial/status` now returns wallet balance (W.6). `/api/trial/usage` returns 410 Gone.
4. ~~**Profile page "Failed to fetch profile"**~~ ✅ Fixed — `.single()` → `.maybeSingle()` in wallet balance query + 401 → redirect to login (commit `c20fb73`).

## Resolved Bugs (SPEC_011)

4. ~~**NullReferenceException on dictation — `LLMProviderFactory.CreateOllamaProvider`**~~ ✅ Fixed — null-coalesce defaults on `baseUrl`, `keepAlive`, `numCtx` in `LLMProviderFactory.CreateOllamaProvider`.
5. ~~**Free-text TextBox corrupted OllamaModel setting**~~ ✅ Fixed — removed "Or type model name" TextBox; model selection now exclusively via ComboBox dropdown of installed models. Added `OnSelectedModelIndexChanged` to sync ComboBox → SelectedModel → settings.
6. ~~**Model Library Install button too risky**~~ ✅ Fixed — replaced Install button with "View" link opening `ollama.com/library/{model}` in browser.
7. ~~**Ollama Settings page empty on open**~~ ✅ Fixed — auto-check health on `Page.Loaded` to populate model list and status.

## Resolved: Startup Crash (SPEC_003 Phase F)

**Root cause**: `settings.json` had `"Tts":null` — the JSON deserializer overwrites the `= new()` default initializer with `null`. Then `ControlPanelViewModel.LoadFromSettings()` accessed `settings.Tts.Enabled`, throwing a `NullReferenceException` during a WinUI UI-thread property change notification. WinUI's native XAML binding system intercepts such exceptions and crashes the process (exit code 127), bypassing ALL managed exception handlers including `UnhandledException`.

**Fix**: Added `SanitizeNulls()` in `SettingsManager.LoadAsync()` — null-coalesces all 11 settings sub-objects with `?? new()` after deserialization. Also added `UnhandledException` handler in `App.xaml.cs` as defensive measure.

**Key lesson**: Any new `AppSettings` sub-object property is vulnerable to this if a user's existing `settings.json` has the property set to `null` (or doesn't have it at all and a migration writes `null`). The `SanitizeNulls` method now covers all sub-objects.

## Resolved: Audio Ducking Not Finding App Sessions

**Root cause**: `GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)` returned only ONE endpoint — Chrome/Edge/Spotify sessions on other endpoints were invisible. **Fix**: Replaced with `EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)` to iterate ALL active render devices and their sessions. Ducking now works for both recording and TTS playback.

## Resolved: TTS ReadSelection Text Capture

**Root cause**: Two bugs — (1) `CaptureSelection` sent Ctrl+C while Alt was still held from Ctrl+Alt+Q hotkey, OS combined into Ctrl+Alt+C firing the Chat hotkey instead; (2) HWND was captured on UI thread (after dispatch delay) instead of hotkey thread. **Fix**: Added `WaitForModifierRelease()` before Ctrl+C in `CaptureSelection`, moved `GetCurrentForegroundWindow()` to `OnHotkeyPressed` (hotkey thread), reordered sound/capture in ReadSelection. Ducking restore race also fixed with `didDuck` flag.

## Resolved: Audio Ducking Fade Duration

**Implemented**: `DuckAsync()` and `RestoreAsync()` with linear volume interpolation over configurable `RampDownMs` (default 500ms, 0 = instant). New "Fade Duration" slider in Audio Settings (0–2000ms, step 100ms). All recording and TTS ducking paths use ramped transitions. Instant `Duck()`/`Restore()` kept for event handlers and `finally` safety nets.

## Known Issues (SPEC_011)

- **Settings corruption from TextBox bug may persist** — users who typed in the old TextBox may have `OllamaModel` set to a partial/invalid string in `settings.json`. Fix: open Ollama Settings → select correct model from dropdown.

## Control Panel Auto-Collapse + Waveform — Polish Remaining (CP.1-CP.9 committed)

**Commits**:
- `1e91ff7` — `feat(ui): auto-collapse bar, voice waveform, and tray restore [CP.1-CP.8]`
- `9394d7b` — `feat(ui): snap-to-position, VU meter bars, and collapse layout fix [CP.9]`

### Bug 1: Status text + dot shift ~3-5px upward on collapse

When the bar auto-collapses (420px → 170px), the "READY"/"LISTENING" text and the status dot jump ~3-5px upward. Noticeable on every collapse cycle.

**Likely cause**: `HeaderButtons.Visibility = Collapsed` (line ~580 in `ControlPanelPage.xaml.cs`) removes the buttons StackPanel from layout. Even though both StackPanels use `VerticalAlignment="Center"`, the Grid recalculates its internal height, subtly shifting the vertical center. The asymmetric `Padding="12,4,12,11"` amplifies small layout shifts.

**Planned fix**: Use `Opacity=0` + `IsHitTestVisible=false` instead of `Visibility.Collapsed` to keep HeaderButtons in layout. Also set `HeaderButtons.IsHitTestVisible = true` in `RestoreBarWidth()`. Not yet implemented.

### Bug 2: Amplitude Bars waveform — redesign as horizontal VU level meter

The "Bars" waveform style doesn't fill enough of the header bar visually. Current design animates all 20 bars independently with a sine wave — they all bounce up/down simultaneously.

**Target redesign**: Horizontal VU level meter — a single row of ~40 vertical segment bars where the fill length (how many bars are lit, left-to-right) tracks voice amplitude in real time. Color gradient across bars using `palette.Accent` → `palette.GlowBase`. See plan file `C:\Users\gecko\.claude\plans\tender-frolicking-cloud.md` for full implementation details.

### Bug 3: Settings sidebar nav text contrast — RESOLVED ✅

**Fix**: Per-item local ThemeResource overrides injected via code-behind into each `NavigationViewItem.Resources`. Each item gets its own 6 `SolidColorBrush` instances for the `NavigationViewItemForeground*` keys. WinUI's VisualStateManager resolves `{ThemeResource}` from the nearest scope — local brushes on the item take priority over App.xaml globals. No bleed between items, no side effects on other Settings text.

**Main nav (NavigationView)**:
- Selected: dark navy text (`p.Background`) on accent background
- Hover (non-selected): accent blue text (`p.NavActive`)
- Normal: 70% white (`#B3FFFFFF`, up from 60%)
- No pointer event handlers needed — VisualStateManager handles all state transitions via local brushes

**Sub-nav (ListView in settings pages)**:
- `StaticResource` aliasing: custom `SubNavItemBackgroundSelected`/`SubNavItemForegroundSelected` brushes in App.xaml ThemeDictionaries, aliased to standard `ListViewItem*` keys via `<StaticResource x:Key="..." ResourceKey="..."/>` in `<ListView.Resources>`
- Removed hardcoded `Foreground="{StaticResource AppTextBrush}"` from DataTemplate TextBlocks so ListViewItem foreground inheritance works
- Sub-nav column width bumped from 230px → 250px to prevent text clipping

**Key insight**: WinUI 3 generic.xaml NavigationViewItemPresenter VisualState Setters use `{ThemeResource}` with an empty `<Grid.Resources/>` — no internal overrides to shadow local resources. Per-item injection works because ThemeResource resolution walks: Presenter → NavigationViewItem (our brushes here) → NavigationView → Window → App.

**Files changed**: `SettingsWindow.xaml.cs`, `ThemeService.cs`, `App.xaml`, 4 settings page XAMLs, `DictationPresetsSettingsPage.xaml` (width only)

### Nav Pane Collapse/Expand + UserPaneFooter Redesign (committed)

**Commits**:
- `6928495` — `feat(ui): collapsible nav pane with persistent overlay logo + chevron`
- `7c6b66c` — `fix(ui): move chevron to pane footer, widen collapsed pane, tune icon alignment`
- `5cc4f31` — `feat(ui): redesign UserPaneFooter with avatar, status dot, compact mode`

**What was done**:
- Logo + "dIKta.me" text overlay positioned outside NavigationView (avoids WinUI 3 PaneHeader clipping when `IsPaneToggleButtonVisible="False"`)
- Chevron button moved to `PaneFooter` (below About, above UserPaneFooter)
- Logo center stays fixed at x=35 in both expanded (left=21, icon=28px) and collapsed (left=26, icon=20px) states — zero horizontal shift
- `NavigationViewItemButtonMargin` tuned to `5,2,4,2` for icon-logo alignment
- `CompactPaneLength="68"` (widened from default 56)
- `PaneCustomContent` spacer reduced from 88px to 56px
- UserPaneFooter: avatar circle (28px→20px compact), green status dot (8px→6px compact), display name from email prefix, always navigates to Account page
- Double-tap on nav item toggles pane collapse/expand (with selection revert logic)

## Open Issues

- ~~**Sign In broken / redesign needed**~~ ✅ Fixed — ACCOUNTS_SIGNIN sprint (Sessions 3-4) wired deeplink with refresh token, JWT auto-refresh, display name extraction, wallet sync after sign-in. UserPaneFooter shows OAuth display name.
- ~~**"Session Expired" toast on startup**~~ ✅ Fixed — JWT refresh now runs before wallet sync. Timer dueTime changed to Zero. Avatar sync added to startup. Sign-in no longer forces `AuthMode.Wallet`.
- **App quit stalling after TTS**: `AppWindow.Closing` handler cancels close unconditionally — `Application.Current.Exit()` gets blocked. Needs `_isExiting` flag to bypass cancellation during shutdown.
- **Test beep ducks live audio**: One test instantiates a real AudioDucker and ducks live audio sessions (YouTube volume drops during test run). Needs mocking or environment guard.

## Completed: ACCOUNTS_SIGNIN Sprint ✅

**Full plan**: `plans/ACCOUNTS_SIGNIN.md` (32 tasks, 9 sessions — ALL COMPLETE)

### Progress

| Session | Tasks | Scope | Status | Commit |
|---------|-------|-------|--------|--------|
| 1 | W.1–W.7 | Website auth + API routes | **DONE** ✅ | `12f70c0` |
| 2 | W.8–W.12 | Website dashboard UI | **DONE** ✅ | `600666d` |
| 3+4 | A.1–A.5, T.1 | App deeplink, JWT refresh, display name, tests | **DONE** ✅ | `3785914` |
| 5 | W.13–W.14 | License provisioning + validation | **DONE** ✅ | `9576e0e` |
| 6 | D.1, D.2, D.7 | Admin foundation (role, overview, auth guard) | **DONE** ✅ | `be3a30a` |
| 7 | D.3, D.4 | Admin sales + users pages | **DONE** ✅ | `eadbdd5` |
| 8 | D.5, D.8 | Admin license gifting + Ko-fi webhook | **DONE** ✅ | `be20eb8` |
| 9 | D.6, D.9 | Support placeholder + env vars | **DONE** ✅ | `c971d2d` |
| — | Bug fix | Profile page 401 + gitleaks allowlist | **DONE** ✅ | `c20fb73` |

### Key Features Delivered

**Website (Next.js on Vercel):**
- Auth deeplink with `refresh_token` for silent JWT renewal
- Wallet API routes: `/api/wallet/status`, `/api/wallet/history`
- Token refresh endpoint: `/api/auth/refresh`
- Dashboard with wallet balance, license status, recent activity cards
- Wallet detail page with transaction history + pagination
- License validation endpoint: `/api/licenses/validate`
- Admin dashboard (at `/hqbackstage`): overview KPIs, user management (search/pagination), sales data, license gifting, Ko-fi webhook adapter
- Profile page: 401 → redirect to login, `.maybeSingle()` fix, avatar upload with circle crop

**C# App (WinUI 3):**
- `HandleDeepLink()` extracts + stores `refresh_token`
- `TokenRefreshService`: background timer (5min check, immediate first tick), proactive refresh when <10min remaining, reactive refresh on 401. Startup: explicit `CheckAndRefreshAsync()` before wallet sync ensures fresh JWT.
- `JwtDecoder.ExtractDisplayName()` / `ExtractAvatarUrl()` — OAuth metadata extraction
- `AccountSettings.DisplayName` / `AvatarUrl` persisted to settings.json
- `SyncWalletAfterSignInAsync()` — wallet sync + HUD refresh + toast after sign-in
- `SessionExpired` events wired on both wallet proxies + refresh service
- UserPaneFooter shows OAuth display name (falls back to email prefix)
- 15 new unit tests (JwtDecoder, AccountService, TokenRefreshService)

**Supabase:**
- Migration 007: `licenses` + `pending_gifts` tables with RLS
- Migration 008: `is_admin` column + updated `handle_new_user` trigger (auto-claims pending gifts)
- Ko-fi webhook adapter in `wallet-webhook` Edge Function
- License provisioning wired into LemonSqueezy webhook flow

### Manual Testing Still Needed (T.2, T.3)

- **T.2**: 10 API endpoints need curl/browser verification (see `plans/ACCOUNTS_SIGNIN.md` Session 9)
- **T.3**: Full 12-step E2E sign-in flow verification (app → browser → deeplink → wallet → refresh → dictation)

---

## Completed: Avatar Upload + Auth Polish ✅

**Commits**: `45e182b`, `177e07b`, `f98e86c`

### Key Features Delivered

**Website (Next.js on Vercel):**
- Profile pic upload with circle crop (`react-easy-crop`) — 256x256 WebP output
- Supabase Storage bucket `avatars` (public, 2MB limit, RLS for user-scoped upload/delete)
- `/api/profile/avatar` endpoint (POST upload, DELETE remove)
- `avatarUrl` field added to `/api/profile` (GET/PATCH) and `/api/account/me` (for C# app)
- Branded deeplink redirect page (logo, dark theme, "Go to Dashboard" button) — replaces plain "Redirecting..." text
- Admin URL obfuscated: `/admin` → `/hqbackstage` (all pages + API routes renamed)
- Auth deeplink bugfix: middleware matcher excludes `api|auth` paths, HTML meta-refresh for `diktame://` custom URL scheme

**C# App (WinUI 3):**
- `AccountService.SyncProfileFromServerAsync()` — fetches `/api/account/me` to sync avatar URL for email/password users
- `UserPaneFooter` displays actual avatar image via `Ellipse + ImageBrush` (falls back to initial letter)
- Avatar alignment: `Padding=16,8`, `Margin=5,2,4,2`, `CornerRadius=8` — matches NavigationViewItem metrics exactly

**Supabase:**
- Migration 009: `avatar_url TEXT` column on profiles, backfill OAuth users, updated `handle_new_user()` trigger
- Storage bucket `avatars` with RLS policies (user upload/update/delete own, public read)

### Open Issue
- **Profile page "Save Changes" returns 500** — "Failed to update profile" error on PATCH to `/api/profile`. RLS policies exist (SELECT + UPDATE for own profile). Diagnostic error detail added to response — needs reproduction to capture actual Supabase error message.

### Key Files

| Directory | Files |
|-----------|-------|
| `website/lib/auth/` | `deeplink-page.ts` (branded HTML redirect template) |
| `website/app/api/profile/avatar/` | `route.ts` (POST upload, DELETE remove) |
| `website/app/components/` | `AvatarCropModal.tsx` (react-easy-crop with circular mask) |
| `website/supabase/migrations/` | `009_avatar_url.sql` |
| `src/DiktaMe.Core/Account/` | `AccountService.cs` (SyncProfileFromServerAsync), `IAccountService.cs` |
| `src/DiktaMe.App/Views/Settings/` | `UserPaneFooter.xaml/.cs` (avatar image display) |

---

## Previous Work: UI Revamp (Glassmorphic Theme System)

**Spec**: `plans/UI_REVAMP.md`
**Plan**: `C:\Users\gecko\.claude\plans\wobbly-hatching-rabbit.md`

### Status by Phase

| Phase | Description | Status |
|-------|-------------|--------|
| **Phase 0** | Theme Foundation (ThemeService, 3 palettes, V1→App brush migration) | **COMPLETE** ✅ |
| **Phase 1** | Inter Font Integration (embed TTF, apply to root containers) | **COMPLETE** ✅ |
| **Phase 2** | Custom Control Styles (SegmentedButton, ToggleSwitch, Slider, Inputs) | **COMPLETE** ✅ — all 4 style dicts enabled in App.xaml, colors via ThemeResource overrides |
| **Phase 3** | Settings Visual Refresh (card-based sections on all 9 pages) | **COMPLETE** ✅ |
| **Phase 4** | Control Panel Dashboard Restyling | Pending |
| **Phase 5** | Polish & QA | Pending |

### What Was Completed

**Phase 0 — Theme Foundation:**
- Added `ThemeName` property to `GeneralSettings` (default: `"Midnight"`)
- Created `ThemeService.cs` in `DiktaMe.App/Services/` — singleton that mutates `SolidColorBrush.Color` in-place on existing App*Brush resources (NOT dictionary swap — `StaticResource` resolves at parse time)
- Three `ThemePalette` records defined in `ThemeService.cs` (Midnight, Ember, Frost) — **NOT** separate palette XAML files
- Created 3 palette XAML files in `Themes/Palettes/` (MidnightPalette.xaml, EmberPalette.xaml, FrostPalette.xaml) — these are NOT loaded at runtime, just there for reference/XAML designer
- Migrated ALL `V1*Brush` references → `App*Brush` across 12+ XAML files (~445 references)
- Updated `SharedResources.xaml` with fallback brush definitions (Midnight defaults)
- Updated `ControlPanelPage.xaml.cs` to derive glow/shimmer colors from theme palette

**Phase 1 — Inter Font:**
- Embedded `Inter-Regular.ttf` (412KB) + `Inter-SemiBold.ttf` (420KB) in `Assets/Fonts/`
- Registered `AppFont`, `AppFontSemiBold`, `AppFontMono` resources in `SharedResources.xaml`
- Applied `FontFamily` to root containers: `Page` (ControlPanelPage), `NavigationView` (SettingsWindow), `ContentControl` wrapper (LoadingWindow, QuickChatWindow)
- Migrated `FontFamily="Consolas"` → `{StaticResource AppFontMono}` in 7 places
- Migrated LoadingWindow hardcoded colors (#002029 etc.) to theme brushes

**Phase 2 — Custom Control Styles (COMPLETE ✅):**
- ✅ `ToggleSwitchStyle.xaml` — minimal property setters (`MinWidth=0`, `MinHeight=0`), colors via 21 ThemeResource overrides in SharedResources.xaml
- ✅ `SliderStyle.xaml` — empty placeholder (colors via ThemeResource keys: `SliderTrackValueFill`, `SliderThumbBackground`, etc.)
- ✅ `InputStyles.xaml` — simple property setters for TextBox, ComboBox, NumberBox (CornerRadius, BorderThickness, Padding, FontSize)
- ✅ `SegmentedButtonStyle.xaml` — pill-shaped Cloud/Local toggle with keyed styles
- All 4 files enabled in App.xaml, app runs cleanly

**Phase 3 — Settings Visual Refresh (COMPLETE):**
- Applied `SettingsCardStyle` card wrapping to all 9 settings pages
- Pages: General, Account, Privacy, Hardware, AIEngine, Workflows, DictationPresets, Macros, About
- Pattern: `<Border Style="{StaticResource SettingsCardStyle}">` wrapping related settings groups
- Build: 0 warnings, 0 errors. Tests: 968/968 passing.

### RESOLVED: `{Binding Color, Source={StaticResource ...}}` Pass2 Crash

The original Phase 2 style files used `{Binding Color, Source={StaticResource AppAccentBrush}}` in `Setter.Value > ResourceDictionary` blocks. This crashed the WinUI 3 XAML compiler at Pass2 (XBF generation). **This was fixed by rewriting all 4 style files to use simple property setters only** — no `{Binding}`, no `Setter.Value > ResourceDictionary`. Color overrides (e.g. `SliderTrackValueFill`, `TextControlBackground`) are now defined as standalone brushes in `SharedResources.xaml` and mutated in-place by `ThemeService.ApplyTheme()`.

All 4 style files now build cleanly: ToggleSwitchStyle.xaml, SliderStyle.xaml, InputStyles.xaml, SegmentedButtonStyle.xaml.

### RESOLVED: Runtime Crash (Exit 127) When Style Dictionaries Loaded

**Root causes identified via bisection (2026-03-17):**

1. **ToggleSwitchStyle.xaml** — Custom ControlTemplate was missing required template parts (`SwitchThumb`, `OffContentPresenter`, `OnContentPresenter`) and used wrong element types for named parts (`Border`/`Ellipse` instead of `Rectangle`/`Border`). The WinUI 3 ToggleSwitch code-behind accesses these parts by name and type — NRE → exit 127.

2. **SliderStyle.xaml** — Implicit `Style TargetType="Slider"` with `Foreground`/`Background` setters but without `BasedOn` stripped the default ControlTemplate (WinUI 3 implicit styles without `BasedOn` don't inherit the template from the default style). Even an implicit style with just `MinHeight` + no `BasedOn` crashed. `BasedOn="{StaticResource DefaultSliderStyle}"` also crashed (key doesn't exist in WinUI 3 1.6).

3. **Palette XAML files** — 3 files in `Themes/Palettes/` defined duplicate `x:Key` values identical to `SharedResources.xaml`. XAML compiler processes ALL `.xaml` files in the project directory. Fixed by excluding from build via `<Page Remove>` in csproj.

**Fixes applied:**
- **ToggleSwitchStyle.xaml** → Rewritten to minimal property setters (`MinWidth=0`, `MinHeight=0`). Visual customization via 21 ThemeResource key overrides in SharedResources.xaml (`ToggleSwitchFillOn`, `ToggleSwitchKnobFillOff`, etc.), wired into ThemeService for live theme switching.
- **SliderStyle.xaml** → Empty ResourceDictionary placeholder. Slider colors already controlled via ThemeResource keys in SharedResources.xaml (`SliderTrackValueFill`, `SliderThumbBackground`, etc.).
- **InputStyles.xaml** → Kept as-is (simple property setters for CornerRadius/Padding/FontSize without `BasedOn` — works fine because TextBox/ComboBox/NumberBox property setters don't strip the template).
- **Palette files** → Excluded from XAML compilation via `<Page Remove="Themes\Palettes\*.xaml"/>` in csproj.
- **App.xaml** → All 4 style dictionaries enabled and loading cleanly.

**Key WinUI 3 lesson:** Never use implicit styles with `Foreground`/`Background` setters on controls that have complex ControlTemplates (Slider, ToggleSwitch) — even without setting `Template`, certain property setters can interfere with the default template's resource resolution. Use ThemeResource key overrides instead.

### Bug Fix: ThemeService.ApplyTheme Null Guard (2026-03-17)

**Root cause:** When `SettingsManager.LoadAsync` fails (e.g. corrupt `settings.json`), it fires `SettingsChanged` with default settings where `General.ThemeName` is null. The `SettingsChanged` handler in `ThemeService` passed null to `ApplyTheme()`, which called `Palettes.TryGetValue(null, ...)` — `Dictionary.TryGetValue` throws `ArgumentNullException` on null key.

**Log evidence:**
```
[WRN] SettingsManager: failed to load settings — using defaults
System.ArgumentNullException: Value cannot be null. (Parameter 'key')
   at DiktaMe.App.Services.ThemeService.ApplyTheme(String themeName) in ThemeService.cs:line 145
```

**Fix applied:** Changed `ApplyTheme(string themeName)` → `ApplyTheme(string? themeName)` and `GetPalette(string themeName)` → `GetPalette(string? themeName)`. Both now guard with `string.IsNullOrEmpty(themeName)` and fall back to "Midnight".

**File:** `src/DiktaMe.App/Services/ThemeService.cs`

### WinUI 3 XAML Gotchas Discovered During Revamp

1. **`FontFamily` on `Grid`/`Panel` = silent XAML compiler crash (exit code 1)**: `Grid` extends `Panel` → `FrameworkElement`, NOT `Control`. `FontFamily` is a `Control` property. Fix: set on `Page`, `NavigationView`, or wrap with `ContentControl`.

2. **`{Binding}` inside `Setter.Value > ResourceDictionary` = Pass2 crash**: The `{Binding Color, Source={StaticResource ...}}` pattern for lightweight resource overrides works in WPF but silently crashes the WinUI 3 XAML compiler at XBF generation (Pass2, line 760). No error in output.json.

3. **XAML compiler processes ALL `.xaml` files** in project directory tree, even if not referenced in App.xaml `MergedDictionaries`. Cannot "hide" a file by simply not referencing it.

### Key Files (UI Revamp — all committed)

| Directory | Files |
|-----------|-------|
| `src/DiktaMe.Core/Config/` | `AppSettings.cs` (`ThemeName` in `GeneralSettings`, `BarPosition`) |
| `src/DiktaMe.App/Services/` | `ThemeService.cs` (theme engine — 3 palettes, in-place brush mutation) |
| `src/DiktaMe.App/Themes/` | `SharedResources.xaml` (font resources, ThemeResource overrides, nav margin) |
| `src/DiktaMe.App/Themes/Styles/` | `SegmentedButtonStyle.xaml`, `ToggleSwitchStyle.xaml`, `SliderStyle.xaml`, `InputStyles.xaml` |
| `src/DiktaMe.App/Assets/Fonts/` | `Inter-Regular.ttf`, `Inter-SemiBold.ttf` |
| `src/DiktaMe.App/Views/` | `SettingsWindow.xaml/.cs` (overlay logo, chevron in footer, per-item nav brushes) |
| `src/DiktaMe.App/Views/Settings/` | `UserPaneFooter.xaml/.cs` (avatar + display name + status dot + compact mode) |
| `src/DiktaMe.App/Views/` | `ControlPanelPage.xaml.cs` (auto-collapse, waveform, snap-to-position) |
| `src/DiktaMe.App/` | `App.xaml` (all 4 style dicts enabled, ThemeDictionaries), `DiktaMe.App.csproj` |

### Known Issues (2026-03-17 post-crash-fix)

**Pipeline Issues (may be local sound config — user restarting PC to verify):**
1. **Local dictation returns "(upbeat music)"** — Whisper transcribes silence/noise as "(upbeat music)" on any dictation. Possibly a microphone input routing issue (wrong device or muted).
2. **Cloud dictation does nothing** — No response from cloud STT. Could be API key / auth issue, or same mic input problem.
3. **Ask pipeline returns "No question detected"** — STT not capturing audio. Same root cause suspected (mic input).
4. **TTS works** — Audio output is fine, confirming the issue is input (recording) not output.

**UI Gaps vs. Design Mockup (`C:\Users\gecko\Downloads\GlassmorphicSettings-toggles.png`):**
1. **ToggleSwitches** — Mockup shows custom capsule (pill) shape with sleek proportions. Current: default WinUI 3 toggle (larger, less refined). The custom ControlTemplate was removed to fix exit 127 crash. ThemeResource overrides control colors but not shape.
2. **Card borders** — Mockup shows very subtle, barely-visible frosted borders. Current: `AppBorderBrush` (`#FFFFFF14`) renders as visible yellow/gold borders on `SettingsCardStyle`. Needs opacity reduction or color adjustment.
3. **Card backgrounds** — Mockup has subtle frosted glass/depth effect. Current: flat `AppSurface2Brush` solid fill. Glassmorphic blur requires `AcrylicBrush` or `MicaBrush` (WinUI 3 supports this via `DesktopAcrylicBackdrop`).
4. **Navigation active indicator** — Mockup has colored left bar on active nav item. Current: default NavigationView highlight. Needs `NavigationViewItem` style or resource overrides.
5. **Overall depth** — Mockup has layered glass panels with subtle shadows/glow. Current: flat solid surfaces. This is a Phase 5 polish item.

### To Resume

1. ~~Fix runtime crash~~ ✅ RESOLVED — all 4 style dicts enabled, app runs
2. ~~Investigate pipeline issues~~ ✅ Resolved after PC restart (mic input routing issue)
3. ~~CP.1-CP.9~~ ✅ All committed — auto-collapse, waveform, tray restore, snap-to-position, VU meter
4. ~~Nav pane collapse/expand~~ ✅ Overlay logo, chevron in footer, pixel-tuned alignment
5. ~~UserPaneFooter redesign~~ ✅ Avatar circle + display name + green status dot + compact mode
6. ~~Phase 6/7 Stabilization~~ ✅ Standardized Cloud/Local tabs, nav text contrast, sidebar logo, new icons
7. ~~Sign In design + function~~ ✅ RESOLVED — ACCOUNTS_SIGNIN sprint (Sessions 3-4)
8. ~~Fix gitleaks CI~~ ✅ Allowlisted `App.xaml` + `plans/ACCOUNTS_SIGNIN.md` — false positives
9. Continue to Phase 4 (Control Panel Dashboard) → Phase 5 (Polish & QA)
10. **UI polish** — card border opacity, toggle switch shape (if safe approach found), glassmorphic depth

---

## SPEC_005: Stream Deck Integration — SHELVED

**Spec**: `plans/SPEC_005_STREAMDECK.md` (full bugfix record in §15)

### What's Working

- **Named pipe IPC** (`DiktaMe.V2.Api`): Bidirectional, newline-delimited JSON, auto-reconnect
- **LocalApiServer** in DiktaMe.App: Per-client write lock, server-side 300ms debounce
- **4 Stream Deck actions**: PipelineTrigger, SettingsToggle, ModeSwitch, StatusDisplay
- **Property Inspectors**: Self-contained HTML (no CDN), settings persistence via SD WebSocket
- **App stability**: No crashes during dictation with Stream Deck connected (Bug 1 fixed)
- **SetWindowLongPtr**: Graceful try/catch (Bug 2 fixed)

### Unresolved Bugs (feature shelved)

| Bug | Summary | Status |
|-----|---------|--------|
| **B-6** | Button press feels sluggish (need double-click or quick-click) | **UNRESOLVED** — 3 rounds of fixes (debounce removal, optimistic UI, sync KeyPressed). Server-side command dropping suspected. |
| **B-7** | Ask button triggers Dictate instead of Ask | **UNRESOLVED** — Plugin sends correct `pipelineType=ask`. Server-side command routing issue. |
| **B-8** | AutoFlush deadlock (connection dead) | **FIXED** — Reverted to `bufferSize: 1` (no `AutoFlush`). |

### Architecture (Key Files)

| File | Role |
|------|------|
| `src/DiktaMe.App/Services/LocalApiServer.cs` | IPC server (disabled — `Start()` commented out in App.xaml.cs) |
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | `TriggerPipeline()` entry point (dead code while server disabled) |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | `ExternalStateChanged` event (dead code while server disabled) |
| `src/DiktaMe.Core/Config/ApiCommand.cs` | IPC command model + parser (compiled but unused) |
| `src/DiktaMe.StreamDeck/` | Full plugin (4 actions, 2 PIs, pipe client, models) |
| `test-helpers/test-ipc-pipe.ps1` | Manual pipe test script |
| `tests/.../ApiCommandParserTests.cs` | 25 unit tests for IPC parser |

### Re-enablement

1. Uncomment `Services.GetRequiredService<Services.LocalApiServer>().Start();` in `App.xaml.cs`
2. Rebuild DiktaMe.App + Stream Deck plugin
3. See `plans/SPEC_005_STREAMDECK.md` §16 for full status

---

## Backlog

**Next 10 Steps** defined in `DEVELOPMENT_ROADMAP.md` (top of file). Priority order:
1. **FIX-17**: TTS wizard step (spec + test matrix ready in `SPEC_009_WIZARD_FLOW.md`)
2. **FIX-1**: Wallet terminology (unblocked — SPEC_008 COMPLETE)
3. **L.5**: Streaming UI toggle (manual test only)
4. **H.1**: Installer — only hard blocker before V2.0 ships
5–10. **SPEC_015 Modules Sprint** — plugin infra → Vision → Connectors → Memory → Meetings

After step 4, V2.0 is releasable. Modules ship as incremental updates.

## Recent Session: Settings Rework + Gemini TTS + UX Improvements (2026-03-15)

### Feature 1: Gemini TTS Cloud Provider

Added Google Gemini as a 5th TTS provider (alongside Kokoro, Deepgram, Inworld, OpenAI).

**New file:** `src/DiktaMe.Core/TTS/GeminiTtsProvider.cs` (271 lines)
- Endpoint: `generativelanguage.googleapis.com/v1beta/models/{model}:generateContent` with `responseModalities: ["AUDIO"]`
- Output: base64-encoded PCM (s16le, 24kHz, mono) — identical format to all other providers
- Auth: API key via `?key=` query param, or OAuth Bearer token detection (`ya29.*`)
- Models: `gemini-2.5-flash-preview-tts` (default), `gemini-2.5-pro-preview-tts`
- 30 voices: Kore (default), Zephyr, Puck, Charon, Fenrir, Leda, Orus, Aoede, etc.
- 60s timeout, 3 retries with exponential backoff on 429/network errors
- Reuses existing Gemini API key from SecureStorage (no new key setup needed)

**Modified files:**
- `TTSProviderFactory.cs` — added `"gemini"` to `ResolveVariant()` and `CreateProviderCore()` switches
- `TtsSettingsViewModel.cs` — added `"gemini"` to `ProviderKeys`, `CloudProviderKeys`, `VoiceLists`, labels
- `AppSettings.cs` — added `SpeechPrompt` property to `TtsSettings`
- `TtsSpeaker.cs` — prepends speech prompt for Gemini before synthesis
- Resource strings: `Settings_Tts_Provider_Gemini` in en + es-MX

### Feature 2: Per-Provider TTS Controls

Each TTS provider now only shows controls it actually supports:
- **Speed slider**: visible only for OpenAI (Cloud tab) and Kokoro (Local tab)
- **Speech Style prompt**: visible only for Gemini — free-text field for tone/pace/style instructions (max 200 chars)
- **Voice/Volume/MaxWords/TestVoice**: shown for all providers

**Implementation:** Added `ShowSpeed`, `ShowSpeechPrompt`, and `CurrentProviderKey` computed properties to `TtsSettingsViewModel`. XAML uses `BoolToVisibilityConverter` on these properties.

**How Gemini speech prompts work:** The prompt is prepended to the text content (e.g. `"Say cheerfully: Hello world"`), not a separate API field. This means no changes to `ITTSProvider` interface. Prepending happens in both `TtsSpeaker.SpeakAsync()` (production) and `TtsSettingsViewModel.TestVoiceAsync()` (test voice button).

### Feature 3: Move "When to Speak" to Pipelines > Speak (TTS)

The "When to Speak" toggles (Speak Ask Responses, Speak Chat, Speak Translations, Speak Notifications, Duck Other Apps, Read Selection hotkey) moved from **AI Engine > TTS > "When to Speak" tab** to **Pipelines > "Speak (TTS)"** sub-item.

**Rationale:** TTS behavior toggles are workflow config, not engine config. TTS engine page now has only Cloud/Local tabs.

**Changes:**
- `AIEngineSettingsPage.xaml` — removed "When to Speak" tab, simplified TTS to Cloud/Local bool toggle (`IsTtsCloudTab`)
- `AIEngineSettingsViewModel.cs` — replaced 3-tab `TtsTabIndex` int with `IsTtsCloudTab` bool
- `WorkflowsSettingsPage.xaml` — added Speak (TTS) section with all 6 toggles
- `WorkflowsSettingsViewModel.cs` — removed "Dictation Behaviors" section, added `Tts` property, replaced `IsDictationBehaviorsSelected` with `IsSpeakSelected`

### Feature 4: Per-Preset Trailing Space + Remove Use LLM Toggle

Trailing space moved from global `GeneralSettings.TrailingSpace` to per-preset `DictationProfile.TrailingSpace`.

**Changes:**
- `DictationMode.cs` — added `TrailingSpace` property to `DictationProfile` (default `true`)
- `DictationModesSettingsViewModel.cs` — replaced `CloudUseLlm`/`LocalUseLlm` with `CloudTrailingSpace`/`LocalTrailingSpace`
- `DictationPresetsSettingsPage.xaml` — replaced "Use LLM" toggle with "Trailing Space" toggle (with description)
- `LoadingViewModel.cs` — dictation pipeline now reads `profile.TrailingSpace` instead of `_settings.Current.General.TrailingSpace`
- `WorkflowsSettingsViewModel.cs` — removed all "Dictation Behaviors" fields (TrailingSpace, AdditionalKeyEnabled, RawModeOverride, RefineVoiceMode) since they were either moved or removed

**Note:** `UseLlm` is preserved in `DictationProfile` for pipeline use but no longer editable from UI — existing values are carried forward via `existing?.CloudProfile.UseLlm ?? true`.

### Feature 5: Remove Per-Pipeline Local Model Selector

Removed the Local Model ComboBox from Pipelines settings to prevent GPU overload from multiple Ollama models loading simultaneously.

**Changes:**
- `WorkflowsSettingsPage.xaml` — removed Local Model ComboBox + Refresh button from Local tab
- `ModesSettingsViewModel.cs` — removed `SelectedLocalModelIndex`, `LocalModelNames`, `_localModelIds`, local model population, and sync. `SaveAsync()` now always sets `LocalProfile.ModelName = null` (uses global Ollama model).

### Feature 6: Conversational Spoken TTS Variants

Notification TTS now speaks natural, conversational phrases instead of raw UI text.

**How it works:**
- `NotificationService.ShowToast()` accepts optional `spokenKey` and `spokenArgs` parameters
- `ResolveSpokenText()` looks up `{spokenKey}_Spoken` from resources for a natural phrasing
- Falls back to generic `Spoken_Error_Generic` / `Spoken_Warning_Generic` for unkeyed error/warning toasts
- Final fallback: original `"{title}. {message}"` concatenation

**Spoken variants added (en + es-MX):**
- `Loading_WhisperFailed_Spoken` — "The speech recognition model couldn't be downloaded..."
- `Loading_HotkeyConflict_Spoken` — "The {0} hotkey is already being used..."
- `Loading_RecordingFailed_Spoken` — "The recording didn't work..."
- `Loading_NoModesConfigured_Spoken` — "No dictation modes are set up yet..."
- `Loading_NoteSaved_Spoken` — "Your note has been saved."
- `ReadSelection_NoSelection_Spoken` — "No text is selected..."
- `Spoken_Error_Generic` — "Something went wrong."
- `Spoken_Warning_Generic` — "Just a heads up."

### Feature 7: Microphone Mute Detection

Detects when the user's microphone is muted during recording and shows a toast + spoken notification.

**Changes:**
- `LoadingViewModel.cs` — added `MuteDetector` dependency, `OnMuteStateChanged` handler
- Both `RecordAudioAsync` (utility pipelines) and streaming dictation: call `_muteDetector.UpdateDeviceLabel()`, check immediately with `CheckMuteState()`, subscribe to `MuteStateChanged`, start monitoring. Cleanup on stop.
- Resource strings: `Recording_MicMuted_Title`, `Recording_MicMuted_Message`, `Recording_MicMuted_Spoken` (en + es-MX)

### Feature 8: Note Pipeline Context Capture

Notes now capture the currently selected text as context before recording, embedding it as a blockquote in the saved note.

**Changes:**
- `PipelineOptions.cs` — added `PreCapturedContext` property to `NoteOptions`
- `NotePipeline.cs` — builds note entry with optional `> {context}` blockquote between timestamp and note text
- `LoadingViewModel.cs` — `RunNotePipelineAsync` now receives `sourceWindow` HWND, calls `CaptureSelection()` before recording, passes `PreCapturedContext` to pipeline options

**Output format:**
```
## 2026-03-15 19:30:00

> Selected text from the active window

Transcribed note content here
```

### Files Changed Summary

| File | Change Type |
|------|------------|
| `GeminiTtsProvider.cs` | **NEW** — Gemini TTS provider (271 lines) |
| `TTSProviderFactory.cs` | Register Gemini provider |
| `AppSettings.cs` | Add `SpeechPrompt` to TtsSettings |
| `DictationMode.cs` | Add `TrailingSpace` to DictationProfile |
| `TtsSpeaker.cs` | Gemini speech prompt prepending |
| `PipelineOptions.cs` | Add `PreCapturedContext` to NoteOptions |
| `NotePipeline.cs` | Context blockquote in saved notes |
| `NotificationService.cs` | Conversational spoken TTS with resource keys |
| `LoadingViewModel.cs` | Mute detection, per-preset trailing space, note context capture, spoken keys |
| `TtsSettingsViewModel.cs` | Gemini provider + per-provider visibility + SpeechPrompt |
| `AIEngineSettingsViewModel.cs` | Simplify TTS tabs to Cloud/Local bool |
| `ModesSettingsViewModel.cs` | Remove local model selector |
| `DictationModesSettingsViewModel.cs` | TrailingSpace replaces UseLlm toggle |
| `WorkflowsSettingsViewModel.cs` | Remove Dictation Behaviors, add Speak (TTS) + Tts VM |
| `AIEngineSettingsPage.xaml` | Remove "When to Speak" tab, add Speech Prompt + ShowSpeed visibility |
| `WorkflowsSettingsPage.xaml` | Add Speak (TTS) section, remove Local Model selector |
| `DictationPresetsSettingsPage.xaml` | Replace UseLlm with TrailingSpace toggle |
| `en/Resources.resw` | Gemini label, SpeechPrompt, spoken variants, mute detection strings |
| `es-MX/Resources.resw` | Same as en (translated) |

### Recently Blocked: SPEC_KOKORO_GPU — Kokoro TTS DirectML GPU Acceleration

| Detail | Value |
|--------|-------|
| **Spec** | `plans/SPEC_KOKORO_GPU.md` |
| **Goal** | Sub-250ms Kokoro TTS synthesis via DirectML GPU |
| **Status** | **BLOCKED** — ONNX Runtime 1.22.0 DirectML EP cannot handle Kokoro's `ConvTranspose` node |
| **Error** | `OnnxRuntimeException: ConvTranspose node '/encoder/F0.1/pool/ConvTranspose' — 80070057` |
| **Scope** | ALL model variants (gpu, fp32, fp16, int8) fail with DirectML EP active |
| **Unblock** | KokoroSharp or ONNX Runtime ships a version fixing DirectML ConvTranspose support |

**What was kept from this work (5 new tests, net-positive):**
- `"gpu"` model variant in `KokoroModelManager` (valid quantization, works on CPU, 169MB)
- Variant reorder in Settings UI: gpu → fp32 → fp16 → int8 (with descriptive labels)
- Default variant changed from `"int8"` to `"gpu"` for new installs
- `KokoroUseGpu` property in `AppSettings.TtsSettings` (inert, avoids settings.json compat issue)

**What was rolled back:**
- NuGet reverted: `KokoroSharp.DirectML` → `KokoroSharp.CPU`
- DirectML SessionOptions code, GPU toggle UI, GPU-aware cache key — all removed

---

### SPEC_003 TTS — Completed (for reference)

| Detail | Value |
|--------|-------|
| **Spec** | `plans/SPEC_003_TTS_V2.md` |
| **Phases** | A–G (40 tasks, all complete, E2E verified) |
| **Local TTS** | Kokoro-ONNX via `KokoroSharp.CPU` NuGet (82M params, 88MB int8 model) |
| **Cloud TTS** | Deepgram Aura-2, Inworld TTS-1.5, OpenAI, Gemini (all working after variant routing fix) |
| **Key hotkey** | `Ctrl+Alt+Q` = "Read Selection" (select text anywhere → hear it) |
| **Tests** | 282 new tests (944 total) |

### E2E Testing Still Needed

- **Cloud providers**: Retest Deepgram, OpenAI, Inworld after variant routing fix
- **Ask/Chat/Translate hooks**: Enable SpeakAskResponses etc. → use mode → verify audio
- **Control Panel toggle**: ON/OFF enables/disables all TTS output
- **Settings persistence**: Toggle states, provider, voice/speed survive restart

## CI/CD Notes

- **Gitleaks:** `.gitleaks.toml` allowlists `website/QUICKSTART.md`, `plans/ACCOUNTS_SIGNIN.md`, `App.xaml`, palette XAMLs (historical fake JWTs + XAML x:Key false positives)
- **Test threshold:** `ci/test-threshold.json` set to 470 (local runs 944, CI runs ~479 due to skipped tests)
- **Vercel:** Connected to `geckogtmx/diktame`, Root Directory = `website`

## i18n Notes (SPEC_004)

- **WinUI3Localizer** adopted — `ApplicationLanguages.PrimaryLanguageOverride` does NOT work in unpackaged apps
- All 24 XAML files migrated from `x:Uid` to `l:Uids.Uid` (WinUI3Localizer namespace)
- en + es-MX `.resw` files (370+ keys each) + CoreStrings `.resx` (8 keys)
- **TODO:** Some labels and tooltips still need translation review — check all screens in es-MX locale for missing or untranslated strings

## Recent Changes (SPEC_009 Wizard Fixes + Telemetry + Local Mode Polish)

All fixes verified via manual testing on 2026-03-09/10. See `plans/SPEC_009_FIXES.md` for full details.

| Fix | Summary |
|-----|---------|
| FIX-2 | Language selection step added (bilingual EN/ES, Step 0) |
| FIX-4 | Default Refine mode = Auto (not Voice) |
| FIX-5 | Default system prompts preloaded for all dictation modes |
| FIX-6 | WPM formula fixed — uses wall-clock time (RecordingMs + TotalMs). Verified: LLM=124 WPM, RAW=154 WPM |
| FIX-7 | Whisper model download UI in wizard STT step (progress bar, blocks Next) |
| FIX-8 | Hotkey double-subscription fix (singleton LoadingViewModel unsubscribes before re-subscribing) |
| FIX-9 | Download triggers on Next click, not radio selection (BeforeLeaveStep callback) |
| FIX-10 | Split Cloud/Local into independent STT + LLM toggles (6-col layout, auth badge LOC/API/MIX) |
| FIX-13 | Wizard LLM step: Ollama validation + model pull with progress (blocks Next when offline) |
| FIX-14 | Wizard LLM step: Ollama auto-install via winget, fallback to browser. Default model → `gemma3:4b`. |
| FIX-15 | Local mode polish: Ollama auto-start on launch, keep-alive setting (5m–2h), first-inference GPU log, Whisper download in Settings, Ollama install from Settings |
| FIX-16 | **LLMProviderFactory caching — 5x Ollama latency improvement** (3000ms→550ms). Wizard language Back bug fix. API Keys step auto-skip on local path. Phased winget install messages. |

## RESOLVED: Wizard Won't Show on Fresh Install

**Root cause**: `ControlPanelViewModel` constructor called `LoadFromSettings()` which triggered `OnIsRefineVoiceChanged` → `UpdateAsync()` → prematurely wrote `settings.json`. Then `LoadAsync()` found the file, Migration 8 set `WizardCompleted = true`, and the wizard was skipped.

**Fix**: Added `_suppressSave` guard in `ControlPanelViewModel`. All `On*Changed` handlers skip `UpdateAsync()` when `_suppressSave` is true. Guard is set around both `LoadFromSettings()` call sites (constructor + `OnSettingsChanged`). Manually verified: wizard shows on fresh install, does not show on subsequent launches.

**File**: `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs`

## DONE: Whisper GPU Acceleration — CUDA → Vulkan Swap

**Root cause**: `Whisper.net.Runtime.Cuda` did NOT bundle CUDA runtime libraries → fell back to CPU silently (~2800ms for 11s audio).

**Fix applied**: NuGet swap in `src/DiktaMe.Core/DiktaMe.Core.csproj`:
```xml
<PackageReference Include="Whisper.net.Runtime.Vulkan" Version="1.9.0" />
```

**Why Vulkan**: Self-contained (28MB, all DLLs bundled), cross-vendor (NVIDIA + AMD + Intel Arc), no user setup needed. No code changes — runtime selection is automatic.

**Verified (G.6)**: `runtime="Vulkan"`, ratio 0.05x–0.09x (GPU). ~6-7x speedup over CPU. First dictation has cold-start penalty (Vulkan shader compile).

**G.7 fix**: `STTProviderFactory` was creating a new `WhisperProvider` per dictation, reloading the 466MB model each time (~800ms). Fixed by caching the instance. **Verified**: pipeline `transcription_ms` dropped from ~1250ms to ~440ms. Raw mode end-to-end: ~500ms.

**G.8**: Added CPU-fallback warning log — if Vulkan DLLs are deployed but `Cpu` runtime is loaded, logs a warning suggesting GPU driver update. Vulkan loader (`vulkan-1.dll`) comes from GPU drivers, not from us.

**Full investigation details**: `plans/SPEC_009_LOCALFLOW.md` §12.8–12.10

## Remaining Work

### Manual Testing Needed

| Item | Notes |
|------|-------|
| ~~**TTS Phase G gaps**~~ | ✅ All gaps fixed, E2E verified (see above) |
| **API Keys step skip** | FIX-16 auto-skips step 4 when both providers are local — needs manual verification |
| **SPEC_009 scenarios 3-8** | Scenarios 1-2 passed. Remaining: full local E2E, hybrid combos (see `plans/SPEC_009_TESTING.md`) |
| **Ollama auto-start** | FIX-15 — verify app launch with Ollama not running |
| **Keep-alive dropdown** | FIX-15 — change in Settings, restart app, verify in Ollama request logs |
| **Whisper model change download** | FIX-15 — switch model in Settings, verify download with progress |
| **Ollama install from Settings** | FIX-15 — verify Install button appears when Ollama is offline |
| **SPEC_011 Ollama Settings page** | Model list ✅, search/view ✅, pull ✅, delete (needs test), service restart (needs retest after fixes), VRAM display (needs test), warmup ✅ |
| **Refine on Antigravity** | `CaptureSelection` times out — app-specific accessibility issue, separate investigation |

### ~~Known Gap: TTS Not Persisted to DB~~ ✅ Fixed

`tts_played_ms` column added to SQLite history table. Ask, Translate, and ReadSelection pipelines now persist TTS latency. Notification TTS wired via `ShowToast` → `SpeakIfEnabledAsync("notification")` with `suppressTts` to prevent double-speak on Ask answers.

### Tier 2 — Ship Blockers (Steps 1-4)

| Task | Effort | Status |
|------|--------|--------|
| **FIX-17** | TTS wizard step (Off / Local Kokoro / Cloud Deepgram) | Pending — spec ready (`SPEC_009_FIXES.md`, `SPEC_009_WIZARD_FLOW.md`) |
| **FIX-1** | Wizard: Trial → Wallet terminology | Unblocked — SPEC_008 now COMPLETE |
| **L.5** | Streaming UI toggle | Manual test pass only (~15 min) |
| **H.1** | Installer (Inno Setup) | Only hard blocker before V2.0 ships |

### Tier 3 — Modules (Steps 5-10, SPEC_015)

| Task | Effort | Status |
|------|--------|--------|
| **Phase 0A** | Factory tests | ✅ COMPLETE — 38 tests |
| **Phase 0B** | Plugin infrastructure | ✅ COMPLETE — 24 tests, 15 new files |
| **Phase 0B.19** | BeforeLlm hooks | ✅ COMPLETE |
| **Phase 0C** | Vision core | ✅ FUNCTIONAL — needs polish (see below) |
| **Phases A-C** | Connectors: `IConnector` + Obsidian + Webhook/Discord/Streamer.bot | Not started — unblocked |
| **Phases O-Q** | Memory: SQLite+VSS, embedding model, pipeline hooks | Not started — unblocked |
| **Phases D-E** | Meetings: session engine + Scribe window (heaviest module, do last) | Not started — unblocked |

### Vision (Phase 0C) — Known Issues

| Issue | Severity | Status |
|-------|----------|--------|
| ~~**Black image on 2nd+ capture**~~ | High | ✅ Fixed — crops from pre-captured monitor image |
| ~~**Silent pipeline failure on 2nd+ attempt**~~ | High | ✅ Fixed — local CancellationTokenSource per invocation |
| ~~**Audio ducking not restored on failure**~~ | Medium | ✅ Fixed — tied to silent failure fix above |
| ~~**TTS reads toast notifications**~~ | Low | ✅ Fixed — `suppressTts: true` on all Vision toasts |
| ~~**No Vision Settings UI**~~ | Low | ✅ Fixed — AI Engine > Vision + Workflows > Vision |
| ~~**NullRef on empty VisionProvider**~~ | High | ✅ Fixed — defaults to ollama/moondream |
| ~~**Output mode mapping**~~ | Medium | ✅ Fixed — all 5 modes correctly mapped + clipboard copy |
| ~~**OllamaProvider no vision support**~~ | High | ✅ Fixed — ProcessWithImageAsync via /api/chat with images |
| **Prompt tuning needed** | Low | Default query works but can be verbose. User can customize in Workflows > Vision. |
| **Image persistence always-on** | Low | Screenshots always saved to `%APPDATA%/DiktaMe/vision/` with no cleanup policy |
| **Two-step capture UX** | Enhancement | Next sprint: hotkey → clip → modal (clipboard/chat/note + text input) instead of auto-pipeline |

Full spec: `plans/SPEC_015_MODULES_SPRINT.md` (17 phases, 18-23 sessions)

### Tier 4 — Deferred

| Task | Effort |
|------|--------|
| ~~**LemonSqueezy**~~ | ✅ Done — ACCOUNTS_SIGNIN Sessions 5 (W.13-W.14) and 8 (D.5, D.8) |
| Cloud latency tuning | Cloud inference profiling |
| Control Panel wiring | RAW toggle→pipeline, REFINE toggle→pipeline (see `plans/CONTROL_PANEL_REWORK.md`) |
| ~~L.6-L.7~~ | Deferred — Flux (revisit when Chat gets voice input) |

## Reference Docs

- `plans/ACCOUNTS_SIGNIN.md` — **COMPLETE** — Full 32-task, 9-session plan for auth + dashboard + admin (all sessions done)
- `DEVELOPMENT_ROADMAP.md` — Full task breakdown
- `ARCHITECTURE.md` — Technical architecture
- `SECURITY.md` — GitHub security policy
- `plans/SPEC_009_LOCALFLOW.md` — Local mode E2E spec + GPU investigation (§12)
- `plans/SPEC_009_FIXES.md` — Wizard + local mode fix tracker (15/17 complete; FIX-1 unblocked, FIX-17 pending)
- `plans/SPEC_009_TESTING.md` — Manual test scenarios
- `plans/SPEC_KOKORO_GPU.md` — Kokoro DirectML GPU acceleration plan (**BLOCKED** — ConvTranspose incompatibility)
- `plans/SPEC_003_TTS_V2.md` — TTS implementation plan (40 tasks, 7 phases, complete)
- `plans/SPEC_003_TTS.md` — TTS research reference (V1 draft, superseded by V2)
- `plans/SPEC_015_MODULES_SPRINT.md` — Modules Sprint: plugin infra, Vision, Connectors, Memory, Meetings (17 phases, DRAFT)
- `plans/SPEC_009_WIZARD_FLOW.md` — Complete wizard path test matrix (14 paths, target-state for FIX-17)
- `plans/SPEC_001_MEETINGS.md` / `SPEC_002_VISION.md` — Post-launch feature specs (superseded by SPEC_015)
- `plans/SPEC_011_OLLAMA.md` — Ollama Management Hub spec (implemented)
- `plans/archive/` — Completed implementation plans (Stream F, K, OAuth Restructure, etc.)
