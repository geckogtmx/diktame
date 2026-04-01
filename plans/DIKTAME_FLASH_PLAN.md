# dIKta.me Flash — Detailed Implementation Plan

## Vision

A **zero-configuration dictation tool**. Press hotkey → speak → text appears. No accounts, no cloud, no LLM, no TTS, no modes. Just Whisper turbo and text injection.

**Whisper turbo model ships bundled** — no downloads, no setup wizards. Install and dictate.

**User picks model at install.** Installer offers turbo (1.5 GB, best accuracy) or small (461 MB, fast download, 96-97% accuracy — plenty for LLM prompting). Both run sub-second on GPU. Requires a Vulkan-capable discrete GPU.

---

## Real-World VRAM Measurements (RTX 4060 Ti, March 31 2026)

Tested with dIKta.me running Whisper turbo + Ollama Gemma 3:1B + Chrome + YouTube + 3 monitors:

| Metric | Value |
|--------|-------|
| Whisper turbo resident model | **~1.3 GB VRAM** |
| Inference spike (during transcription) | **~400-540 MB temporary** |
| **Peak VRAM (model + inference)** | **~1.8 GB** |
| Total system VRAM at load | 4.5 / 8.0 GB (56%) |

The "6 GB VRAM required" from PyTorch docs is wildly inaccurate for GGML inference. Actual turbo VRAM is under 2 GB.

### Pending: GTX 1050 Ti Validation

A GTX 1050 Ti (4 GB, laptop) test will confirm whether turbo runs sub-second on older low-end discrete GPUs. If yes → turbo ships as the only model, no tiers needed. If not → small model fallback tier added.

---

## First-Run Wizard (in-app)

On first launch, a simple 3-step wizard:

```
┌─────────────────────────────────────────┐
│  Welcome to dIKta.me Flash              │
├─────────────────────────────────────────┤
│                                         │
│  Step 1: Hardware Detection             │
│                                         │
│  Scanning your system...                │
│                                         │
│  ✅ NVIDIA GeForce RTX 3060 (12 GB)    │
│  → Sub-second transcription ready       │
│                                         │
│  ── or ──                               │
│                                         │
│  ❌ No compatible GPU detected          │
│  → dIKta.me Flash requires a discrete   │
│    GPU with Vulkan support.             │
│                                         │
│                          [ Next → ]     │
├─────────────────────────────────────────┤
│  Step 2: Language                        │
│                                         │
│  ◉ English                              │
│  ○ Español                              │
│                                         │
│                 [ ← Back ] [ Next → ]   │
├─────────────────────────────────────────┤
│  Step 3: Hotkey                          │
│                                         │
│  Press your hotkey to dictate:          │
│  [ Ctrl + Alt + D ]  [Change]           │
│                                         │
│               [ ← Back ] [ Done ✓ ]    │
└─────────────────────────────────────────┘
```

After wizard: settings saved, model loaded, main window shown — ready to dictate.

---

## System Requirements

| Component | Requirement |
|-----------|-------------|
| **GPU** | **Vulkan-capable discrete GPU, 2 GB+ VRAM** |
| **RAM** | 8 GB system |
| **Disk** | 2 GB free (SSD recommended for faster model load) |
| **OS** | Windows 10 1903+ (build 19041) |

### Compatible Hardware

| GPU | VRAM | Status |
|-----|------|--------|
| RTX 3060 / 4060+ | 8-24 GB | ✅ Confirmed |
| RTX 2060 / GTX 1060 | 6 GB | ✅ Expected |
| GTX 1050 Ti | 4 GB | ⏳ **Pending test** |
| AMD RX 6600+ / RX 580 | 4-8 GB | ✅ Expected (Vulkan) |
| Intel Arc A580+ | 8 GB | ✅ Expected (Vulkan) |
| GTX 1050 2GB | 2 GB | ⚠️ Tight — needs testing |
| Intel UHD / Iris (integrated) | Shared | ❌ Not supported |
| No discrete GPU | — | ❌ Not supported |

---

## Distribution Size

| Component | Size |
|-----------|------|
| .NET 8 runtime (self-contained, trimmed) | ~45 MB |
| WinUI 3 SDK | ~35 MB |
| Whisper.net + Vulkan runtime DLLs | ~46 MB |
| NAudio + InputSimulator + Serilog | ~1 MB |
| App code | < 1 MB |
**Model options (user picks at install):**

| Model | VRAM usage | Accuracy (WER) | File size | Best for |
|-------|-----------|----------------|-----------|----------|
| **Turbo** | ~1.8 GB peak | ~2.5% | 1.5 GB | Emails, docs, precise output |
| **Small** | ~500 MB peak | ~3-4% | 461 MB | LLM prompting, chat, notes |

---

## Solution Structure

```
E:\git\diktame-flash\
├── DiktaMe.Flash.sln
├── publish-release.cmd
├── models\
│   └── ggml-large-v3-turbo.bin          # Bundled in publish output
├── src\
│   ├── DiktaMe.Flash.Core\              # Minimal class library
│   │   ├── DiktaMe.Flash.Core.csproj
│   │   ├── Audio\
│   │   │   ├── AudioRecorder.cs         # From Core — NAudio WaveInEvent → WAV
│   │   │   └── AudioDeviceManager.cs    # From Core — enumerate input devices
│   │   ├── STT\
│   │   │   ├── ISTTProvider.cs          # From Core — interface
│   │   │   ├── TranscriptionResult.cs   # From Core — result record
│   │   │   └── WhisperProvider.cs       # From Core — Whisper.net inference
│   │   ├── Input\
│   │   │   ├── TextInjector.cs          # From Core — clipboard + Ctrl+V
│   │   │   ├── ClipboardManager.cs      # From Core — Win32 P/Invoke
│   │   │   ├── HotkeyManager.cs         # From Core — Win32 RegisterHotKey
│   │   │   └── HotkeyParser.cs          # From Core — parse "Ctrl+Alt+D"
│   │   └── Pipeline\
│   │       ├── FlashPipeline.cs         # NEW — simplified: record → STT → inject
│   │       └── PipelineResult.cs        # From Core — result type (simplified)
│   │
│   └── DiktaMe.Flash.App\              # WinUI 3 app
│       ├── DiktaMe.Flash.App.csproj
│       ├── App.xaml / App.xaml.cs       # Minimal DI, single window
│       ├── MainWindow.xaml              # The entire UI
│       ├── MainWindow.xaml.cs
│       ├── FlashViewModel.cs            # All state: recording, settings, status
│       ├── WizardWindow.xaml            # First-run: GPU detect → language → hotkey
│       ├── WizardWindow.xaml.cs
│       ├── GpuDetector.cs               # Vulkan probe — returns GPU name + VRAM or null
│       ├── Assets\
│       │   └── icon.ico
│       └── Strings\                     # EN/ES localized strings (optional)
```

**~18 source files total** vs ~270+ in full dIKta.me.

---

## Files Copied from DiktaMe.Core

Each file is copied and simplified (strip unused parameters, remove dependencies on removed subsystems):

| Source (DiktaMe.Core) | Target (Flash.Core) | Changes |
|----------------------|---------------------|---------|
| `Audio/AudioRecorder.cs` | `Audio/AudioRecorder.cs` | Remove streaming events, keep batch WAV only |
| `Audio/AudioDeviceManager.cs` | `Audio/AudioDeviceManager.cs` | Keep `GetInputDevices()` + `ResolveDeviceIndex()` only |
| `STT/ISTTProvider.cs` | `STT/ISTTProvider.cs` | As-is |
| `STT/TranscriptionResult.cs` | `STT/TranscriptionResult.cs` | As-is |
| `STT/WhisperProvider.cs` | `STT/WhisperProvider.cs` | Remove `DownloadModelAsync()` (bundled), hardcode turbo, add app-dir model path resolution |
| `Input/TextInjector.cs` | `Input/TextInjector.cs` | Remove `CaptureSelection()`, `ReInjectLast()` |
| `Input/ClipboardManager.cs` | `Input/ClipboardManager.cs` | As-is |
| `Input/HotkeyManager.cs` | `Input/HotkeyManager.cs` | Reduce `HotkeyId` enum to single `Dictate` entry |
| `Input/HotkeyParser.cs` | `Input/HotkeyParser.cs` | As-is |

---

## New Files

### `FlashPipeline.cs` — The Heart

Simplified orchestrator replacing `DictationPipeline.cs`. No LLM, no snippets, no modes:

```
StartRecording()
  → AudioRecorder.StartRecording(deviceLabel, deviceId)
  → Wait for user stop (hotkey release or toggle)

StopAndTranscribe()
  → audioFile = AudioRecorder.StopRecordingAsync()
  → result = WhisperProvider.TranscribeAsync(audioFile, language)
  → TextInjector.InjectText(result.Text, trailingSpace: true)
  → return PipelineResult
```

State machine: `Idle → Recording → Transcribing → Idle`

### `FlashViewModel.cs` — Single ViewModel

Properties:
- `IsRecording` (bool) — toggle state
- `StatusText` (string) — "Ready" / "Recording..." / "Transcribing..." / "Done (1.2s)"
- `SelectedLanguage` (string) — "en" or "es"
- `Languages` (List) — `[("English", "en"), ("Espa\u00f1ol", "es")]`
- `SelectedDeviceIndex` (int) — mic picker
- `AudioDevices` (ObservableCollection) — from AudioDeviceManager
- `HotkeyText` (string) — current hotkey display, e.g. "Ctrl+Alt+D"
- `IsRecordingHotkey` (bool) — hotkey capture mode
- `TranscriptionTimeMs` (long) — last result timing

Commands:
- `ToggleRecordingCommand` — start/stop dictation
- `RecordHotkeyCommand` — enter hotkey capture mode
- `ResetHotkeyCommand` — reset to default

### `MainWindow.xaml` — The Entire UI

Single compact window (~400 x 350px). Layout:

```
┌─────────────────────────────────────┐
│  dIKta.me Flash              ─ □ ✕  │
├─────────────────────────────────────┤
│                                     │
│         ╔═══════════════╗           │
│         ║   ● RECORD    ║  ← Big   │
│         ║               ║    toggle │
│         ╚═══════════════╝    button │
│                                     │
│     Status: Ready          1.2s     │
│                                     │
├─────────────────────────────────────┤
│  Language   [ English      ▼ ]      │
│  Microphone [ Default      ▼ ]      │
│  Hotkey     [ Ctrl+Alt+D ] [Set]    │
└─────────────────────────────────────┘
```

- **Record button**: Large, centered, toggles recording. Changes color when active (red pulse).
- **Status line**: Shows pipeline state + last transcription latency.
- **Settings section**: Three rows at the bottom. Language dropdown (EN/ES), mic dropdown, hotkey with "Set" button.
- **No navigation**, no pages, no tabs. Everything on one screen.

---

## NuGet Packages

### Flash.Core.csproj
```xml
<PackageReference Include="Whisper.net" Version="1.9.0" />
<PackageReference Include="Whisper.net.Runtime" Version="1.9.0" />
<PackageReference Include="Whisper.net.Runtime.Vulkan" Version="1.9.0" />
<PackageReference Include="NAudio" Version="2.*" />
<PackageReference Include="NAudio.Wasapi" Version="2.*" />
<PackageReference Include="InputSimulatorStandard" Version="1.*" />
<PackageReference Include="Serilog" Version="3.*" />
<PackageReference Include="Serilog.Sinks.File" Version="5.*" />
```

### Flash.App.csproj
```xml
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.*" />
```

**Removed vs full dIKta.me**: No KokoroSharp, no HtmlAgilityPack, no SQLite, no ScreenRecorderLib, no WinUI3Localizer, no H.NotifyIcon, no CommunityToolkit.WinUI.Controls.Markdown, no Microsoft.Toolkit.Uwp.Notifications.

---

## Bundling the Whisper Model

### Build-time model inclusion

In `Flash.App.csproj`:
```xml
<!-- Both models in repo, Inno Setup picks which to include -->
<ItemGroup Condition="'$(FlashModel)' == 'turbo' Or '$(FlashModel)' == ''">
  <Content Include="..\..\models\ggml-large-v3-turbo.bin">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>models\ggml-large-v3-turbo.bin</Link>
  </Content>
</ItemGroup>
<ItemGroup Condition="'$(FlashModel)' == 'small'">
  <Content Include="..\..\models\ggml-small.bin">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>models\ggml-small.bin</Link>
  </Content>
</ItemGroup>
```

Simpler approach: both models in the `models/` folder, Inno Setup includes only the user's choice via `[Components]` section.

### Model resolution in WhisperProvider

Checks for whichever model was installed:
```csharp
private string ResolveModelPath()
{
    var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
    var turbo = Path.Combine(baseDir, "ggml-large-v3-turbo.bin");
    var small = Path.Combine(baseDir, "ggml-small.bin");

    if (File.Exists(turbo)) return turbo;  // Prefer turbo if present
    if (File.Exists(small)) return small;

    throw new FileNotFoundException("No Whisper model found. Reinstall dIKta.me Flash.");
}
```

### Git LFS for model file

The 1.5 GB model must use Git LFS:
```
# .gitattributes
models/*.bin filter=lfs diff=lfs merge=lfs -text
```

Alternatively, download during CI build via a script (avoids bloating the repo).

---

## Settings Persistence

Minimal JSON file at `%APPDATA%\DiktaMe.Flash\settings.json`:

```json
{
  "language": "en",
  "micDeviceIndex": 0,
  "hotkey": "Ctrl+Alt+D"
}
```

**3 settings. That's it.** Loaded on startup, saved on change. No SettingsManager — a simple `JsonSerializer` read/write is sufficient.

---

## Hotkey Implementation

Reuse `HotkeyManager.cs` + `HotkeyParser.cs` from Core:

1. **Startup**: Register configured hotkey (default `Ctrl+Alt+D`)
2. **Press**: Start recording → status "Recording..."
3. **Press again**: Stop recording → transcribe → inject → status "Done (1.2s)"
4. **Settings "Set" button**: Enter capture mode → next key combo is saved → re-register

Single `HotkeyId.Dictate` — all other IDs removed.

---

## Language Support

Two languages in the dropdown:
- **English** → passes `"en"` to `WhisperProvider.TranscribeAsync()`
- **Espa\u00f1ol** → passes `"es"`

Whisper turbo handles both natively. No additional model files needed — the same `ggml-large-v3-turbo.bin` supports 98+ languages.

Could expand to more languages later by simply adding entries to the dropdown — no code changes needed.

---

## Implementation Phases

### Phase 1: Scaffold (2h)
- [ ] Create `E:\git\diktame-flash\` repo
- [ ] Create `DiktaMe.Flash.sln` with two projects
- [ ] Set up csproj files with NuGet packages
- [ ] Configure `net8.0-windows10.0.19041.0`, self-contained, trimmed
- [ ] Add `.gitattributes` for LFS (model file)
- [ ] Download `ggml-large-v3-turbo.bin` to `models/`

### Phase 2: Core Library (3h)
- [ ] Copy and simplify the 9 source files listed above
- [ ] Write `FlashPipeline.cs` (record → STT → inject orchestration)
- [ ] Simplify `PipelineResult` (just Text, IsSuccess, LatencyMs)
- [ ] Strip `WhisperProvider` to turbo-only + bundled model path
- [ ] Strip `HotkeyManager` to single Dictate hotkey
- [ ] Verify: `dotnet build` compiles with 0 errors

### Phase 3: UI (3h)
- [ ] `MainWindow.xaml` — record button + status + 3 settings rows
- [ ] `FlashViewModel.cs` — state management, commands, device enumeration
- [ ] `App.xaml.cs` — minimal DI (no container needed, manual wiring)
- [ ] Wire hotkey → ViewModel toggle
- [ ] Wire settings persistence (load/save JSON)
- [ ] Style: clean, minimal, dark theme matching dIKta.me brand

### Phase 4: Integration & Polish (2h)
- [ ] Model bundling in csproj (Content → CopyToOutputDirectory)
- [ ] Test: Launch → Ctrl+Alt+D → speak → text appears in Notepad
- [ ] Test: Switch language ES → speak Spanish → correct transcription
- [ ] Test: Change mic → recording uses selected device
- [ ] Test: Change hotkey → new combo works
- [ ] Publish: `dotnet publish -c Release -r win-x64 --self-contained`
- [ ] Verify published size ~1.6 GB uncompressed

### Phase 5: First-Run Wizard (2h)
- [ ] Simple 3-step in-app wizard (only shown on first launch)
- [ ] Step 1 — **GPU Detection**: Auto-scan for Vulkan GPU. Show result: "✅ NVIDIA RTX 3060 detected — turbo mode (sub-second)" or "⚠️ No compatible GPU — standard mode (2-4s)"
- [ ] Step 2 — **Language**: Pick EN or ES (big radio buttons)
- [ ] Step 3 — **Hotkey**: Show default Ctrl+Alt+D, option to record custom
- [ ] "Done" → save settings, load model, ready to dictate
- [ ] Wizard state saved in settings.json (`"wizardCompleted": true`) — never shown again

### Phase 6: Installer (1h)
- [ ] Inno Setup script with model choice page:

```
┌─────────────────────────────────────────────┐
│  Choose your Whisper model                  │
│                                             │
│  ◉ Turbo (recommended)                      │
│    Best accuracy · ~1.8 GB VRAM · 809M      │
│    params · Near-perfect transcription       │
│                                             │
│  ○ Small                                    │
│    Good accuracy · ~500 MB VRAM · 244M      │
│    params · Great for LLM prompting          │
│                                             │
│  ┌───────────────────────────────────────┐  │
│  │ 💡 Both models run sub-second on GPU. │  │
│  │ Choose based on your available VRAM:  │  │
│  │                                       │  │
│  │ • 4 GB+ GPU → either works            │  │
│  │ • 2 GB GPU  → pick Small              │  │
│  │ • Not sure?  → pick Small, upgrade    │  │
│  │   later by reinstalling               │  │
│  └───────────────────────────────────────┘  │
│                                             │
│                     [ < Back ] [ Next > ]    │
└─────────────────────────────────────────────┘
```

- [ ] Desktop shortcut, Start Menu entry
- [ ] Uninstaller removes `%APPDATA%\DiktaMe.Flash\`

---

## What This Is NOT

- No LLM cleanup/reformatting of text
- No cloud anything (no accounts, no API keys, no wallet)
- No TTS (no text-to-speech feedback)
- No dictation modes (no Ask, Refine, Translate, Chat)
- No snippets or text expansion
- No tray icon (close = close)
- No vision/screenshot
- No history or metrics
- No license system — Flash is free, forever
- No auto-update mechanism
- No wizard/onboarding — just works

---

## Estimated Total Effort

| Phase | Hours |
|-------|-------|
| Scaffold | 2 |
| Core library | 3 |
| UI | 3 |
| Integration & polish | 2 |
| First-run wizard | 2 |
| Installer | 1 |
| **Total** | **~13 hours** |
