
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using DiktaMe.Core.SystemManagement;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class OllamaSettingsViewModel : ObservableObject
{
    private readonly OllamaManager _ollamaManager;
    private readonly SettingsManager _settings;

    [ObservableProperty] private string _ollamaVersion = "Unknown";
    [ObservableProperty] private string _statusText = "Not checked";
    [ObservableProperty] private OllamaStatus _status = OllamaStatus.Offline;
    [ObservableProperty] private string _selectedModel = "llama3.2";
    [ObservableProperty] private ObservableCollection<string> _installedModels = new();
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _showRescue;
    [ObservableProperty] private string _rescueMessage = "";
    [ObservableProperty] private string _fallbackModel = "";

    public OllamaSettingsViewModel(OllamaManager ollamaManager, SettingsManager settings)
    {
        _ollamaManager = ollamaManager;
        _settings = settings;
        SelectedModel = settings.Current.OllamaModel;
    }

    [RelayCommand]
    private async Task CheckHealthAsync()
    {
        IsChecking = true;
        ShowRescue = false;
        try
        {
            var result = await _ollamaManager.CheckAsync(SelectedModel);
            Status = result.Status;
            OllamaVersion = result.OllamaVersion ?? "Not detected";
            StatusText = result.Status switch
            {
                OllamaStatus.Ready => "Ready",
                OllamaStatus.Offline => "Offline — is Ollama running?",
                OllamaStatus.VersionTooOld => $"Ollama {result.OllamaVersion} is too old (needs {result.RequiredVersion})",
                OllamaStatus.ModelNotPulled => $"Model '{SelectedModel}' not installed",
                _ => "Unknown",
            };

            if (result.Status == OllamaStatus.VersionTooOld)
            {
                ShowRescue = true;
                RescueMessage = $"Model '{SelectedModel}' requires Ollama {result.RequiredVersion}+, you have {result.OllamaVersion}.";
                FallbackModel = result.FallbackModel ?? "llama3.2";
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
            StatusText = "Check failed";
            Log.Error(ex, "Ollama health check failed");
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
        StatusText = $"Switched to fallback model: {FallbackModel}";
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
}
