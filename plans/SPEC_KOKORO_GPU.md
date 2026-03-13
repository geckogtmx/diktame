# SPEC_KOKORO_GPU: Kokoro TTS DirectML GPU Acceleration

> **Status:** APPROVED — Ready to implement
> **Created:** 2026-03-12
> **Parent:** SPEC_003_V2 (TTS)
> **Goal:** Sub-250ms Kokoro TTS synthesis via DirectML GPU, with CPU fallback

---

## 0. Context & Motivation

Kokoro TTS currently runs on CPU via `KokoroSharp.CPU` with the int8 model (88MB). Synthesis latency is 3–5s for first inference (cold start), ~1.8s steady-state. The fan spins on every request because ONNX inference is compute-heavy even on CPU.

**Benchmarks** (from [KokoroSharp issue #39](https://github.com/Lyrcaxis/KokoroSharp/issues/39)):

| Config | Synthesis Time | Notes |
|--------|---------------|-------|
| CPU int8 | 1,887ms | Current configuration |
| CPU fp32 | 462ms | — |
| CPU fp16 | 432ms | — |
| **GPU fp32** | **134ms** | **Target: 3.4x faster than CPU** |
| GPU fp16 | 214ms | — |
| GPU int8 | 2,106ms | **TERRIBLE — slower than CPU** |

**Key insight:** The int8 quantized model is designed for CPU. GPU needs the fp32 (310MB) or a GPU-optimized model (`kokoro-quant-gpu.onnx`, 169MB).

---

## 1. Technical Analysis

### 1.1 Why DirectML (Not CUDA or Vulkan)

| Option | Cross-vendor | User Setup | Available |
|--------|:------------:|------------|:---------:|
| **DirectML** | AMD + NVIDIA + Intel | None (DX12 driver) | ✅ `KokoroSharp.DirectML` 0.6.5 |
| CUDA | NVIDIA only | CUDA 12.x + cuDNN | ✅ `KokoroSharp.GPU` 0.6.5 |
| Vulkan | All GPUs | — | ❌ ONNX Runtime has no Vulkan EP |

DirectML matches our Vulkan STT philosophy: cross-vendor, zero user setup, ships with Windows 10 1903+.

**Why no Vulkan?** ONNX Runtime (which KokoroSharp uses) has no Vulkan execution provider. Whisper.net's Vulkan works because it uses whisper.cpp/ggml, not ONNX Runtime. Different stack.

### 1.2 No Conflict with Whisper.net

Whisper.net uses its own native `whisper.dll` (via whisper.cpp P/Invoke), **not** `Microsoft.ML.OnnxRuntime`. The packages in `DiktaMe.Core.csproj`:

```
Whisper.net              → whisper.dll (whisper.cpp)
Whisper.net.Runtime.Vulkan → vulkan whisper.dll
KokoroSharp.DirectML     → onnxruntime.dll (ONNX Runtime + DirectML EP)
```

These are completely independent native runtimes. No DLL conflict.

### 1.3 KokoroSharp API — SessionOptions

`KokoroModel` accepts optional `SessionOptions`:

```csharp
public KokoroModel(string modelPath, SessionOptions options = null) {
    session = new InferenceSession(modelPath, options ?? defaultOptions);
}
```

Default `options` = CPU with 8 threads. To use DirectML:

```csharp
var options = new SessionOptions();
options.AppendExecutionProvider_DML();
var model = new KokoroModel(modelPath, options);
```

DirectML auto-falls back to CPU for unsupported ops, so no risk of hard failure.

### 1.4 Model Files

| Variant Key | File | Size | CPU | GPU | Use Case |
|-------------|------|------|-----|-----|----------|
| `int8` | `kokoro-quant-convinteger.onnx` | 88MB | 1,887ms | 2,106ms (BAD) | CPU-only (current default) |
| `fp16` | `kokoro-quant.onnx` | 169MB | 432ms | 214ms | Balanced |
| `fp32` | `kokoro.onnx` | 310MB | 462ms | **134ms** | Best GPU quality |
| `gpu` | `kokoro-quant-gpu.onnx` | 169MB | ? | ~134ms | GPU-optimized quantization |

All from `https://github.com/taylorchu/kokoro-onnx/releases/download/v0.2.0/`.

**Note:** The `kokoro-quant-gpu.onnx` model is not in KokoroSharp's `KModel` enum, but we manage our own model files via `KokoroModelManager` anyway.

### 1.5 Publish Size Impact

| Component | Size |
|-----------|------|
| `Microsoft.ML.OnnxRuntime.DirectML` | ~17MB |
| `Microsoft.AI.DirectML` (`DirectML.dll`) | ~193MB |
| **Total NuGet increase** | **~210MB** |
| Current published app | ~173MB |
| **New published app** | **~383MB** (uncompressed) |

This is significant. Mitigation: `DirectML.dll` compresses well (~60MB with zip). Published compressed size: ~130MB → ~190MB.

Alternative: Keep `KokoroSharp.CPU` as default, offer DirectML as opt-in download. But this complicates the build and deployment. Better to ship DirectML and let it fall back to CPU automatically when no compatible GPU is present.

---

## 2. Implementation Plan

### Phase 1: NuGet Swap + SessionOptions Wiring

**Goal:** Replace CPU runtime with DirectML, pass SessionOptions to KokoroModel.

#### Task 1.1: NuGet Package Swap

**File:** `src/DiktaMe.Core/DiktaMe.Core.csproj`

```diff
- <PackageReference Include="KokoroSharp.CPU" Version="0.6.5" />
+ <PackageReference Include="KokoroSharp.DirectML" Version="0.6.5" />
```

**Verify:** `dotnet build DiktaMe.sln -c Release` — should compile with 0 errors. DirectML NuGet pulls in `Microsoft.ML.OnnxRuntime.DirectML` + `Microsoft.AI.DirectML` automatically.

#### Task 1.2: KokoroTtsProvider — DirectML SessionOptions

**File:** `src/DiktaMe.Core/TTS/KokoroTtsProvider.cs`

Modify `EnsureModelLoadedAsync()` to pass DirectML `SessionOptions` to `KokoroModel`:

```csharp
private async Task EnsureModelLoadedAsync(CancellationToken cancellationToken)
{
    if (_model is not null)
        return;

    if (!_modelManager.IsModelDownloaded)
        throw new FileNotFoundException(
            "Kokoro TTS model not downloaded. Open Settings > Text-to-Speech to download it.");

    await Task.Run(() =>
    {
        lock (_lock)
        {
            if (_model is not null) return;
            var loadSw = Stopwatch.StartNew();

            var options = CreateSessionOptions();
            _model = new KokoroModel(_modelManager.ModelPath, options);

            if (!_voicesLoaded)
            {
                KokoroVoiceManager.LoadVoicesFromPath();
                _voicesLoaded = true;
            }

            Log.Information("KokoroTtsProvider: model loaded in {Ms}ms from {Path} (runtime={Runtime})",
                loadSw.ElapsedMilliseconds, _modelManager.ModelPath, _useGpu ? "DirectML" : "CPU");
        }
    }, cancellationToken);
}
```

Add `CreateSessionOptions()` method:

```csharp
private readonly bool _useGpu;

private SessionOptions CreateSessionOptions()
{
    var options = new SessionOptions
    {
        EnableMemoryPattern = true,
    };

    if (_useGpu)
    {
        try
        {
            options.AppendExecutionProvider_DML();
            Log.Information("KokoroTtsProvider: DirectML execution provider appended");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "KokoroTtsProvider: DirectML not available, falling back to CPU");
            // CPU fallback — SessionOptions without DirectML EP = CPU only
        }
    }
    else
    {
        options.InterOpNumThreads = 8;
        options.IntraOpNumThreads = 8;
    }

    return options;
}
```

Update constructor to accept `useGpu` parameter:

```csharp
public KokoroTtsProvider(string modelVariant = "int8", float speed = 1.0f, bool useGpu = true)
{
    _modelManager = new KokoroModelManager(modelVariant);
    _speed = Math.Clamp(speed, 0.5f, 2.0f);
    _useGpu = useGpu;
}
```

**Default `useGpu = true`** — DirectML auto-falls back to CPU for unsupported hardware, so this is safe. The `try/catch` around `AppendExecutionProvider_DML()` handles the case where the DirectML native DLL is missing.

#### Task 1.3: TTSProviderFactory — Pass useGpu

**File:** `src/DiktaMe.Core/Config/TTSProviderFactory.cs`

Update `CreateProviderCore()`:

```csharp
"kokoro" => new KokoroTtsProvider(
    modelVariant: variant,
    speed: (float)_settings.Current.Tts.Speed,
    useGpu: _settings.Current.Tts.KokoroUseGpu),
```

#### Task 1.4: TtsSettings — Add KokoroUseGpu

**File:** `src/DiktaMe.Core/Config/AppSettings.cs`

```csharp
public sealed record TtsSettings
{
    // ... existing fields ...

    /// <summary>Use DirectML GPU acceleration for Kokoro inference.</summary>
    public bool KokoroUseGpu { get; init; } = true;
}
```

Default `true` — DirectML safely falls back to CPU. Users can disable via Settings if GPU causes issues.

### Phase 2: GPU Model Variant + Settings UI

#### Task 2.1: Add GPU Model Variant to KokoroModelManager

**File:** `src/DiktaMe.Core/TTS/KokoroModelManager.cs`

Add `gpu` variant to `ModelMap`:

```csharp
private static readonly IReadOnlyDictionary<string, (string FileName, long ApproxSizeMb)> ModelMap =
    new Dictionary<string, (string, long)>(StringComparer.OrdinalIgnoreCase)
    {
        ["int8"] = ("kokoro-quant-convinteger.onnx", 88),
        ["fp16"] = ("kokoro-quant.onnx", 169),
        ["fp32"] = ("kokoro.onnx", 310),
        ["gpu"]  = ("kokoro-quant-gpu.onnx", 169),   // GPU-optimized quantization
    };
```

#### Task 2.2: TTS Settings UI — GPU Toggle + Variant

**File:** `src/DiktaMe.App/ViewModels/Settings/TtsSettingsViewModel.cs`

- Add `KokoroUseGpu` observable property
- Update `KokoroVariantKeys` and `KokoroVariantLabels`:
  ```csharp
  public static readonly string[] KokoroVariantKeys = ["gpu", "fp32", "fp16", "int8"];
  public string[] KokoroVariantLabels { get; } = [
      "GPU Optimized (169 MB, fastest)",
      "fp32 (310 MB, best quality)",
      "fp16 (169 MB, balanced)",
      "int8 (88 MB, CPU only)"
  ];
  ```
- Default variant → `"gpu"` for new installs (change `TtsSettings.KokoroModelVariant` default from `"int8"` to `"gpu"`)
- Add GPU toggle checkbox bound to `KokoroUseGpu` in `TtsSettingsPage.xaml`
- `LoadFromSettings()` / `Save()` — wire `KokoroUseGpu`

**File:** `src/DiktaMe.App/Views/Settings/TtsSettingsPage.xaml`

Add after the model variant ComboBox:
```xml
<CheckBox Content="Use GPU acceleration (DirectML)"
          IsChecked="{x:Bind ViewModel.KokoroUseGpu, Mode=TwoWay}" />
```

#### Task 2.3: Invalidate TTSProviderFactory Cache on GPU/Variant Change

**Problem:** If the user changes GPU toggle or model variant in Settings, the cached `KokoroTtsProvider` in `TTSProviderFactory._cache` still uses the old settings.

**Fix:** Include `useGpu` in the cache key:

```csharp
// In CreateProvider():
string cacheKey = type == "kokoro"
    ? $"{type}:{variant}:{(_settings.Current.Tts.KokoroUseGpu ? "gpu" : "cpu")}"
    : $"{type}:{variant}";
```

Also add a `ClearCache()` method to `TTSProviderFactory` called when Kokoro settings change:

```csharp
public void ClearCache()
{
    foreach (var provider in _cache.Values)
    {
        if (provider is IDisposable d) d.Dispose();
    }
    _cache.Clear();
    Log.Information("TTSProviderFactory: cache cleared");
}
```

Wire `ClearCache()` call in `TtsSettingsViewModel` when variant or GPU toggle changes.

### Phase 3: Default Change + Migration

#### Task 3.1: Change Default for New Installs

**File:** `src/DiktaMe.Core/Config/AppSettings.cs`

```csharp
public string KokoroModelVariant { get; init; } = "gpu";  // was "int8"
public bool KokoroUseGpu { get; init; } = true;
```

New users get GPU-optimized by default. Existing users keep their current variant (persisted in `settings.json`).

#### Task 3.2: Existing Users — No Migration Needed

Existing `settings.json` with `"KokoroModelVariant":"int8"` will continue working. The int8 model on DirectML will be ~2s (worse than CPU), but:

- User can switch variant in Settings > TTS > Model dropdown
- `KokoroUseGpu` defaults to `true` but int8+GPU is just slow, not broken
- The Settings UI labels now show "(CPU only)" next to int8, guiding the user

No forced migration avoids breaking existing setups.

---

## 3. Fallback & Safety

### 3.1 DirectML Failure → CPU Fallback

DirectML EP append is wrapped in try/catch. If `AppendExecutionProvider_DML()` throws (missing `DirectML.dll`, incompatible GPU driver), we log a warning and continue with CPU-only `SessionOptions`.

### 3.2 GPU Model Not Downloaded → Clear Error

`KokoroModelManager` already handles missing model files with `FileNotFoundException`. The Settings UI shows "Download" button per variant.

### 3.3 VRAM Exhaustion

DirectML handles this internally — it falls back to system RAM if GPU VRAM is insufficient. Performance degrades but doesn't crash.

### 3.4 Rollback Plan

If DirectML causes issues in production:

1. **Quick fix:** User toggles off "Use GPU acceleration" in Settings → forces CPU SessionOptions
2. **NuGet rollback:** Revert `KokoroSharp.DirectML` → `KokoroSharp.CPU` in csproj, remove `CreateSessionOptions()`, hardcode CPU options. One-commit revert.

### 3.5 int8 + GPU Warning

If user selects `int8` variant with GPU enabled, show an InfoBar warning in TTS Settings:

> "The int8 model runs slower on GPU than CPU. For best GPU performance, select 'GPU Optimized' or 'fp32'."

---

## 4. TTSProviderFactory Cache Key Update

The `ConcurrentDictionary` cache key must change to include GPU state, otherwise switching GPU on/off in Settings won't take effect until app restart.

Current: `"kokoro:int8"`
New: `"kokoro:gpu:gpu"` or `"kokoro:fp32:cpu"`

Format: `"{provider}:{variant}:{runtime}"`

---

## 5. Testing

### 5.1 Unit Tests

| Test | What |
|------|------|
| `KokoroTtsProvider` constructor with `useGpu=true` — no throw | Verify construction doesn't fail |
| `KokoroTtsProvider` constructor with `useGpu=false` — no throw | CPU fallback path |
| `KokoroModelManager` with `"gpu"` variant — correct file path | New variant registered |
| `KokoroModelManager.GetApproxSizeMb("gpu")` returns 169 | Size lookup |
| `TTSProviderFactory` cache key includes GPU state | Cache invalidation |

### 5.2 Manual E2E

1. Download GPU model via Settings > TTS > Model dropdown > "GPU Optimized (169 MB)"
2. Enable GPU toggle
3. "Test Voice" — verify audio plays, log shows `runtime=DirectML`
4. Check synthesis latency in logs — expect <250ms
5. Ctrl+Alt+Q with selected text — verify Read Selection works
6. Disable GPU toggle → re-test → log shows `runtime=CPU`, latency ~450ms
7. Select int8 + GPU → verify InfoBar warning appears

---

## 6. File Change Summary

| File | Change |
|------|--------|
| `DiktaMe.Core.csproj` | `KokoroSharp.CPU` → `KokoroSharp.DirectML` |
| `AppSettings.cs` | Add `KokoroUseGpu`, change default variant to `"gpu"` |
| `KokoroTtsProvider.cs` | Add `useGpu` param, `CreateSessionOptions()`, DirectML EP |
| `KokoroModelManager.cs` | Add `"gpu"` variant to `ModelMap` |
| `TTSProviderFactory.cs` | GPU-aware cache key, `ClearCache()` method |
| `TtsSettingsViewModel.cs` | GPU toggle, updated variant labels, cache clear on change |
| `TtsSettingsPage.xaml` | GPU toggle CheckBox, int8+GPU InfoBar warning |
| `KokoroTtsProviderTests.cs` | New tests for `useGpu` parameter |
| `KokoroModelManagerTests.cs` | Test `"gpu"` variant |

---

## 7. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| DirectML DLL missing on some Windows builds | Low | App still works (CPU fallback) | try/catch around EP append |
| int8 model slower on GPU | Certain | User confusion | InfoBar warning + label "(CPU only)" |
| ~210MB publish size increase | Certain | Larger installer | Compresses to ~60MB; acceptable for GPU acceleration |
| ONNX Runtime version conflict with future packages | Low | Build error | Pin version in csproj |
| DirectML first-inference shader compile (cold start) | Likely | ~1-2s penalty on first call | Same as Vulkan STT — expected, logged |

---

## 8. Expected Results

| Metric | Before (CPU int8) | After (DirectML fp32/gpu) |
|--------|-------------------|---------------------------|
| Synthesis latency | 1,800–5,000ms | **130–250ms** |
| Cold start | 3–5s | ~2–3s (shader compile + model load) |
| Fan noise | Noticeable | Brief spike, much shorter |
| Model size | 88MB | 169MB (gpu) or 310MB (fp32) |
| Publish size | 173MB | ~383MB (uncompressed) |

**Target: Sub-250ms steady-state synthesis with DirectML GPU.**
