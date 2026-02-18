# CI / Analyzer Decisions Log

Decisions made to suppress or adjust CI rules, with rationale and revisit notes.

---

## Meziantou.Analyzer suppressions (`.editorconfig` + `.csproj`)

### MA0002 — `none` (IEqualityComparer\<string\>)
- **Why suppressed**: We use `StringComparer.OrdinalIgnoreCase` explicitly where needed. The blanket warning fires on dictionary constructors that already have explicit comparers. Noisy with no real benefit.
- **Revisit when**: MA0002 is refined to only fire when no comparer is specified.

### MA0003 — `none` (MethodImpl AggressiveInlining)
- **Why suppressed**: Micro-optimization noise. We are not a hot-path library.
- **Revisit when**: Never (micro-opt hints don't belong in application code).

### MA0004 — `none` (ConfigureAwait(false))
- **Why suppressed**: WinUI 3 requires the synchronization context to be preserved for UI thread marshalling. `ConfigureAwait(false)` in WinUI 3 code causes `InvalidOperationException` when continuations try to access UI elements.
- **Revisit when**: If we separate UI code (App project) from core logic (Core project) into separate `await` domains. Core project could potentially use `ConfigureAwait(false)`.

### MA0009 — `none` (Regex timeout)
- **Why suppressed**: All regexes operate on short, bounded user-input strings (dictation output, API keys). The patterns are not backtracking-prone and a denial-of-service scenario doesn't apply.
- **Revisit when**: Any regex is added that operates on unbounded external input.

### MA0016 — `none` (Prefer IReadOnlyList/IReadOnlyCollection)
- **Why suppressed**: Our public APIs use concrete types internally; changing to read-only interfaces would require cast-throughs in callers without real benefit.
- **Revisit when**: We have a public NuGet package surface or add external consumers.

### MA0046 — `none` (Use EventHandler\<T\>)
- **Why suppressed**: We do use `EventHandler<T>` for all events. The rule fires on inherited WinUI 3 event patterns that follow Microsoft's own API conventions.
- **Revisit when**: MA0046 is scoped to exclude WinUI/WPF framework event signatures.

### MA0048 / MA0049 — `none` (File name / type name match)
- **Why suppressed**: We deliberately have files with multiple related types (e.g. record + factory in one file). Splitting would create file bloat.
- **Revisit when**: Any file grows beyond ~150 lines, at which point splitting is warranted anyway.

### MA0051 — `warning`, limits: 150 lines / 80 statements
- **Default limits raised from**: 60 lines / 40 statements
- **Why raised**: `RunAsync` pipeline orchestrators are inherently sequential flows with 47–70 statements. Breaking them into smaller methods would require passing state via parameters or fields, reducing clarity. 150/80 catches genuinely bloated methods.
- **Revisit when**: Any pipeline orchestrator exceeds 150 lines — that signals it needs to be split.

### MA0074 — suppressed in test project only (`<NoWarn>MA0074</NoWarn>`)
- **Why**: FluentAssertions `.Contains()`, `.EndsWith()` etc. use `StringComparison.Ordinal` internally but the Meziantou rule fires on the call site. Test assertions are not locale-sensitive.
- **Revisit when**: FluentAssertions exposes explicit `StringComparison` overloads (or MA0074 is fixed to detect FA calls).

### MA0132 — `none` (DateTimeOffset instead of DateTime)
- **Why suppressed**: `DateTime.Now` is used for local-time note timestamps shown to the user (e.g. `## 2026-02-18 22:00`). `DateTimeOffset` would be more correct for storage, but the note format is display-only.
- **Revisit when**: Notes need to be parsed back or compared across timezones.

---

## Editorconfig naming rule scoping

### Private field `_camelCase` rule — restricted to instance fields only
- **Why**: The original rule applied to all `private` fields including `static readonly` and `const`. C# convention (and Microsoft guidelines) use PascalCase for constants and static readonly fields (`EmailPattern`, `PollIntervalMs`, etc.).
- **Fix**: Added a higher-priority rule for `private static` fields requiring PascalCase, leaving `_camelCase` for instance fields only.
- **Revisit when**: Never — this is correct C# convention.

---

## gitleaks — `.gitleaks.toml` allowlist

### `AIzaSyABCDEFGHIJKLMNOPQRSTUVWXYZ0123456` in ApiKeyValidatorTests.cs
- **Why suppressed**: Synthetic test fixture value — sequential alphabet characters are clearly not a real GCP API key. Gitleaks' `gcp-api-key` rule matches the `AIzaSy` prefix + 33-char suffix regardless of entropy-based plausibility.
- **Config**: `[allowlist]` with `regexTarget = "secret"` in `.gitleaks.toml`; `--config .gitleaks.toml` passed to the CI detect command.
- **Revisit when**: A new test is added that looks superficially real. Only suppress values that are provably synthetic (sequential chars, all-zeros, etc.). Real-looking values should be replaced with a different pattern.

---

## dotnet format — IDE1006 unfixable by `--fix`
- `dotnet format` cannot auto-fix `IDE1006` (naming violations) because renaming fields is a refactoring, not a formatting operation.
- The `NamingStyleCodeFixProvider` does not support "Fix All in Solution".
- **Current approach**: Editorconfig rules are scoped correctly so no violations exist.
- **Revisit when**: A future `dotnet format` version adds safe rename support.
