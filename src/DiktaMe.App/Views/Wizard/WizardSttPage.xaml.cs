
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using DiktaMe.Core.STT;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardSttPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;
    private CancellationTokenSource? _downloadCts;
    private readonly LocalizationService _loc;
    private readonly DispatcherQueue _dispatcher;

    public WizardSttPage()
    {
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;

        // Set the callback so the ViewModel calls us before leaving this step
        viewModel.BeforeLeaveStep = OnBeforeLeaveStepAsync;

        // Restore selection from VM
        if (string.Equals(viewModel.SttChoice, "local", StringComparison.Ordinal))
        {
            SttLocal.IsChecked = true;
            ShowModelStatus();
        }
        else
        {
            SttCloud.IsChecked = true;
        }
    }

    private void SttRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (SttLocal.IsChecked == true)
        {
            _viewModel.SttChoice = "local";
            ShowModelStatus();
        }
        else
        {
            _viewModel.SttChoice = "cloud";
            CancelDownload();
            DownloadPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Shows the download panel with current model status (no download yet).
    /// </summary>
    private void ShowModelStatus()
    {
        DownloadPanel.Visibility = Visibility.Visible;
        DownloadError.Visibility = Visibility.Collapsed;
        DownloadProgress.Value = 0;

        var whisper = App.Current.Services.GetRequiredService<WhisperProvider>();
        if (whisper.IsModelDownloaded)
        {
            DownloadStatus.Text = _loc.GetString("Wizard_Stt_ModelReady");
            DownloadProgress.Value = 100;
        }
        else
        {
            DownloadStatus.Text = _loc.GetString("Wizard_Stt_DownloadPending");
        }
    }

    /// <summary>
    /// Called by WizardViewModel when the user clicks Next on this step.
    /// If local STT is selected and model is not downloaded, starts the download
    /// and returns false to block navigation until complete.
    /// </summary>
    private async Task<bool> OnBeforeLeaveStepAsync()
    {
        // Cloud STT — nothing to download, proceed immediately
        if (!string.Equals(_viewModel?.SttChoice, "local", StringComparison.Ordinal))
        {
            return true;
        }

        var whisper = App.Current.Services.GetRequiredService<WhisperProvider>();

        // Model already present — proceed
        if (whisper.IsModelDownloaded)
        {
            return true;
        }

        // Start download, block navigation
        _viewModel!.CanGoNext = false;
        DownloadPanel.Visibility = Visibility.Visible;
        DownloadError.Visibility = Visibility.Collapsed;
        DownloadProgress.Value = 0;
        DownloadStatus.Text = _loc.GetFormatted("Wizard_Stt_Downloading", 0);

        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        whisper.DownloadProgress += OnDownloadProgress;

        try
        {
            await whisper.DownloadModelAsync(ct);

            _dispatcher.TryEnqueue(() =>
            {
                DownloadStatus.Text = _loc.GetString("Wizard_Stt_DownloadComplete");
                DownloadProgress.Value = 100;
                _viewModel.CanGoNext = true;
            });

            Log.Information("Wizard: Whisper model downloaded successfully");
            return false; // Stay on step so user sees completion — next click advances via IsModelDownloaded check
        }
        catch (OperationCanceledException)
        {
            Log.Information("Wizard: Whisper model download cancelled");
            _viewModel.CanGoNext = true;
            return false; // Stay on this step
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: Whisper model download failed");

            _dispatcher.TryEnqueue(() =>
            {
                DownloadError.Text = _loc.GetFormatted("Wizard_Stt_DownloadFailed", ex.Message);
                DownloadError.Visibility = Visibility.Visible;
                DownloadStatus.Text = "";
                _viewModel.CanGoNext = true;
            });

            return false; // Stay on this step so user can retry or switch to Cloud
        }
        finally
        {
            whisper.DownloadProgress -= OnDownloadProgress;
        }
    }

    private void OnDownloadProgress(object? sender, DownloadProgressEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            DownloadProgress.Value = e.Percent;
            DownloadStatus.Text = _loc.GetFormatted("Wizard_Stt_Downloading", e.Percent);
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
