
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using DiktaMe.Core.SystemManagement;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class OllamaSettingsViewModel : ObservableObject
{
    private readonly OllamaManager _ollamaManager;
    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;

    private static readonly string[] KeepAliveValues = ["5m", "10m", "30m", "1h", "2h"];

    [ObservableProperty] private string _ollamaVersion = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private OllamaStatus _status = OllamaStatus.Offline;
    [ObservableProperty] private string _selectedModel = "llama3.2";
    [ObservableProperty] private ObservableCollection<string> _installedModels = new();
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _showRescue;
    [ObservableProperty] private string _rescueMessage = "";
    [ObservableProperty] private string _fallbackModel = "";
    [ObservableProperty] private int _keepAliveIndex = 1;

    // Install flow
    [ObservableProperty] private bool _showInstall;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _installButtonText = "";
    [ObservableProperty] private bool _showManualInstallLink;
    [ObservableProperty] private string _installError = "";
    [ObservableProperty] private bool _showInstallError;

    public OllamaSettingsViewModel(OllamaManager ollamaManager, SettingsManager settings, LocalizationService loc)
    {
        _ollamaManager = ollamaManager;
        _settings = settings;
        _loc = loc;
        OllamaVersion = _loc.GetString("Settings_Ollama_Status_Unknown");
        StatusText = _loc.GetString("Settings_Ollama_Status_NotChecked");
        SelectedModel = settings.Current.OllamaModel;

        int idx = Array.IndexOf(KeepAliveValues, settings.Current.OllamaKeepAlive);
        _keepAliveIndex = idx >= 0 ? idx : 1; // default 10m
    }

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
                OllamaStatus.VersionTooOld => _loc.GetFormatted("Settings_Ollama_Status_TooOld", result.OllamaVersion ?? "", result.RequiredVersion ?? ""),
                OllamaStatus.ModelNotPulled => _loc.GetFormatted("Settings_Ollama_Status_ModelNotInstalled", SelectedModel),
                _ => _loc.GetString("Settings_Ollama_Status_Unknown"),
            };

            if (result.Status == OllamaStatus.VersionTooOld)
            {
                ShowRescue = true;
                RescueMessage = _loc.GetFormatted("Settings_Ollama_Rescue_Message", SelectedModel, result.RequiredVersion ?? "", result.OllamaVersion ?? "");
                FallbackModel = result.FallbackModel ?? "llama3.2";
            }

            if (result.Status == OllamaStatus.Offline)
            {
                ShowInstall = true;
                InstallButtonText = _loc.GetString("Settings_Ollama_InstallButton");
            }

            // Refresh installed models
            var installed = await _ollamaManager.GetInstalledModelTagsAsync();
            InstalledModels.Clear();
            foreach (var tag in installed)
            {
                InstalledModels.Add(tag);
            }
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
    private async Task UseFallbackAsync()
    {
        SelectedModel = FallbackModel;
        var updated = _settings.Current with { OllamaModel = FallbackModel };
        await _settings.UpdateAsync(updated);
        ShowRescue = false;
        StatusText = _loc.GetFormatted("Settings_Ollama_Rescue_Switched", FallbackModel);
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

    partial void OnSelectedModelChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
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

    [RelayCommand]
    private async Task InstallOllamaAsync()
    {
        IsInstalling = true;
        ShowInstallError = false;

        var progress = new Progress<string>(msg =>
        {
            InstallButtonText = msg;
        });

        try
        {
            var (result, error) = await OllamaManager.InstallViaWingetAsync(progress);

            switch (result)
            {
                case OllamaInstallResult.Success:
                    InstallButtonText = _loc.GetString("Settings_Ollama_Status_Ready");
                    ShowInstall = false;
                    // Start Ollama and re-check status
                    await _ollamaManager.StartOllamaAsync();
                    await CheckHealthAsync();
                    break;

                case OllamaInstallResult.WingetNotFound:
                    Log.Information("Settings: winget not available, opening browser for Ollama download");
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
}
