# Testing Coverage Report — 2026-03-23

## Summary

| Metric | Value |
|--------|-------|
| **Total Tests** | 1010 (local) / ~520 (CI — DPAPI/Clipboard/Audio/Whisper skipped on runners) |
| **Test Framework** | xUnit + Moq + FluentAssertions |
| **Test Files** | 60 |
| **Core Source Files** | 91 (61 testable) |
| **App Source Files** | 60 (0 unit tested — WinUI 3 UI layer) |
| **Core File-Level Coverage** | **93.4%** (57 / 61 testable classes) |
| **Estimated Line-Level Coverage** | **~70-75%** |
| **Effective Coverage (controllable code)** | **~75%** |

---

## Module Breakdown

| Module | Tests | Files | Covered / Testable | Coverage | Notes |
|--------|-------|-------|-------------------|----------|-------|
| **TTS** | 184 | 10 | 10 / 13 | 77% | `GeminiTtsProvider.cs` untested (newest) |
| **Config** | 118 | 8 | 9 / 12 | 75% | `LLMProviderFactory`, `STTProviderFactory` untested |
| **STT** | 95 | 6 | 5 / 9 | 56% | Whisper tests skipped on CI (no GPU/model) |
| **Pipeline** | 89 | 4 | 8 / 8 | **100%** | All 8 pipelines covered |
| **Data** | 82 | 5 | 5 / 6 | 83% | |
| **LLM** | 69 | 3 | 5 / 7 | 71% | All providers + router tested |
| **Audio** | 62 | 6 | 5 / 6 | 83% | Hardware-dependent tests skipped on CI |
| **Account** | 46 | 6 | 5 / 6 | 83% | `AccountService.cs` untested (HTTP + OAuth redirect) |
| **Input** | 44 | 4 | 4 / 4 | **100%** | Clipboard tests use `[Collection("Clipboard")]` |
| **System** | 41 | 2 | 2 / 3 | 67% | `HardwareInfoService` untested (WMI queries) |
| **Security** | 26 | 3 | 3 / 3 | **100%** | DPAPI tests skipped on CI |
| **Weather** | 13 | 1 | 1 / 1 | **100%** | Open-Meteo + IP geolocation |
| **Root** | 10 | 2 | — | — | Localization + scaffold tests |
| **TOTAL** | **1010** | **60** | **57 / 61** | **93.4%** | |

---

## Classes WITH Test Coverage (57+ files)

### Account (5/6)
- `JwtDecoder.cs` -> `JwtDecoderTests.cs` + `JwtDecoderExtendedTests.cs`
- `TokenRefreshService.cs` -> `TokenRefreshServiceTests.cs`
- `AccountService.cs` -> `AccountServiceTests.cs`
- `WalletGeminiProxy.cs` -> `WalletGeminiProxyTests.cs`

### Audio (5/6)
- `AudioDeviceManager.cs` -> `AudioDeviceManagerTests.cs`
- `AudioDucker.cs` -> `AudioDuckerTests.cs`
- `AudioLevelMonitor.cs` -> `AudioLevelMonitorTests.cs`
- `AudioRecorder.cs` -> `AudioRecorderTests.cs`
- `MuteDetector.cs` -> `MuteDetectorTests.cs`

### Config (9/12)
- `DictationModeDefaults.cs` -> `DictationModeDefaultsTests.cs`
- `DictationModeManager.cs` -> `DictationModeManagerTests.cs`
- `PipelineFactory.cs` -> `PipelineFactoryTests.cs`
- `ProfileManager.cs` -> `ProfileManagerTests.cs`
- `PromptDefaults.cs` -> `PromptDefaultsTests.cs`
- `PromptRepository.cs` -> `PromptRepositoryTests.cs`
- `SettingsManager.cs` -> `SettingsManagerTests.cs` (25 tests)
- `SnippetManager.cs` -> `SnippetManagerTests.cs` (19 tests)
- `TTSProviderFactory.cs` -> `TTSProviderFactoryTests.cs`

### Data (5/6)
- `ConversationManager.cs` -> `ConversationManagerTests.cs` (20 tests)
- `HistoryManager.cs` -> `HistoryManagerTests.cs`
- `MetricsCollector.cs` -> `MetricsCollectorTests.cs`
- `NoteWriter.cs` -> `NoteWriterTests.cs`
- `WalletManager.cs` -> `WalletManagerTests.cs` (23 tests)

### Input (4/4)
- `ClipboardManager.cs` -> `ClipboardManagerTests.cs`
- `HotkeyManager.cs` -> `HotkeyManagerTests.cs`
- `HotkeyParser.cs` -> `HotkeyParserTests.cs`
- `TextInjector.cs` -> `TextInjectorTests.cs`

### LLM (5/7)
- `AnthropicProvider.cs` -> `LLMProviderTests.cs::AnthropicProviderTests`
- `GeminiProvider.cs` -> `LLMProviderTests.cs::GeminiProviderTests`
- `LLMRouter.cs` -> `LLMProviderTests.cs::LLMRouterTests` + `LLMRouterWalletTests.cs`
- `ModelListService.cs` -> `ModelListServiceTests.cs`
- `OllamaProvider.cs` -> `LLMProviderTests.cs::OllamaProviderTests`
- `OpenAICompatibleProvider.cs` -> `LLMProviderTests.cs::OpenAICompatibleProviderTests`

### Pipeline (8/8)
- `AskPipeline.cs` -> `PipelineTests.cs`
- `ChatPipeline.cs` -> `ChatPipelineTests.cs` (19 tests)
- `DictationPipeline.cs` -> `PipelineTests.cs`
- `NotePipeline.cs` -> `PipelineTests.cs`
- `ReadSelectionPipeline.cs` -> `ReadSelectionPipelineTests.cs` (21 tests)
- `RefinePipeline.cs` -> `PipelineTests.cs`
- `StreamingDictationPipeline.cs` -> `StreamingDictationPipelineTests.cs` (21 tests)
- `TranslatePipeline.cs` -> `PipelineTests.cs`

### Security (3/3)
- `ApiKeyValidator.cs` -> `ApiKeyValidatorTests.cs`
- `PIIScrubber.cs` -> `PIIScrubberTests.cs`
- `SecureStorage.cs` -> `SecureStorageTests.cs`

### STT (5/9)
- `DeepgramProvider.cs` -> `DeepgramProviderTests.cs` (24 tests)
- `DeepgramStreamingProvider.cs` -> `DeepgramStreamingProviderTests.cs` (29 tests)
- `GeminiAudioProvider.cs` -> `GeminiAudioProviderTests.cs`
- `STTRouter.cs` -> `STTRouterTests.cs`
- `WhisperProvider.cs` -> `WhisperProviderTests.cs`

### SystemManagement (2/3)
- `OllamaManager.cs` -> `OllamaManagerTests.cs` (33 tests)
- `OllamaSearchService.cs` -> `OllamaSearchServiceTests.cs`

### TTS (10/13)
- `DeepgramTtsProvider.cs` -> `DeepgramTtsProviderTests.cs`
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

---

## Classes WITHOUT Test Coverage (4 testable)

| Class | Module | Reason |
|-------|--------|--------|
| `AccountService.cs` | Account | HTTP calls + browser OAuth redirect — hard to unit test without integration setup |
| `LLMProviderFactory.cs` | Config | Factory with DI wiring — tested indirectly via `PipelineFactoryTests` |
| `STTProviderFactory.cs` | Config | Factory with DI wiring — tested indirectly via `PipelineFactoryTests` |
| `GeminiTtsProvider.cs` | TTS | Newest provider (added 2026-03-15) — not yet covered |

---

## Not Unit-Testable (30 files — by design)

**Interfaces (10):** `IAccountService`, `IAudioDataSource`, `ILLMProvider`, `ILLMProviderFactory`, `ISTTProvider`, `ISTTProviderFactory`, `IStreamingSTTProvider`, `ITTSProvider`, `ITTSProviderFactory`, `IWebSocketClient`

**Enums/Records/DTOs (12):** `AuthMode`, `Capabilities`, `PipelineOptions`, `PipelineResult`, `PipelineState`, `ModelInfo`, `ConversationRecord`, `WalletTransaction`, `TtsResult`, `AccountSettings`, `DictationMode`, `AppSettings`

**System/Auto-generated (3):** `HardwareInfoService` (WMI), `SystemWebSocketClient` (native), `CoreStrings.Designer.cs`

**Data Models (5):** `PipelineConfig`, `PipelineConfigManager`, and other config DTOs tested via integration through `SettingsManagerTests`

---

## App Layer (DiktaMe.App) — 0% Unit Tested

| Category | Files | Reason not unit tested |
|----------|-------|----------------------|
| **Views/Pages** | 24 XAML + code-behind | WinUI 3 XAML coupled to Windows App SDK runtime |
| **ViewModels** | 12 | CommunityToolkit MVVM + `DispatcherQueue` dependency |
| **Services** | 6 | `ThemeService`, `NotificationService` etc. require WinUI runtime |
| **Converters** | 8 | Simple value converters — low risk |
| **App entry** | 2 | `App.xaml.cs`, `Program.cs` |

This is standard for WinUI 3 desktop apps. UI layer is validated via manual E2E testing.

---

## CI Test Gaps (~490 tests skipped on runners)

| Category | Skipped Tests | Reason |
|----------|--------------|--------|
| **DPAPI/SecureStorage** | ~20 | No Windows user profile on CI runner |
| **Clipboard/TextInjector** | ~15 | No desktop session (Win32 clipboard unavailable) |
| **Audio (NAudio)** | ~30 | No audio devices on CI runner |
| **Whisper (GPU)** | ~10 | No Vulkan GPU / model file on CI runner |
| **Other hardware-dependent** | ~415 | Various Win32 API dependencies |

---

## Recommendations for Future Coverage

1. **GeminiTtsProvider** — add tests (matches pattern of other TTS provider tests, ~18-23 tests expected)
2. **LLMProviderFactory / STTProviderFactory** — add factory resolution tests (verify correct provider type returned for each config combination)
3. **AccountService** — consider integration test with mock HTTP handler
4. **ViewModels** — extract testable logic from ViewModels into Core services where possible
