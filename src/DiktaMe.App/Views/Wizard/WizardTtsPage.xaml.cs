
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Security;
using DiktaMe.Core.TTS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardTtsPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;
    private CancellationTokenSource? _downloadCts;
    private readonly LocalizationService _loc;
    private readonly DispatcherQueue _dispatcher;
    private readonly KokoroModelManager _kokoro = new();
    private readonly LicenseManager _licenseManager;

    public WizardTtsPage()
    {
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        _licenseManager = App.Current.Services.GetRequiredService<LicenseManager>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
        viewModel.BeforeLeaveStep = OnBeforeLeaveStepAsync;

        _licenseManager.LicenseStateChanged += OnLicenseStateChanged;

        // Remove irrelevant options from RadioButtons (Collapsed still leaves gap)
        bool isLocal = string.Equals(viewModel.OnboardingChoice, "local", StringComparison.Ordinal);
        bool isByok = string.Equals(viewModel.OnboardingChoice, "apikeys", StringComparison.Ordinal);
        if (isLocal)
        {
            TtsRadio.Items.Remove(TtsCloud);
        }

        if (isByok)
        {
            TtsRadio.Items.Remove(TtsLocal);
        }

        TtsCloudPanel.Visibility = Visibility.Collapsed;

        if (isByok)
        {
            SelectComboByTag(TtsProviderCombo, viewModel.CloudTtsProvider);
        }

        // Select the appropriate option
        if (isLocal || (string.Equals(viewModel.TtsChoice, "local", StringComparison.Ordinal) && _licenseManager.IsLicensed))
        {
            TtsLocal.IsChecked = true;
            ShowModelStatus();
        }
        else if (string.Equals(viewModel.TtsChoice, "cloud", StringComparison.Ordinal))
        {
            TtsCloud.IsChecked = true;
        }
        else
        {
            TtsOff.IsChecked = true;
        }

    }

    private void TtsRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (TtsLocal.IsChecked == true)
        {
            if (!_licenseManager.IsLicensed)
            {
                // Bounce back to previous selection
                TtsOff.IsChecked = true;
                _viewModel.TtsChoice = "off";
                return;
            }

            _viewModel.TtsChoice = "local";
            ShowModelStatus();
        }
        else if (TtsCloud.IsChecked == true)
        {
            _viewModel.TtsChoice = "cloud";
            bool isByok = string.Equals(_viewModel.OnboardingChoice, "apikeys", StringComparison.Ordinal);
            TtsCloudPanel.Visibility = isByok ? Visibility.Visible : Visibility.Collapsed;
            if (isByok)
            {
                UpdateTtsKeyInfo(_viewModel.CloudTtsProvider);
            }

            CancelDownload();
            DownloadPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            _viewModel.TtsChoice = "off";
            TtsCloudPanel.Visibility = Visibility.Collapsed;
            CancelDownload();
            DownloadPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowModelStatus()
    {
        DownloadPanel.Visibility = Visibility.Visible;
        DownloadError.Visibility = Visibility.Collapsed;
        DownloadProgress.Value = 0;

        var kokoro = _kokoro;
        if (kokoro.IsModelDownloaded)
        {
            DownloadStatus.Text = _loc.GetString("Wizard_Tts_ModelReady");
            DownloadProgress.Value = 100;
        }
        else
        {
            DownloadStatus.Text = _loc.GetString("Wizard_Tts_DownloadPending");
        }
    }

    private async Task<bool> OnBeforeLeaveStepAsync()
    {
        // Off or Cloud — nothing to download, proceed immediately
        if (!string.Equals(_viewModel?.TtsChoice, "local", StringComparison.Ordinal))
        {
            return true;
        }

        var kokoro = _kokoro;

        // Model already present — proceed
        if (kokoro.IsModelDownloaded)
        {
            return true;
        }

        // Start download, block navigation
        _viewModel!.CanGoNext = false;
        DownloadPanel.Visibility = Visibility.Visible;
        DownloadError.Visibility = Visibility.Collapsed;
        DownloadProgress.Value = 0;
        DownloadStatus.Text = _loc.GetFormatted("Wizard_Tts_Downloading", 0);

        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        kokoro.DownloadProgress += OnDownloadProgress;

        try
        {
            await kokoro.DownloadModelAsync(ct);

            _dispatcher.TryEnqueue(() =>
            {
                DownloadStatus.Text = _loc.GetString("Wizard_Tts_DownloadComplete");
                DownloadProgress.Value = 100;
                _viewModel.CanGoNext = true;
            });

            Log.Information("Wizard: Kokoro TTS model downloaded successfully");
            return false; // Stay on step so user sees completion — next click advances via IsModelDownloaded check
        }
        catch (OperationCanceledException)
        {
            Log.Information("Wizard: Kokoro TTS model download cancelled");
            _viewModel.CanGoNext = true;
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: Kokoro TTS model download failed");

            _dispatcher.TryEnqueue(() =>
            {
                DownloadError.Text = _loc.GetFormatted("Wizard_Tts_DownloadFailed", ex.Message);
                DownloadError.Visibility = Visibility.Visible;
                DownloadStatus.Text = "";
                _viewModel.CanGoNext = true;
            });

            return false; // Stay on this step so user can retry or switch
        }
        finally
        {
            kokoro.DownloadProgress -= OnDownloadProgress;
        }
    }

    private void OnDownloadProgress(object? sender, DownloadProgressEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            DownloadProgress.Value = e.Percent;
            DownloadStatus.Text = _loc.GetFormatted("Wizard_Tts_Downloading", e.Percent);
        });
    }

    private void CancelDownload()
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = null;
    }

    private void TtsProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || TtsProviderCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        string provider = item.Tag as string ?? "deepgram";
        _viewModel.CloudTtsProvider = provider;
        UpdateTtsKeyInfo(provider);
    }

    private void TtsKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        string provider = _viewModel.CloudTtsProvider;
        string key = TtsKeyBox.Password;

        // Store on the correct ViewModel property
        switch (provider)
        {
            case "deepgram": _viewModel.DeepgramApiKey = key; break;
            case "openai": _viewModel.OpenAiApiKey = key; break;
            case "gemini": _viewModel.GeminiApiKey = key; break;
            case "inworld": _viewModel.InworldApiKey = key; break;
        }
    }

    /// <summary>
    /// Shows key info — if the key was already entered on STT/LLM page, tell the user.
    /// If it's a new provider (Inworld), show the key input field.
    /// </summary>
    private void UpdateTtsKeyInfo(string provider)
    {
        if (_viewModel is null)
        {
            return;
        }

        // Check if this provider's key was already entered on a previous wizard step
        string? existingKey = provider switch
        {
            "deepgram" => _viewModel.DeepgramApiKey,
            "openai" => _viewModel.OpenAiApiKey,
            "gemini" => _viewModel.GeminiApiKey,
            _ => null,
        };

        bool hasKey = !string.IsNullOrWhiteSpace(existingKey);

        if (hasKey)
        {
            TtsKeyInfo.Text = $"Uses the {provider} API key you already entered.";
            TtsKeyInfo.Visibility = Visibility.Visible;
            TtsKeyInputPanel.Visibility = Visibility.Collapsed;
        }
        else if (string.Equals(provider, "inworld", StringComparison.Ordinal))
        {
            TtsKeyInfo.Text = "Get your key from: docs.inworld.ai";
            TtsKeyInfo.Visibility = Visibility.Visible;
            TtsKeyInputPanel.Visibility = Visibility.Visible;
            TtsKeyBox.Password = _viewModel.InworldApiKey;
        }
        else
        {
            // Provider key not entered yet (e.g. picked Deepgram TTS but only entered Gemini for LLM)
            TtsKeyInfo.Text = $"Requires a {provider} API key (not entered on previous steps).";
            TtsKeyInfo.Visibility = Visibility.Visible;
            TtsKeyInputPanel.Visibility = Visibility.Visible;
            TtsKeyBox.Password = "";
        }
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem ci && string.Equals(ci.Tag as string, tag, StringComparison.Ordinal))
            {
                combo.SelectedItem = ci;
                return;
            }
        }
    }

    private void OnLicenseStateChanged(bool licensed)
    {
        // Reserved for future use — lane filtering handles visibility now
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelDownload();
        _licenseManager.LicenseStateChanged -= OnLicenseStateChanged;
    }
}
