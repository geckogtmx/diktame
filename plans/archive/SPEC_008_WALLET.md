# SPEC_008: Wallet Service (Pay-As-You-Go AI)

> **Status:** ✅ COMPLETE (code-verified 2026-03-16: K.8-K.12 client + M.1-M.4 cloud, all committed)
> **Date:** 2026-03-09
> **Parent Specs:** `DEVELOPMENT_ROADMAP.md`
> **Replaces:** V1 `SPEC_036_WALLET_SERVICE.md`, Stream K Trial architecture

---

## 1. Executive Summary

dIKta.me V2 currently supports "Bring Your Own Key" (BYOK) via `AuthMode.None`/`AuthMode.ApiKey` and a 15,000-word free trial via `AuthMode.Trial`. To serve users who cannot run local models and refuse to manage raw API keys, we introduce a **prepaid Wallet Service**.

Following the "Fair Pricing" philosophy from V1, this service allows users to purchase credits (top-ups) which act as fuel for cloud STT (Deepgram) and LLM (Gemini Flash) processing.

**Crucial Architecture Decision:** We will completely **replace the separate "Trial Account" architecture** from Stream K. Instead of maintaining two distinct systems (a managed Gemini proxy for trials + a Wallet for paid users), we unify them. A "Trial" is simply a user who creates a dIKta.me account and receives an initial promotional **Grant** in their Wallet (e.g., $1.00, equivalent to ~15,000 words of dictation).

Because the V2 engine is a native C# client, **it cannot securely hold master API keys**. Wallet requests must be routed through a secure Cloud Proxy. To keep the app fast and offline-resilient, the client maintains a **Local Append-Only Ledger** that synchronizes with the cloud source-of-truth. This architecture absorbs the open-source billing patterns of **UniBee**, **Flexprice**, and **NetLedger** (specifically their append-only audit chains and priority burn-down logic).

### 1.1 Trial Code Deletion Scope

Since the app has **no distributed users**, this is a clean replacement (not a migration). The following Stream K artifacts will be **deleted entirely** when the Wallet is implemented:

| Artifact | File |
|---|---|
| `TrialAccountService` | `DiktaMe.Core/Account/TrialAccountService.cs` |
| `ITrialAccountService` | `DiktaMe.Core/Account/ITrialAccountService.cs` |
| `ITrialService` | `DiktaMe.Core/Account/ITrialService.cs` |
| `TrialGeminiProvider` | `DiktaMe.Core/Account/TrialGeminiProvider.cs` |
| `TrialGeminiAudioProvider` | `DiktaMe.Core/Account/TrialGeminiAudioProvider.cs` |
| `TrialSettings` | `DiktaMe.Core/Config/TrialSettings.cs` |
| `TrialStatus` | `DiktaMe.Core/Account/TrialStatus.cs` |
| `TrialAccountServiceTests` | `DiktaMe.Core.Tests/Account/TrialAccountServiceTests.cs` |
| `TrialGeminiProviderTests` | `DiktaMe.Core.Tests/Account/TrialGeminiProviderTests.cs` |
| `TrialGeminiAudioProviderTests` | `DiktaMe.Core.Tests/Account/TrialGeminiAudioProviderTests.cs` |
| `LLMRouterTrialTests` | `DiktaMe.Core.Tests/LLM/LLMRouterTrialTests.cs` |

The `AppSettings.Trial` property, `AccountSettingsViewModel` trial bindings, and all `AuthMode.Trial` routing in `LLMRouter`/`STTRouter` will be replaced with their Wallet equivalents.

---

## 2. Architectural Alignment with V2

### 2.1 The New AuthMode

`DiktaMe.Core.Config.AuthMode` — replace `Trial` in-place (same integer slot = `1`) to minimize code churn. `ApiKey` and `Account` retain their existing values:

```csharp
public enum AuthMode
{
    None = 0,      // BYOK, no account
    Wallet = 1,    // Signed in, using managed Pay-As-You-Go proxy (replaces Trial = 1)
    ApiKey = 2,    // BYOK, configured keys (unchanged)
    Account = 3,   // Signed in via OAuth but BYOK (unchanged)
}
```

### 2.2 Account & Token Management

`IAccountService` (retained from Stream K) and the new `WalletService` handle the `diktame://auth?token=JWT` deep link flow — reusing the existing infrastructure in `App.xaml.cs` and `ProtocolRegistrar`.

- The Wallet utilizes the JWT issued by Supabase Auth (same as current trial flow).
- When `WalletService` hits `GET /api/wallet/status`, the backend returns the current balance.
- New users automatically receive a promotional `GRANT` transaction upon first login.

### 2.3 Provider Routing & Graceful Fallback

To protect user agency, the app **never** forces Wallet usage just because an account has a positive balance. The Wallet providers are offered as explicit selections alongside BYOK options.

For `AuthMode.Wallet`, we create proxy providers:

- **`WalletGeminiProxy.cs`** — implements `ILLMProvider`. Only `ProcessAsync` (single-shot dictation/rewrite) is functional. `ProcessConversationAsync` throws `NotSupportedException` — **Chat is excluded from Wallet credits** (see section 2.5). Routes through the Cloudflare Worker to the **Google AI Gemini API** directly (no OpenRouter middleman).
- **`WalletDeepgramProxy.cs`** — implements `ISTTProvider` (batch transcription only). Streaming via `IStreamingSTTProvider` is intentionally excluded from wallet scope — batch is the primary dictation mode.

**User-Driven Flow:**
1. **Explicit Selection:** In Settings, the user explicitly selects their STT/LLM provider. If they select `Wallet-Deepgram` or `Wallet-Gemini`, the proxy providers are used and their balance burns down. If they select `OpenAI` (BYOK), the Wallet is completely bypassed.
2. **The "Missing Key" Safety Net (NEW FEATURE):** This does not currently exist in `LLMRouter` and must be built from scratch. When a BYOK provider fails due to a missing/invalid API key, and `WalletManager.Balance > 0`, the app shows a **confirmation dialog** (not a silent fallback): *"Your API key is missing. Use your dIKta.me Wallet instead? (Est. cost: ~$0.002)"*. Only upon user confirmation does the app route through `WalletGeminiProxy`. This preserves the "never forces Wallet usage" principle. This safety net only applies to dictation/rewrite modes, never chat.

### 2.4 DI Registration Transition

In `App.xaml.cs`, the current Trial DI wiring:
```csharp
// DELETE these registrations:
services.AddSingleton<TrialAccountService>();
services.AddSingleton<ITrialAccountService>(...);
services.AddSingleton<ITrialService>(...);
// IAccountService registration STAYS (backed by new implementation)
```

Replace with:
```csharp
services.AddSingleton<WalletManager>();
services.AddSingleton<WalletGeminiProxy>();
services.AddSingleton<WalletDeepgramProxy>();
services.AddSingleton<IAccountService, AccountService>();  // simplified, no trial logic
```

`LLMRouter` and `STTRouter` constructors change: replace `TrialGeminiProvider?` / `TrialGeminiAudioProvider?` parameters with `WalletGeminiProxy?` / `WalletDeepgramProxy?`. The `AuthMode.Wallet` check replaces the `AuthMode.Trial` check at the top of `ProcessAsync` / `TranscribeAsync`.

### 2.5 Chat Feature — BYOK / Local Only

The Chat feature (SPEC_007) is **excluded from Wallet credits**. Multi-turn conversations are open-ended and cost-unpredictable — a single long conversation could burn an entire wallet balance. Wallet credits are reserved for the core dictation pipeline where costs are small and bounded (one recording → one transcription + one rewrite).

**Enforcement:**
- `WalletGeminiProxy.ProcessConversationAsync()` throws `NotSupportedException`.
- `LLMRouter`: When `AuthMode == Wallet` and `mode == "chat"`, skip the wallet provider entirely and fall through to BYOK/local providers. If none are configured, show an Info Toast: *"Chat requires your own API key or a local model. Configure one in Settings > API Keys."*
- The Chat UI (`ChatViewModel`) checks `AuthMode` and disables the send button with a tooltip if no BYOK/local provider is available.

---

## 3. The Local Ledger (Absorbing Open-Source Patterns)

Borrowing the architecture of **UniBee**, **Flexprice**, and **NetLedger**, we **never** use `UPDATE balance = balance - X`. Balances are derived by summing an **Append-Only Audit Chain**.

### 3.1 Local SQLite Schema (`wallet_ledger`)

Implemented in `WalletManager.cs` using raw `Microsoft.Data.Sqlite` (mirroring `HistoryManager.cs` pattern — `InitAsync()`, `SemaphoreSlim` for thread safety, same `%APPDATA%\DiktaMe\` path).

| Column | Type | Description |
|:---|:---|:---|
| `id` | TEXT (UUID) | Unique Transaction ID matching the cloud DB. |
| `amount_micro` | INTEGER | Dollar value in **microdollars** (1 USD = 1,000,000). Negative for usage, positive for top-ups. Avoids IEEE 754 float rounding errors on sub-cent amounts. |
| `balance_after_micro` | INTEGER | Running balance after this transaction (microdollars). Enables O(1) balance reads via `SELECT balance_after_micro FROM wallet_ledger ORDER BY created_at DESC LIMIT 1` instead of `SUM()` over entire ledger. |
| `type` | TEXT | `GRANT`, `PURCHASE`, `USAGE`, `EXPIRY`, `REFUND`. |
| `created_at` | TEXT (ISO8601) | Timestamp. |
| `expires_at` | TEXT (ISO8601) | Null for purchased credits, set for promotional Grants. |
| `metadata` | TEXT (JSON) | Audit details, e.g., `{"pipeline": "dictate", "provider": "deepgram", "model": "nova-3"}` or `{"pipeline": "rewrite", "provider": "gemini", "model": "gemini-2.0-flash"}`. |

**Privacy Level:** The wallet ledger is **always** written regardless of `PrivacyLevel` — financial audit data is a legal/operational requirement, not a user-privacy-optional feature. The `metadata` field is the only privacy-sensitive column; at `PrivacyLevel.Ghost` or `Stats`, the `metadata` field is stored as `{}` (empty JSON object) instead of recording pipeline/provider details.

### 3.2 The "Burn-Down" Priority (Flexprice Strategy)

The fast-path UI balance uses `balance_after_micro` from the latest row. The full calculation (for sync verification) is:
```sql
SELECT SUM(amount_micro) FROM wallet_ledger
WHERE expires_at IS NULL OR expires_at > datetime('now', 'utc');
```

When deducting credits locally, the app mirrors the cloud's Priority Queue: **Promotional/Expiring Grants are burned before Purchased/Permanent Credits.**

### 3.3 Cloud Sync & Offline Behavior

1. **The Source of Truth** is the Cloudflare backend / Supabase.
2. **Synchronous Deduction:** When proxy providers receive a response from the Cloudflare Worker, the response headers contain the exact `X-Wallet-Cost` (microdollars) and `X-Wallet-Balance` (microdollars).
3. **Local Audit Insert:** The client immediately writes this delta into the local `wallet_ledger` SQLite database, including the `balance_after_micro` running total.
4. **Offline / Network Failure:** If the Cloudflare proxy is unreachable:
   - Wallet proxy providers return a failed result (same pattern as current `TrialGeminiProvider` network failure handling).
   - The app fires an Error Toast: *"dIKta.me Wallet unreachable. Check your internet connection."*
   - **No local-only spending.** The cloud is the source of truth; the client never optimistically deducts without server confirmation.
   - If the user has BYOK keys configured for a different provider, `LLMRouter`'s existing fallback mechanism handles the failover.

---

## 4. The Cloud Architecture (The "Meter")

### 4.1 Payment Flow (Gateway-Agnostic)

User opens `https://dikta.me/wallet` — our page, we control what payment options appear.

**Balance Constraints** (carried from V1 SPEC_036):
- **Minimum Top-Up:** **$5.00 USD** (gateway fees are disproportionate below this).
- **Maximum Wallet Balance:** **$50.00 USD** (strict liability cap — prevents large-scale refund exposure).

**Products (example pricing — varies per gateway):**
| Product | Credit Amount | Checkout Total (incl. service fee) |
|---|---|---|
| Starter | $5.00 | $6.50 |
| Standard | $10.00 | $12.00 |
| Pro | $20.00 | $24.00 |
| Power | $50.00 | $60.00 |

**"What you buy is what you get" UX:** Gateway fees and operational margin are added at checkout as a service fee. The user always receives the exact credit amount listed.

**Gateway architecture:** The webhook handler uses an **adapter pattern** — one adapter per payment provider (LemonSqueezy, Ko-fi, Stripe, Substack, manual donations, etc.). Each adapter validates the incoming webhook, extracts the payment details, and produces a normalized `CreditRequest`. The core handler is gateway-agnostic. Adding a new gateway = adding one adapter file.

**Planned gateways:**
- **LemonSqueezy** — primary, one-time purchases with product tiers
- **Ko-fi** — low-key donation support
- **Manual grants** — admin-initiated credits for support cases, beta testers, etc.
- Future: Stripe, Substack, etc.

The Webhook fires an event to a Supabase Edge Function, which inserts a `PURCHASE` transaction into the master ledger with `{"gateway":"<name>"}` metadata.

### 4.2 The Proxy (Cloudflare Workers)

The proxy sits between the WinUI 3 App and two upstream APIs: **Google AI (Gemini)** for LLM and **Deepgram** for STT. No OpenRouter — we hold direct API keys for both providers, eliminating the middleman.

**Two master keys only:**
- `GEMINI_API_KEY` — your Google AI Studio key (free tier: 1,500 req/day; paid: $0.075/1M input tokens for Flash)
- `DEEPGRAM_API_KEY` — your Deepgram key (free $200 credit on signup; paid: ~$0.0077/min for Nova-3)

**Request flow:**
1. Client sends STT/LLM request + User JWT.
2. Proxy validates JWT.
3. Proxy queries Supabase for current balance. Rejects if `< $0.01`.
4. **Concurrent Request Guard:** Proxy uses a Supabase `SELECT ... FOR UPDATE` row lock on the user's balance during deduction. This serializes concurrent requests per-user, preventing the race condition where two simultaneous requests both pass the balance check and collectively overdraw.
5. Proxy injects the master operator key and forwards to Gemini API / Deepgram API directly.
6. Proxy intercepts the response, calculates exact cost based on Gemini `usageMetadata` token counts / Deepgram audio duration.
7. Proxy issues an atomic `INSERT INTO wallet_ledger ... VALUES ('USAGE', -cost_micro)` with the calculated `balance_after_micro`.
8. Proxy returns the response to the C# client with `X-Wallet-Cost` and `X-Wallet-Balance` headers.

### 4.3 Model — Gemini Flash Only

The Wallet service uses a **single model**: **Gemini Flash** (currently Gemini 2.0 Flash, upgraded as new versions release). This is the fastest and cheapest LLM available — perfect for dictation rewrites where speed matters more than reasoning depth.

No model selection, no whitelist complexity. Wallet users get the best model for the job. Users who want GPT-4, Claude, or other models use BYOK.

**Why single-model:**
- Gemini Flash is the cheapest production LLM available ($0.075/1M input tokens)
- One API key = one billing relationship = simple operations
- No need for OpenRouter's 200-model catalog when the use case is "rewrite this dictated text"
- If Google ever becomes unreliable, adding a second direct key (e.g., OpenAI) is a 10-line Worker change

---

## 5. Implementation Roadmap (Tasks)

### Phase 1: WinUI 3 App Extension (Local C#)

- **Task K.8:** Overhaul `AuthMode` enum (`Trial=1` → `Wallet=1`), delete all Trial artifacts (see section 1.1 deletion table), update `AppSettings` to replace `TrialSettings` with `WalletSettings`. Update all `AuthMode.Trial` references in `LLMRouter`, `STTRouter`, and DI registration.
- **Task K.9:** Create `DiktaMe.Core.Data.WalletManager` using `Microsoft.Data.Sqlite` — local ledger with `amount_micro` / `balance_after_micro` schema, `SemaphoreSlim` thread safety, privacy-level-aware metadata, `InitAsync()` pattern from `HistoryManager`.
- **Task K.10:** Create `WalletDeepgramProxy` (`ISTTProvider`, batch only) and `WalletGeminiProxy` (`ILLMProvider`, `ProcessAsync` only — `ProcessConversationAsync` throws `NotSupportedException`). Wire into `STTRouter`/`LLMRouter` replacing trial provider slots. Add "Missing Key" safety net with user confirmation dialog (dictation only, never chat). Enforce chat exclusion in `LLMRouter` and `ChatViewModel`.
- **Task K.11:** Build the WinUI 3 `WalletDashboard` in Settings. Bind to `WalletManager` balance (fast read via `balance_after_micro`), show transaction history, add "Top Up" deep link button. Replace current `AccountSettingsPage` trial-credits section.

### Phase 2: The Cloud Integrations

- **Task M.1:** Deploy Cloudflare Worker proxy routes (`/v1/listen` for Deepgram batch STT, `/v1/generate` for Gemini Flash dictation rewrite). **No chat route** — chat is excluded from wallet. Two master keys only (`GEMINI_API_KEY`, `DEEPGRAM_API_KEY`). Implement JWT validation, balance check with `SELECT ... FOR UPDATE` row lock, cost calculation from Gemini `usageMetadata` / Deepgram audio duration, and `X-Wallet-Cost`/`X-Wallet-Balance` response headers.
- **Task M.2:** Setup Supabase `wallet_ledger` table (microdollar integers, `balance_after_micro` column) and RLS policies. Create promotional `GRANT` insertion trigger for new user sign-ups.
- **Task M.3:** Configure Lemon Squeezy Webhooks to handle `order_created` events. Validate against product IDs, enforce max balance cap ($50), and issue `PURCHASE` ledger rows via Supabase Edge Function.

### Phase 3: Emergency Governance

- **Task M.4 (Operation Liquidity):** Write a strict, tested Supabase Edge Function that can temporarily freeze the service (reject all proxy requests), snapshot all user balances, and calculate partial refunds via the Lemon Squeezy API in the event of a forced sunset. This script must be written and tested **before** the wallet launches.

---

## 6. Task Log (Session-by-Session Implementation)

Each session below is designed to be self-contained: it produces a compilable, testable commit. Sessions can be executed across separate coding sessions without losing context.

---

### Session 1: Trial Teardown + AuthMode Overhaul `[K.8]`

**Goal:** Remove all Trial infrastructure, replace `AuthMode.Trial` with `AuthMode.Wallet`, create `WalletSettings`. Solution must build with 0 errors at session end.

**Steps:**
1. Rename `AuthMode.Trial` → `AuthMode.Wallet` in `DiktaMe.Core/Config/AuthMode.cs` (keep `= 1`).
2. Create `DiktaMe.Core/Config/WalletSettings.cs` — sealed record with `Email`, `LastSynced`, `BalanceMicro` (cached for UI startup before sync).
3. Update `AppSettings`: replace `TrialSettings Trial` property with `WalletSettings Wallet`. Keep `AccountSettings Account`.
4. Delete all 7 Trial source files listed in section 1.1 (production code).
5. Delete all 4 Trial test files listed in section 1.1.
6. Find-and-replace `AuthMode.Trial` → `AuthMode.Wallet` across `LLMRouter.cs`, `STTRouter.cs`, and any remaining references.
7. In `LLMRouter` and `STTRouter`: replace `TrialGeminiProvider?` / `TrialGeminiAudioProvider?` constructor params with nullable placeholders (e.g., `ILLMProvider? walletLlm = null`, `ISTTProvider? walletStt = null`). The wallet providers don't exist yet — just stub the routing to use these params when `AuthMode.Wallet`.
8. Update `App.xaml.cs` DI: remove `TrialAccountService`/`ITrialService`/`ITrialAccountService` registrations. Remove `TrialGeminiProvider`/`TrialGeminiAudioProvider` wiring from router construction. `IAccountService` stays but backed by a simplified implementation (or keep the existing class with trial logic stripped).
9. Update `AccountSettingsViewModel`: remove all `Trial.*` property reads. Stub wallet balance display (`$0.00 — Wallet not yet connected`).
10. Build solution. Fix any remaining compile errors from Trial references.
11. Run `dotnet test` — expect test count to DROP (deleted trial tests). All remaining tests must pass.

**Commit:** `refactor(auth): replace Trial with Wallet AuthMode, delete Trial infrastructure [K.8]`

**Files touched:** ~15-20 files (deletions + edits)

---

### Session 2: WalletManager SQLite Ledger `[K.9]`

**Goal:** Create `WalletManager` with full local ledger CRUD, mirroring `HistoryManager` patterns. Comprehensive unit tests.

**Steps:**
1. Create `DiktaMe.Core/Data/WalletManager.cs`:
   - Constructor: `WalletManager(SettingsManager settings)` — db path at `%APPDATA%\DiktaMe\wallet.db`.
   - `InitAsync()` — create `wallet_ledger` table with schema from section 3.1.
   - `GetBalanceMicroAsync()` — fast path: `SELECT balance_after_micro ... ORDER BY created_at DESC LIMIT 1`. Returns `0` if empty.
   - `GetFullBalanceMicroAsync()` — verification path: `SUM(amount_micro)` excluding expired rows.
   - `InsertTransactionAsync(id, amountMicro, type, metadata, expiresAt?)` — inserts row, computes `balance_after_micro` from previous latest + amount.
   - `GetTransactionsAsync(int limit = 50)` — returns recent transactions for UI history display.
   - `SyncBalanceAsync(long serverBalanceMicro)` — called on app startup after `GET /api/wallet/status`. If local and server disagree, insert a reconciliation `USAGE` or `REFUND` row to align.
   - `SemaphoreSlim _lock` for thread safety on all write operations.
   - Privacy-level-aware metadata: check `_settings.Current.PrivacyLevel` — at Ghost/Stats, store `{}` for metadata.
2. Create `DiktaMe.Core.Tests/Data/WalletManagerTests.cs`:
   - Test `InitAsync` creates schema.
   - Test `InsertTransactionAsync` + `GetBalanceMicroAsync` round-trip.
   - Test `balance_after_micro` running total is correct after multiple inserts.
   - Test `GetFullBalanceMicroAsync` excludes expired rows.
   - Test `SyncBalanceAsync` reconciliation.
   - Test thread safety (concurrent inserts don't corrupt).
   - Test privacy level metadata stripping.
3. Wire `WalletManager` into DI in `App.xaml.cs`: `services.AddSingleton<WalletManager>()`.
4. Call `WalletManager.InitAsync()` in `LoadingViewModel.InitializeAsync()` after `HistoryManager.InitAsync()`.
5. Build + test.

**Commit:** `feat(wallet): add WalletManager local SQLite ledger with append-only audit chain [K.9]`

**Files touched:** ~4 files (new WalletManager, new tests, App.xaml.cs, LoadingViewModel)

---

### Session 3: Wallet Proxy Providers + Router Wiring `[K.10]`

**Goal:** Create the two proxy provider classes, wire them into the routers, enforce chat exclusion. Unit tests for all new code.

**Steps:**
1. Create `DiktaMe.Core/Account/WalletGeminiProxy.cs`:
   - Implements `ILLMProvider`, `IDisposable`.
   - Constructor: `(SecureStorage secureStorage, WalletManager walletManager, HttpClient? httpClient = null)`.
   - `ProcessAsync(text, systemPrompt, mode, ct)` — sends POST to Cloudflare Worker `/v1/generate` with `Authorization: Bearer {jwt}`. Reads `X-Wallet-Cost` / `X-Wallet-Balance` from response headers. Calls `WalletManager.InsertTransactionAsync()` with the cost delta. Returns `LlmResult`.
   - `ProcessConversationAsync(...)` — throws `NotSupportedException("Chat is not available with Wallet credits. Configure your own API key or use a local model.")`.
   - `IsAvailableAsync()` — checks JWT exists in SecureStorage AND `WalletManager.GetBalanceMicroAsync() > 0`.
   - `ProviderName => "Gemini Flash (Wallet)"`.
2. Create `DiktaMe.Core/Account/WalletDeepgramProxy.cs`:
   - Implements `ISTTProvider`, `IDisposable`.
   - Constructor: `(SecureStorage secureStorage, WalletManager walletManager, HttpClient? httpClient = null)`.
   - `TranscribeAsync(audioFilePath, language, ct)` — reads WAV file, sends POST to Cloudflare Worker `/v1/listen`. Reads wallet headers. Inserts transaction. Returns `TranscriptionResult`.
   - `IsAvailableAsync()` — same pattern as LLM proxy.
   - `ProviderName => "Deepgram (Wallet)"`.
3. Update `LLMRouter`:
   - Replace `TrialGeminiProvider? _trialProvider` with `WalletGeminiProxy? _walletProvider`.
   - In `ProcessAsync`: when `AuthMode == Wallet`, route to `_walletProvider` (existing Trial routing pattern).
   - In `ProcessConversationAsync`: when `AuthMode == Wallet`, **skip** `_walletProvider` — fall through to primary/fallback providers. If all fail, return empty result (the UI layer handles the messaging).
4. Update `STTRouter`:
   - Replace `TrialGeminiAudioProvider? _trialStt` with `WalletDeepgramProxy? _walletStt`.
   - In `TranscribeAsync`: when `AuthMode == Wallet`, route to `_walletStt`.
5. Update `App.xaml.cs` DI: register `WalletGeminiProxy` and `WalletDeepgramProxy`, inject into router construction.
6. Update `ChatViewModel`: check if current auth mode is `Wallet` AND no BYOK/local provider is configured. If so, disable send button with tooltip.
7. Create `DiktaMe.Core.Tests/Account/WalletGeminiProxyTests.cs`:
   - Test `ProcessAsync` sends correct request, reads headers, inserts transaction.
   - Test `ProcessConversationAsync` throws `NotSupportedException`.
   - Test `IsAvailableAsync` returns false when no JWT or zero balance.
   - Mock `HttpClient` for all tests (no real network calls).
8. Create `DiktaMe.Core.Tests/Account/WalletDeepgramProxyTests.cs`:
   - Test `TranscribeAsync` sends file, reads headers, inserts transaction.
   - Test `IsAvailableAsync`.
9. Create `DiktaMe.Core.Tests/LLM/LLMRouterWalletTests.cs`:
   - Test wallet routing for dictation mode.
   - Test chat mode skips wallet provider.
   - Test fallback when wallet provider fails.
10. Build + test.

**Commit:** `feat(wallet): add proxy providers and router wiring with chat exclusion [K.10]`

**Files touched:** ~10 files (2 new providers, 2 router edits, DI, ChatViewModel, 3 new test files)

---

### Session 4: Wallet Dashboard UI `[K.11]`

**Goal:** Build the Settings UI for wallet balance, transaction history, and top-up. Replace the trial credits section.

**Steps:**
1. Create `DiktaMe.App/ViewModels/Settings/WalletDashboardViewModel.cs`:
   - Properties: `decimal BalanceDollars` (computed from `WalletManager.GetBalanceMicroAsync() / 1_000_000m`), `ObservableCollection<WalletTransaction> RecentTransactions`, `bool IsSignedIn`, `string Email`, `bool HasBalance`.
   - Commands: `TopUpCommand` (opens `https://dikta.me/wallet` in browser), `RefreshCommand` (calls `GET /api/wallet/status` + `SyncBalanceAsync`), `SignInCommand`, `SignOutCommand`.
   - `LoadAsync()` called on page navigation — reads balance, loads recent transactions.
2. Create `DiktaMe.Core/Data/WalletTransaction.cs` — display model record: `Id`, `AmountDollars`, `Type`, `CreatedAt`, `Metadata`.
3. Rewrite `DiktaMe.App/Views/Settings/AccountSettingsPage.xaml`:
   - **Signed-out state:** InfoBar with sign-in button (keep from current design).
   - **Signed-in state:** User email, balance display (large number, green when > $0, red when < $0.50), "Top Up" button.
   - **Transaction history:** `ListView` bound to `RecentTransactions` showing type icon, amount, date, provider.
   - Remove all trial-specific UI elements (progress bar, word count, days remaining).
4. Update `AccountSettingsPage.xaml.cs` codebehind if needed for navigation events.
5. Update any settings navigation that references trial-specific elements.
6. Build + manual verification (UI review).

**Commit:** `feat(wallet): add WalletDashboard settings UI with balance and transaction history [K.11]`

**Files touched:** ~5 files (new ViewModel, new display model, XAML rewrite, codebehind)

---

### Session 5: Cloudflare Worker Proxy `[M.1]`

**Goal:** Deploy the serverless proxy that sits between the app and Gemini/Deepgram. Two master keys, no middleman. Handles JWT validation, balance checks, key injection, cost tracking.

**Manual Setup (YOU must do before this session):**
1. **Cloudflare account:** Sign up at [dash.cloudflare.com](https://dash.cloudflare.com) (free tier is sufficient).
2. **Install Wrangler CLI:** `npm install -g wrangler` then `wrangler login` to authenticate.
3. **Google AI Studio API key:** Go to [aistudio.google.com/apikey](https://aistudio.google.com/apikey), create an API key. This is the **master operator key** for all wallet LLM requests. Free tier: 1,500 req/day; paid tier: $0.075/1M input tokens for Gemini 2.0 Flash.
4. **Deepgram API key:** Sign up at [console.deepgram.com](https://console.deepgram.com), create an API key. This is the **master operator key** for STT. Free tier includes $200 credit.
5. **Note your Supabase project URL and service role key** — the Worker needs these to query/insert into `wallet_ledger`. You already have these from the existing trial setup.

**Steps:**
1. Create `diktame-proxy/` directory (separate from the C# solution — this is TypeScript for Cloudflare Workers).
2. `wrangler init diktame-proxy` — scaffold Cloudflare Worker project.
3. Add secrets to the Worker (these are encrypted, never visible in code):
   ```bash
   wrangler secret put GEMINI_API_KEY        # paste your Google AI Studio key
   wrangler secret put DEEPGRAM_API_KEY      # paste your Deepgram key
   wrangler secret put SUPABASE_URL          # e.g., https://volwljbiyzvvcqqdojyf.supabase.co
   wrangler secret put SUPABASE_SERVICE_KEY  # the service_role key (NOT the anon key)
   ```
4. Implement route handler for `POST /v1/generate` (LLM rewrite):
   - Extract `Authorization: Bearer {jwt}` header.
   - Validate JWT against Supabase (call `https://<project>.supabase.co/auth/v1/user` with the token).
   - Query `wallet_ledger` for balance: `SELECT balance_after_micro FROM wallet_ledger WHERE user_id = $1 ORDER BY created_at DESC LIMIT 1`.
   - Reject if balance < 10,000 microdollars ($0.01).
   - `SELECT ... FOR UPDATE` row lock for concurrent request guard.
   - Forward to Google AI Gemini API: `POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={GEMINI_API_KEY}`.
   - Parse `usageMetadata` from Gemini response (`promptTokenCount`, `candidatesTokenCount`) to calculate cost in microdollars.
   - `INSERT INTO wallet_ledger` the USAGE row with `balance_after_micro`.
   - Return response with `X-Wallet-Cost` and `X-Wallet-Balance` headers.
5. Implement route handler for `POST /v1/listen` (STT):
   - Same JWT/balance flow.
   - Forward audio to Deepgram REST API: `POST https://api.deepgram.com/v1/listen` with master `DEEPGRAM_API_KEY`.
   - Calculate cost from audio duration (Deepgram pricing: ~$0.0077/min for Nova-3).
   - Insert USAGE row, return with wallet headers.
6. Add `wrangler.toml` configuration with Supabase URL and secret bindings.
7. Test locally with `wrangler dev` and curl.
8. Deploy with `wrangler deploy`. Note the deployed URL (e.g., `https://diktame-proxy.<your-subdomain>.workers.dev`) — this goes into the C# proxy providers' base URL constant.

**Commit:** `feat(proxy): deploy Cloudflare Worker with Gemini + Deepgram direct keys [M.1]`

**Files touched:** New `diktame-proxy/` directory (~200 lines TypeScript)

---

### Session 6: Supabase Schema + Auth `[M.2]`

**Goal:** Set up the server-side wallet ledger table, RLS policies, and promotional grant trigger.

**Manual Setup (YOU must do before this session):**
1. **Supabase project:** You already have one from Stream K (the trial system). We reuse it — no new project needed.
2. **Supabase CLI:** Install if not already: `npm install -g supabase`. Then `supabase login` and `supabase link --project-ref <your-project-id>`.
3. **Verify access:** Go to [supabase.com/dashboard](https://supabase.com/dashboard) → your project → Settings → API. Confirm you have:
   - `Project URL` (e.g., `https://volwljbiyzvvcqqdojyf.supabase.co`)
   - `anon` key (for client-side auth)
   - `service_role` key (for server-side operations — the Cloudflare Worker uses this)
4. **Database access:** Go to SQL Editor in the Supabase dashboard. Verify you can run queries. The migration in step 1 below will be run here.

**Steps:**
1. Create Supabase migration: `wallet_ledger` table with columns matching section 3.1 schema (plus `user_id UUID REFERENCES auth.users`).
2. Create index on `(user_id, created_at DESC)` for fast balance lookups.
3. RLS policies:
   - Users can `SELECT` only their own rows.
   - Only the service role (Cloudflare Worker) can `INSERT`.
   - No `UPDATE` or `DELETE` allowed (append-only).
4. Create Supabase Edge Function `grant-on-signup`:
   - Triggered by `auth.users` insert (via database webhook or Supabase Auth hook).
   - Inserts a `GRANT` row: `amount_micro = 1_000_000` ($1.00), `expires_at = NOW() + 90 days`.
5. Create Edge Function `wallet-status`:
   - `GET /api/wallet/status` — returns `{ balance_micro: number, transactions: [...] }` for the authenticated user.
6. Test: create a test user, verify grant appears, verify balance endpoint returns correct data.

**Commit:** `feat(backend): setup Supabase wallet_ledger schema, RLS, and promotional grant trigger [M.2]`

**Files touched:** Supabase migrations + 2 Edge Functions

---

### Session 7: Lemon Squeezy Webhooks `[M.3]`

**Goal:** Wire up payment processing so top-ups become ledger rows.

**Manual Setup (YOU must do before this session):**
1. **Lemon Squeezy account:** You already have one (used for app licensing). Log in at [app.lemonsqueezy.com](https://app.lemonsqueezy.com).
2. **Create 4 products** in your existing Lemon Squeezy store (one-time payments, NOT subscriptions):
   - **Starter:** Price $6.50, name "dIKta.me Wallet — $5 Credit"
   - **Standard:** Price $12.00, name "dIKta.me Wallet — $10 Credit"
   - **Pro:** Price $24.00, name "dIKta.me Wallet — $20 Credit"
   - **Power:** Price $60.00, name "dIKta.me Wallet — $50 Credit"
   - For each: disable "Generate license key", disable "Generate invoice". These are simple consumable purchases.
3. **Note the `product_id`** for each (visible in the URL when editing the product, e.g., `123456`). The webhook Edge Function maps these IDs to credit amounts.
4. **Webhook signing secret:** Go to Settings → Webhooks in Lemon Squeezy. You'll configure the webhook URL in step 3 below, but note the **signing secret** — the Edge Function uses this to verify webhook authenticity.
5. **Enable test mode:** Toggle "Test Mode" in the Lemon Squeezy dashboard so you can simulate purchases without real charges.

**Steps:**
1. Create Supabase Edge Function `lemon-webhook`:
   - Validates webhook signature using the Lemon Squeezy signing secret (stored as a Supabase secret).
   - Extracts `order_created` event: `product_id`, `customer_email`, `total`.
   - Maps `product_id` → credit amount in microdollars (e.g., Standard product → 10,000,000).
   - Looks up user by email in `auth.users`. If not found, rejects (user must sign up first).
   - Checks current balance + credit amount <= $50.00 max cap. Rejects if exceeded (or caps at max).
   - Inserts `PURCHASE` row into `wallet_ledger` with `balance_after_micro`.
2. Deploy the Edge Function: `supabase functions deploy lemon-webhook`.
3. **Configure webhook in Lemon Squeezy dashboard:**
   - URL: `https://<your-project>.supabase.co/functions/v1/lemon-webhook`
   - Events: select `order_created` only.
   - Paste signing secret into the Edge Function's Supabase secrets: `supabase secrets set LEMON_SIGNING_SECRET=<your-secret>`.
4. Test with Lemon Squeezy test mode: simulate purchase, verify ledger row appears in Supabase, verify balance updates.

**Commit:** `feat(payments): configure Lemon Squeezy webhooks for wallet top-ups [M.3]`

**Files touched:** 1 Edge Function + Lemon Squeezy dashboard config

---

### Session 8: Operation Liquidity `[M.4]`

**Goal:** Emergency shutdown and refund capability. Must be tested before wallet goes live.

**Manual Setup (YOU must do before this session):**
1. **Lemon Squeezy API key:** Go to [app.lemonsqueezy.com](https://app.lemonsqueezy.com) → Settings → API. Create an API key. The refund Edge Function uses this to issue partial refunds programmatically via `POST https://api.lemonsqueezy.com/v1/refunds`.
2. **Store it as a Supabase secret:** `supabase secrets set LEMON_API_KEY=<your-key>`.
3. **Create a `config` table in Supabase** (can be done via SQL Editor — single row, single column):
   ```sql
   CREATE TABLE IF NOT EXISTS config (
     key TEXT PRIMARY KEY,
     value TEXT NOT NULL
   );
   INSERT INTO config (key, value) VALUES ('service_frozen', 'false');
   ```

**Steps:**
1. Create Supabase Edge Function `operation-liquidity`:
   - **Freeze:** Sets `service_frozen` to `'true'` in the `config` table. The Cloudflare Worker checks this flag and rejects all requests with HTTP 503 when frozen.
   - **Snapshot:** Queries all users with `balance_after_micro > 0`, exports to JSON.
   - **Refund calculation:** For each user, calculates refund amount = remaining balance. Maps back to original Lemon Squeezy order IDs for partial refund API calls.
   - **Dry-run mode:** Outputs the refund plan without executing. Requires explicit `--execute` flag for real refunds.
2. Add `service_frozen` check to the Cloudflare Worker (1-line check at the top of each route):
   - Query `SELECT value FROM config WHERE key = 'service_frozen'`.
   - If `'true'`, return HTTP 503 with body `{"error": "Service temporarily unavailable"}`.
3. Test: freeze service, verify proxy rejects requests, verify snapshot output, verify dry-run refund plan is correct. Then unfreeze and verify service resumes.

**Commit:** `feat(governance): implement Operation Liquidity emergency freeze and refund protocol [M.4]`

**Files touched:** 1 Edge Function + minor Cloudflare Worker edit

---

### Session 9: Integration Test + Polish

**Goal:** End-to-end verification, final build, commit any remaining fixes.

**Steps:**
1. Full `dotnet build DiktaMe.sln -c Release` — 0 warnings, 0 errors.
2. Full `dotnet test DiktaMe.sln` — all tests pass.
3. Manual E2E test:
   - Sign up → verify promotional grant appears in local ledger.
   - Dictate → verify wallet balance decreases, transaction logged.
   - Chat → verify wallet is not used, BYOK/local provider required.
   - Top up via Lemon Squeezy test mode → verify balance increases.
   - Disconnect internet → verify wallet proxy fails gracefully with toast.
   - Reconnect → verify balance syncs on next request.
4. Update `DEVELOPMENT_ROADMAP.md` completion line with K.8-K.11 and M.1-M.4.
5. Final commit with any polish fixes.

**Commit:** `test(wallet): end-to-end integration verification [SPEC_008]`
