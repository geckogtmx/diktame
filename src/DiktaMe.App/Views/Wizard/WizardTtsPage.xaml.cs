
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
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

    public WizardTtsPage()
    {
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
        viewModel.BeforeLeaveStep = OnBeforeLeaveStepAsync;

        // Restore selection from VM
        if (string.Equals(viewModel.TtsChoice, "local", StringComparison.Ordinal))
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
            _viewModel.TtsChoice = "local";
            ShowModelStatus();
        }
        else if (TtsCloud.IsChecked == true)
        {
            _viewModel.TtsChoice = "cloud";
            CancelDownload();
            DownloadPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            _viewModel.TtsChoice = "off";
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelDownload();
    }
}
