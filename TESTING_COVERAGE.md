# Testing Coverage Report — 2026-04-10

## Summary

| Metric | Value |
|--------|-------|
| **Total Tests** | 1,192 (1,090 unit + 102 integration) |
| **CI Tests** | ~1,090 (integration + hardware skipped on runners) |
| **Test Framework** | xUnit + Moq + FluentAssertions |
| **Test Files** | 71 |
| **Core Source Files** | 106 |
| **Core File-Level Coverage** | **95%** (57 / 60 testable classes) |
| **Estimated Line-Level Coverage** | **~80%** |

---

## Module Breakdown

| Module | Tests | Files | Covered / Testable | Notes |
|--------|-------|-------|-------------------|-------|
| **TTS** | 200 | 11 | 11 / 13 | All providers including Gemini TTS |
| **Config** | 157 | 12 | 11 / 12 | PipelineConfigManager, SettingsManager, PipelineFactory |
| **Pipeline** | 96 | 4 | 8 / 8 | All 8 pipelines including Refine edge cases |
| **STT** | 95 | 6 | 5 / 9 | Whisper tests skipped on CI (no GPU/model) |
| **LLM** | 90 | 3 | 6 / 7 | All 4 providers + router + ModelListService HTTP |
| **Data** | 84 | 5 | 5 / 6 | SQLite integration tests |
| **Audio** | 62 | 6 | 5 / 6 | Hardware-dependent tests skipped on CI |
| **System** | 50 | 3 | 2 / 3 | OllamaManager (33 unit + 9 integration) |
| **Account** | 45 | 5 | 5 / 6 | AccountService, JWT, TokenRefresh, WalletProxy |
| **Input** | 44 | 4 | 4 / 4 | Clipboard tests use `[Collection("Clipboard")]` |
| **Plugin** | 24 | 4 | 4 / 4 | EventBus, PluginManager, UIRegistry, SettingsStore |
| **Security** | 25 | 4 | 4 / 4 | LicenseManager (14 tests), ApiKeyValidator, PIIScrubber, SecureStorage |
| **Weather** | 13 | 1 | 1 / 1 | Open-Meteo + IP geolocation |
| **Vision** | 10 | 1 | 1 / 1 | VisionPipeline orchestration |
| **Root** | 10 | 2 | — | Localization + scaffold tests |
| **TOTAL** | **1,005** | **71** | **57 / 60** | (+83 [InlineData] theory cases = 1,090 CI) |

---

## Classes WITH Test Coverage (57 files)

### Account (5/6)
- `AccountService.cs` -> `AccountServiceTests.cs` (9 tests)
- `JwtDecoder.cs` -> `JwtDecoderTests.cs` + `JwtDecoderExtendedTests.cs`
- `TokenRefreshService.cs` -> `TokenRefreshServiceTests.cs`
- `WalletGeminiProxy.cs` -> `WalletGeminiProxyTests.cs`

### Audio (5/6)
- `AudioDeviceManager.cs` -> `AudioDeviceManagerTests.cs`
- `AudioDucker.cs` -> `AudioDuckerTests.cs`
- `AudioLevelMonitor.cs` -> `AudioLevelMonitorTests.cs`
- `AudioRecorder.cs` -> `AudioRecorderTests.cs`
- `MuteDetector.cs` -> `MuteDetectorTests.cs`

### Config (11/12)
- `DictationModeDefaults.cs` -> `DictationModeDefaultsTests.cs`
- `DictationModeManager.cs` -> `DictationModeManagerTests.cs`
- `PipelineConfigManager.cs` -> `PipelineConfigManagerTests.cs` (9 tests)
- `PipelineFactory.cs` -> `PipelineFactoryTests.cs` (17 tests)
- `ProfileManager.cs` -> `ProfileManagerTests.cs`
- `PromptDefaults.cs` -> `PromptDefaultsTests.cs`
- `PromptRepository.cs` -> `PromptRepositoryTests.cs`
- `SettingsManager.cs` -> `SettingsManagerTests.cs` (28 tests)
- `SnippetManager.cs` -> `SnippetManagerTests.cs` (19 tests)
- `TTSProviderFactory.cs` -> `TTSProviderFactoryTests.cs`
- `ApiCommandParser.cs` -> included in Config tests

### Data (5/6)
- `ConversationManager.cs` -> `ConversationManagerTests.cs` (20 tests, integration)
- `HistoryManager.cs` -> `HistoryManagerTests.cs` (18 tests, integration)
- `MetricsCollector.cs` -> `MetricsCollectorTests.cs`
- `NoteWriter.cs` -> `NoteWriterTests.cs` (16 tests, integration)
- `WalletManager.cs` -> `WalletManagerTests.cs` (23 tests, integration)

### Input (4/4)
- `ClipboardManager.cs` -> `ClipboardManagerTests.cs`
- `HotkeyManager.cs` -> `HotkeyManagerTests.cs`
- `HotkeyParser.cs` -> `HotkeyParserTests.cs`
- `TextInjector.cs` -> `TextInjectorTests.cs`

### LLM (6/7)
- `AnthropicProvider.cs` -> `LLMProviderTests.cs::AnthropicProviderTests` (8 tests)
- `GeminiProvider.cs` -> `LLMProviderTests.cs::GeminiProviderTests` (10 tests)
- `LLMRouter.cs` -> `LLMProviderTests.cs::LLMRouterTests` + `LLMRouterWalletTests.cs` (18 tests)
- `ModelListService.cs` -> `ModelListServiceTests.cs` (20 tests including HTTP-level)
- `OllamaProvider.cs` -> `LLMProviderTests.cs::OllamaProviderTests` (11 tests)
- `OpenAICompatibleProvider.cs` -> `LLMProviderTests.cs::OpenAICompatibleProviderTests` (14 tests)

### Pipeline (8/8)
- `AskPipeline.cs` -> `PipelineTests.cs`
- `ChatPipeline.cs` -> `ChatPipelineTests.cs` (19 tests)
- `DictationPipeline.cs` -> `PipelineTests.cs`
- `NotePipeline.cs` -> `PipelineTests.cs`
- `ReadSelectionPipeline.cs` -> `ReadSelectionPipelineTests.cs` (21 tests)
- `RefinePipeline.cs` -> `PipelineTests.cs` (8 tests)
- `StreamingDictationPipeline.cs` -> `StreamingDictationPipelineTests.cs` (21 tests)
- `TranslatePipeline.cs` -> `PipelineTests.cs`

### Plugin (4/4)
- `JsonPluginSettingsStore.cs` -> `JsonPluginSettingsStoreTests.cs`
- `PipelineEventBus.cs` -> `PipelineEventBusTests.cs` (10 tests)
- `PluginManager.cs` -> `PluginManagerTests.cs`
- `PluginUIRegistry.cs` -> `PluginUIRegistryTests.cs` (7 tests)

### Security (4/4)
- `ApiKeyValidator.cs` -> `ApiKeyValidatorTests.cs`
- `LicenseManager.cs` -> `LicenseManagerTests.cs` (14 tests)
- `PIIScrubber.cs` -> `PIIScrubberTests.cs`
- `SecureStorage.cs` -> `SecureStorageTests.cs`

### STT (5/9)
- `DeepgramProvider.cs` -> `DeepgramProviderTests.cs` (24 tests)
- `DeepgramStreamingProvider.cs` -> `DeepgramStreamingProviderTests.cs` (29 tests)
- `GeminiAudioProvider.cs` -> `GeminiAudioProviderTests.cs`
- `STTRouter.cs` -> `STTRouterTests.cs`
- `WhisperProvider.cs` -> `WhisperProviderTests.cs`

### SystemManagement (2/3)
- `OllamaManager.cs` -> `OllamaManagerTests.cs` (33 unit) + `OllamaIntegrationTests.cs` (9 integration)
- `OllamaSearchService.cs` -> `OllamaSearchServiceTests.cs`

### TTS (11/13)
- `DeepgramTtsProvider.cs` -> `DeepgramTtsProviderTests.cs`
- `GeminiTtsProvider.cs` -> `GeminiTtsProviderTests.cs` (13 tests)
- `InworldTtsProvider.cs` -> `InworldTtsProviderTests.cs` (23 tests)
- `KokoroModelManager.cs` -> `KokoroModelManagerTests.cs`
- `KokoroTtsProvider.cs` -> `KokoroTtsProviderTests.cs` (19 tests)
- `OpenAITtsProvider.cs` -> `OpenAITtsProviderTests.cs` (18 tests)
- `TextCleaner.cs` -> `TextCleanerTests.cs` (33 tests)
- `TtsPlayerService.cs` -> `TtsPlayerServiceTests.cs`
- `TTSRouter.cs` -> `TTSRouterTests.cs` (20 tests)
- `TtsSpeaker.cs` -> `TtsSpeakerTests.cs` (16 tests)

### Weather (1/1)
- `WeatherService.cs` -> `WeatherServiceTests.cs` (13 tests)

### Vision (1/1)
- `VisionPipeline.cs` -> `VisionPipelineTests.cs` (10 tests)

---

## Classes WITHOUT Test Coverage (3 testable)

| Class | Lines | Reason |
|-------|-------|--------|
| `WalletStreamingSTTProxy.cs` | 522 | ClientWebSocket + Gemini Live API streaming — no interface seam |
| `SystemWebSocketClient.cs` | 58 | Thin `ClientWebSocket` wrapper — passthrough, low risk |
| `HardwareInfoService.cs` | 115 | WMI + Win32 queries — not mockable without abstractions |

---

## Not Unit-Testable (46 files — by design)

**Interfaces (10):** `IAccountService`, `IAudioDataSource`, `ILLMProvider`, `ILLMProviderFactory`, `ISTTProvider`, `ISTTProviderFactory`, `IStreamingSTTProvider`, `ITTSProvider`, `ITTSProviderFactory`, `IWebSocketClient`

**Enums/Records/DTOs (20):** `AuthMode`, `Capabilities`, `PipelineOptions`, `PipelineResult`, `PipelineState`, `ModelInfo`, `ConversationRecord`, `WalletTransaction`, `TtsResult`, `AccountSettings`, `DictationMode`, `AppSettings`, `PipelineConfig`, `UtilityProfile`, `ModeSettings`, `OllamaCheckResult`, `OllamaModelDetail`, `OllamaRunningModel`, `OllamaModelInfo`, `ModelEntry`

**Win32/Hardware/ONNX (10):** `ScreenCapture.cs` (582 lines), `Annotations/*.cs` (9 classes, ~400 lines), `ImageProcessor.cs` (195 lines) — GDI/WinRT P/Invoke

**System/Auto-generated (6):** `CoreStrings.Designer.cs`, `AppSettingsContext.cs` (source gen), etc.

---

## App Layer (DiktaMe.App) — 0% Unit Tested

| Category | Files | Reason |
|----------|-------|--------|
| **Views/Pages** | 24 XAML + code-behind | WinUI 3 runtime dependency |
| **ViewModels** | 12 | CommunityToolkit MVVM + `DispatcherQueue` |
| **Services** | 6 | `ThemeService`, `NotificationService` — WinUI runtime |
| **Converters** | 8 | Simple value converters — low risk |
| **App entry** | 2 | `App.xaml.cs`, `Program.cs` |

Standard for WinUI 3 desktop apps. UI layer validated via manual E2E testing (see `MANUAL_TEST_PLAN.md`).

### Security Note

The App layer gap is an **orchestration gap, not a security gap.** All security-critical logic lives in tested Core classes:

| Concern | Tested In | Tests |
|---------|-----------|-------|
| License activation + anti-piracy | `LicenseManagerTests` | 14 |
| JWT storage + refresh | `TokenRefreshServiceTests`, `AccountServiceTests` | 14 |
| API key validation | `ApiKeyValidatorTests` | — |
| PII scrubbing | `PIIScrubberTests` | — |
| DPAPI secure storage | `SecureStorageTests` | — |
| Wallet balance + transactions | `WalletManagerTests` | 23 |

The untested ViewModels read tokens from `SecureStorage` and pass them as `Bearer` headers over HTTPS — standard usage, no crypto or validation logic. `WalletStreamingSTTProxy` forces WSS (rejects HTTP) and escapes URL parameters. No credentials are constructed, parsed, or validated in the App layer.

**Planned for v2.x:** Extract ViewModel orchestration into a testable `PipelineOrchestrator` in Core.

---

## Integration Tests (102 tests)

Integration tests hit real resources (SQLite, file system, Ollama API) and are **skipped in CI**. Run locally with `dotnet test --filter "Category=Integration"`.

| Class | Tests | What it exercises |
|-------|-------|-------------------|
| `HistoryManagerTests` | 18 | SQLite DB write/read, privacy levels, pruning |
| `WalletManagerTests` | 23 | SQLite transactions, balance tracking, expiry |
| `ConversationManagerTests` | 20 | SQLite conversations, messages, privacy |
| `NoteWriterTests` | 16 | File system write, path validation, security |
| `SettingsManagerTests` | 14 | JSON load/save, SanitizeNulls, migration, events |
| `OllamaIntegrationTests` | 9 | Real Ollama API: check, version, models, info |
| `SnippetManagerTests` | 2 | JSON persistence round-trip |

---

## CI Pipeline

- **Filter:** `Category!=Integration&Category!=Hardware`
- **Threshold:** Minimum 470 passing tests (enforced, auto-fails below)
- **Coverage:** Coverlet collects Cobertura XML, uploaded as artifact (14-day retention)
- **Lint:** `dotnet format --verify-no-changes` must pass
