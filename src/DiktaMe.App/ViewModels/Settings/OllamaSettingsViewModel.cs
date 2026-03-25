
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.SystemManagement;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class OllamaSettingsViewModel : ObservableObject
{
    private readonly OllamaManager _ollamaManager;
    private readonly OllamaSearchService _searchService;
    private readonly HardwareInfoService _hardwareService;
    private readonly OllamaProvider _ollamaProvider;
    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;

    private static readonly string[] KeepAliveValues = ["5m", "10m", "30m", "1h", "2h"];
    private static readonly int[] NumCtxValues = [2048, 4096, 8192, 16384];

    // ── Service Status ───────────────────────────────────────────────────
    [ObservableProperty] private string _ollamaVersion = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private OllamaStatus _status = OllamaStatus.Offline;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private int _runningModelCount;
    [ObservableProperty] private string _vramSummary = "";

    // ── 412 Rescue ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _showRescue;
    [ObservableProperty] private string _rescueMessage = "";
    [ObservableProperty] private string _fallbackModel = "";

    // ── Default Model ────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedModel = "gemma3:1b";
    [ObservableProperty] private int _selectedModelIndex = -1;
    [ObservableProperty] private int _keepAliveIndex = 1;
    [ObservableProperty] private bool _isWarmingUp;

    // ── Installed Models ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<OllamaModelDetail> _installedModels = new();
    [ObservableProperty] private string _totalDiskUsage = "";
    [ObservableProperty] private string _pullModelName = "";
    [ObservableProperty] private bool _isPulling;
    [ObservableProperty] private double _pullProgress;
    [ObservableProperty] private string _pullStatusText = "";

    // ── VRAM Monitor ─────────────────────────────────────────────────────
    [ObservableProperty] private string _gpuName = "";
    [ObservableProperty] private double _vramTotalGB;
    [ObservableProperty] private double _vramUsedGB;
    [ObservableProperty] private double _vramPercent;
    [ObservableProperty] private string _ramSummary = "";
    [ObservableProperty] private string _lastInferenceSpeed = "";
    [ObservableProperty] private ObservableCollection<OllamaRunningModel> _runningModels = new();

    // ── Model Library ────────────────────────────────────────────────────
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private ObservableCollection<OllamaSearchResult> _searchResults = new();
    [ObservableProperty] private string _searchStatusText = "";

    // ── Install Flow ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _showInstall;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _installButtonText = "";
    [ObservableProperty] private bool _showManualInstallLink;
    [ObservableProperty] private string _installError = "";
    [ObservableProperty] private bool _showInstallError;

    // ── Advanced ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _ollamaBaseUrl = "http://localhost:11434";
    [ObservableProperty] private bool _autoWarmup;
    [ObservableProperty] private int _numCtxIndex;

    // ── Model Info Flyout ────────────────────────────────────────────────
    [ObservableProperty] private bool _showModelInfo;
    [ObservableProperty] private OllamaModelInfo? _modelInfoDetail;
    [ObservableProperty] private string _modelInfoTitle = "";

    public OllamaSettingsViewModel(
        OllamaManager ollamaManager,
        OllamaSearchService searchService,
        HardwareInfoService hardwareService,
        OllamaProvider ollamaProvider,
        SettingsManager settings,
        LocalizationService loc)
    {
        _ollamaManager = ollamaManager;
        _searchService = searchService;
        _hardwareService = hardwareService;
        _ollamaProvider = ollamaProvider;
        _settings = settings;
        _loc = loc;

        // Load from settings
        var current = settings.Current;
        SelectedModel = current.OllamaModel;
        OllamaVersion = _loc.GetString("Settings_Ollama_Status_Unknown");
        StatusText = _loc.GetString("Settings_Ollama_Status_NotChecked");

        int keepIdx = Array.IndexOf(KeepAliveValues, current.OllamaKeepAlive);
        _keepAliveIndex = keepIdx >= 0 ? keepIdx : 1;

        _ollamaBaseUrl = current.OllamaBaseUrl;
        _autoWarmup = current.OllamaAutoWarmup;
        int ctxIdx = Array.IndexOf(NumCtxValues, current.OllamaNumCtx);
        _numCtxIndex = ctxIdx >= 0 ? ctxIdx : 0;
    }

    // ── Service Status Commands ──────────────────────────────────────────

    [RelayCommand]
    private async Task CheckHealthAsync()
    {
        IsChecking = true;
        ShowRescue = false;
        ShowInstall = false;
        ShowInstallError = false;
        ShowManualInstallLink = false;
        try
        {
            var result = await _ollamaManager.CheckAsync(SelectedModel);
            Status = result.Status;
            OllamaVersion = result.OllamaVersion ?? _loc.GetString("Settings_Ollama_Status_Unknown");

            StatusText = result.Status switch
            {
                OllamaStatus.Ready => _loc.GetString("Settings_Ollama_Status_Ready"),
                OllamaStatus.Offline => _loc.GetString("Settings_Ollama_Status_Offline"),
                OllamaStatus.VersionTooOld => _loc.GetFormatted("Settings_Ollama_Status_TooOld",
                    result.OllamaVersion ?? "", result.RequiredVersion ?? ""),
                OllamaStatus.ModelNotPulled => _loc.GetFormatted("Settings_Ollama_Status_ModelNotInstalled",
                    SelectedModel),
                _ => _loc.GetString("Settings_Ollama_Status_Unknown"),
            };

            if (result.Status == OllamaStatus.VersionTooOld)
            {
                ShowRescue = true;
                RescueMessage = _loc.GetFormatted("Settings_Ollama_Rescue_Message",
                    SelectedModel, result.RequiredVersion ?? "", result.OllamaVersion ?? "");
                FallbackModel = result.FallbackModel ?? "gemma3:1b";
            }

            if (result.Status == OllamaStatus.Offline)
            {
                ShowInstall = true;
                InstallButtonText = _loc.GetString("Settings_Ollama_InstallButton");
            }

            // Refresh installed models + running models + hardware
            await Task.WhenAll(
                RefreshInstalledModelsAsync(),
                RefreshRunningModelsAsync(),
                RefreshHardwareInfoAsync());
        }
        catch (Exception ex)
        {
            StatusText = _loc.GetString("Settings_Ollama_Status_CheckFailed");
            Log.Error(ex, "Ollama health check failed");
            ShowInstall = true;
            InstallButtonText = _loc.GetString("Settings_Ollama_InstallButton");
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task RestartServiceAsync()
    {
        IsChecking = true;
        StatusText = "Restarting...";
        try
        {
            bool ok = await _ollamaManager.RestartServiceAsync();
            if (ok)
            {
                await CheckHealthAsync();
            }
            else
            {
                StatusText = "Restart failed — could not start Ollama";
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaSettings: restart failed");
            StatusText = $"Restart failed: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    // ── Default Model Commands ───────────────────────────────────────────

    [RelayCommand]
    private async Task WarmupModelAsync()
    {
        IsWarmingUp = true;
        try
        {
            await _ollamaProvider.WarmUpAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaSettings: warmup failed");
        }
        finally
        {
            IsWarmingUp = false;
            await RefreshRunningModelsAsync();
        }
    }

    [RelayCommand]
    private async Task UseFallbackAsync()
    {
        SelectedModel = FallbackModel;
        var updated = _settings.Current with { OllamaModel = FallbackModel };
        await _settings.UpdateAsync(updated);
        ShowRescue = false;
        StatusText = _loc.GetFormatted("Settings_Ollama_Rescue_Switched", FallbackModel);
    }

    partial void OnSelectedModelIndexChanged(int value)
    {
        if (value >= 0 && value < InstalledModels.Count)
        {
            SelectedModel = InstalledModels[value].Name;
        }
    }

    partial void OnSelectedModelChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        var updated = _settings.Current with { OllamaModel = value };
        _ = _settings.UpdateAsync(updated);
    }

    partial void OnKeepAliveIndexChanged(int value)
    {
        if (value >= 0 && value < KeepAliveValues.Length)
        {
            var updated = _settings.Current with { OllamaKeepAlive = KeepAliveValues[value] };
            _ = _settings.UpdateAsync(updated);
        }
    }

    // ── Installed Models Commands ────────────────────────────────────────

    [RelayCommand]
    private async Task PullModelAsync()
    {
        if (string.IsNullOrWhiteSpace(PullModelName))
        {
            return;
        }

        IsPulling = true;
        PullProgress = 0;
        PullStatusText = "Starting...";

        var progress = new Progress<OllamaPullProgress>(p =>
        {
            PullStatusText = p.Status;
            if (p.Total > 0)
            {
                PullProgress = (double)p.Completed / p.Total * 100;
            }
        });

        try
        {
            await _ollamaManager.PullModelAsync(PullModelName.Trim(), progress);
            PullStatusText = "Complete!";
            PullModelName = "";
            await RefreshInstalledModelsAsync();
        }
        catch (Exception ex)
        {
            PullStatusText = $"Failed: {ex.Message}";
            Log.Warning(ex, "OllamaSettings: pull failed for '{Model}'", PullModelName);
        }
        finally
        {
            IsPulling = false;
        }
    }

    [RelayCommand]
    private async Task DeleteModelAsync(OllamaModelDetail? model)
    {
        if (model is null)
        {
            return;
        }

        // Prevent deleting the active model
        if (string.Equals(NormaliseTag(model.Name), NormaliseTag(SelectedModel), StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("OllamaSettings: blocked deletion of active model '{Model}'", model.Name);
            return;
        }

        bool ok = await _ollamaManager.DeleteModelAsync(model.Name);
        if (ok)
        {
            await RefreshInstalledModelsAsync();
        }
    }

    [RelayCommand]
    private async Task ShowModelInfoAsync(OllamaModelDetail? model)
    {
        if (model is null)
        {
            return;
        }

        ModelInfoTitle = model.Name;
        ModelInfoDetail = await _ollamaManager.GetModelInfoAsync(model.Name);
        ShowModelInfo = ModelInfoDetail is not null;
    }

    // ── Search Commands ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task SearchModelsAsync()
    {
        IsSearching = true;
        SearchStatusText = "";
        try
        {
            var results = await _searchService.SearchModelsAsync(SearchQuery);
            SearchResults.Clear();
            foreach (var r in results)
            {
                SearchResults.Add(r);
            }

            if (results.Count == 0)
            {
                SearchStatusText = string.IsNullOrWhiteSpace(SearchQuery)
                    ? "No models found"
                    : $"No results for \"{SearchQuery}\"";
            }
        }
        catch (Exception ex)
        {
            SearchStatusText = "Search unavailable — you can still pull models by name above.";
            Log.Warning(ex, "OllamaSettings: search failed");
        }
        finally
        {
            IsSearching = false;
        }
    }

    // ── Install Commands ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task InstallOllamaAsync()
    {
        IsInstalling = true;
        ShowInstallError = false;

        var progress = new Progress<string>(msg => InstallButtonText = msg);

        try
        {
            var (result, error) = await OllamaManager.InstallViaWingetAsync(progress);

            switch (result)
            {
                case OllamaInstallResult.Success:
                    InstallButtonText = _loc.GetString("Settings_Ollama_Status_Ready");
                    ShowInstall = false;
                    await _ollamaManager.StartOllamaAsync();
                    await CheckHealthAsync();
                    break;

                case OllamaInstallResult.WingetNotFound:
                    OpenOllamaWebsite();
                    ShowManualInstallLink = true;
                    InstallButtonText = _loc.GetString("Settings_Ollama_RetryCheck");
                    break;

                case OllamaInstallResult.Failed:
                    InstallError = _loc.GetFormatted("Settings_Ollama_InstallFailed", error ?? "Unknown error");
                    ShowInstallError = true;
                    ShowManualInstallLink = true;
                    InstallButtonText = _loc.GetString("Settings_Ollama_RetryCheck");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Settings: Ollama install failed");
            InstallError = _loc.GetFormatted("Settings_Ollama_InstallFailed", ex.Message);
            ShowInstallError = true;
            InstallButtonText = _loc.GetString("Settings_Ollama_RetryCheck");
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void OpenOllamaWebsite()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://ollama.com",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open Ollama website");
        }
    }

    // ── Advanced Settings ────────────────────────────────────────────────

    partial void OnOllamaBaseUrlChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        var updated = _settings.Current with { OllamaBaseUrl = value };
        _ = _settings.UpdateAsync(updated);
    }

    partial void OnAutoWarmupChanged(bool value)
    {
        var updated = _settings.Current with { OllamaAutoWarmup = value };
        _ = _settings.UpdateAsync(updated);
    }

    partial void OnNumCtxIndexChanged(int value)
    {
        if (value >= 0 && value < NumCtxValues.Length)
        {
            var updated = _settings.Current with { OllamaNumCtx = NumCtxValues[value] };
            _ = _settings.UpdateAsync(updated);
        }
    }

    /// <summary>Returns the names of all installed Ollama models (for external consumers like Vision settings).</summary>
    public async Task<IReadOnlyList<string>> GetInstalledModelNamesAsync()
    {
        var models = await _ollamaManager.GetInstalledModelsAsync().ConfigureAwait(false);
        return models.Select(m => m.Name).ToList();
    }

    // ── Refresh Helpers ──────────────────────────────────────────────────

    private async Task RefreshInstalledModelsAsync()
    {
        try
        {
            var models = await _ollamaManager.GetInstalledModelsAsync();
            InstalledModels.Clear();
            long totalBytes = 0;
            foreach (var m in models)
            {
                InstalledModels.Add(m);
                totalBytes += m.Size;
            }
            TotalDiskUsage = FormatBytes(totalBytes);

            // Update model selector index — find match first to avoid transient -1
            int matchIndex = -1;
            for (int i = 0; i < InstalledModels.Count; i++)
            {
                if (string.Equals(NormaliseTag(InstalledModels[i].Name), NormaliseTag(SelectedModel),
                    StringComparison.OrdinalIgnoreCase))
                {
                    matchIndex = i;
                    break;
                }
            }
            SelectedModelIndex = matchIndex;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OllamaSettings: failed to refresh installed models");
        }
    }

    private async Task RefreshRunningModelsAsync()
    {
        try
        {
            var running = await _ollamaManager.GetRunningModelsAsync();
            RunningModels.Clear();
            long totalVram = 0;
            foreach (var m in running)
            {
                RunningModels.Add(m);
                totalVram += m.SizeVram;
            }
            RunningModelCount = running.Count;
            VramSummary = running.Count > 0
                ? $"{running.Count} model{(running.Count > 1 ? "s" : "")} loaded · {FormatBytes(totalVram)} VRAM"
                : "No models loaded";

            // Update inference speed
            LastInferenceSpeed = _ollamaProvider.LastTokensPerSec.HasValue
                ? $"{_ollamaProvider.LastTokensPerSec.Value:F1} tok/s"
                : "";
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "OllamaSettings: failed to refresh running models");
        }
    }

    private async Task RefreshHardwareInfoAsync()
    {
        try
        {
            var info = await _hardwareService.GetInfoAsync();
            GpuName = info.GpuName;
            VramTotalGB = info.VramTotalGB;
            VramUsedGB = info.VramUsedGB;
            VramPercent = info.VramTotalGB > 0 ? info.VramUsedGB / info.VramTotalGB * 100 : 0;
            RamSummary = $"{info.RamAvailableGB:F1} / {info.RamTotalGB:F1} GB";
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "OllamaSettings: failed to refresh hardware info");
        }
    }

    // ── Utilities ────────────────────────────────────────────────────────

    private static string NormaliseTag(string tag)
    {
        tag = tag.Trim();
        if (tag.EndsWith(":latest", StringComparison.OrdinalIgnoreCase))
        {
            tag = tag[..^7];
        }
        return tag;
    }

    internal static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824L => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576L => $"{bytes / 1_048_576.0:F0} MB",
            >= 1024L => $"{bytes / 1024.0:F0} KB",
            _ => $"{bytes} B",
        };
    }
}
