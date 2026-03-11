
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Config;
using DiktaMe.Core.SystemManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardLlmPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;
    private CancellationTokenSource? _pullCts;
    private readonly LocalizationService _loc;
    private readonly DispatcherQueue _dispatcher;

    public WizardLlmPage()
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
        if (string.Equals(viewModel.LlmChoice, "local", StringComparison.Ordinal))
        {
            LlmLocal.IsChecked = true;
            _ = ShowOllamaStatusAsync();
        }
        else
        {
            LlmCloud.IsChecked = true;
        }
    }

    private void LlmRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (LlmLocal.IsChecked == true)
        {
            _viewModel.LlmChoice = "local";
            _ = ShowOllamaStatusAsync();
        }
        else
        {
            _viewModel.LlmChoice = "cloud";
            CancelPull();
            OllamaPanel.Visibility = Visibility.Collapsed;
            InstallPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Shows the Ollama panel with current status (no pull yet — pull happens on Next).
    /// </summary>
    private async Task ShowOllamaStatusAsync()
    {
        OllamaPanel.Visibility = Visibility.Visible;
        PullError.Visibility = Visibility.Collapsed;
        InstallPanel.Visibility = Visibility.Collapsed;
        PullProgress.IsIndeterminate = true;
        PullProgress.Value = 0;
        PullStatus.Text = "";

        var ollama = App.Current.Services.GetRequiredService<OllamaManager>();
        var settings = App.Current.Services.GetRequiredService<SettingsManager>();
        string modelTag = settings.Current.OllamaModel;

        try
        {
            var result = await ollama.CheckAsync(modelTag);

            _dispatcher.TryEnqueue(() =>
            {
                PullProgress.IsIndeterminate = false;

                switch (result.Status)
                {
                    case OllamaStatus.Ready:
                        PullStatus.Text = _loc.GetString("Wizard_Llm_OllamaReady");
                        PullProgress.Value = 100;
                        break;

                    case OllamaStatus.Offline:
                        ShowOfflineWithInstallOption();
                        break;

                    case OllamaStatus.ModelNotPulled:
                        PullStatus.Text = _loc.GetString("Wizard_Llm_ModelPending");
                        PullProgress.Value = 0;
                        break;

                    case OllamaStatus.VersionTooOld:
                        PullStatus.Text = _loc.GetFormatted("Wizard_Llm_OllamaVersionOld",
                            result.OllamaVersion ?? "?", result.RequiredVersion ?? "?");
                        PullProgress.Value = 0;
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: Ollama status check failed");
            _dispatcher.TryEnqueue(() =>
            {
                PullProgress.IsIndeterminate = false;
                ShowOfflineWithInstallOption();
            });
        }
    }

    /// <summary>
    /// Shows the offline state with an Install button for uninstalled Ollama.
    /// </summary>
    private void ShowOfflineWithInstallOption()
    {
        PullStatus.Text = _loc.GetString("Wizard_Llm_OllamaNotDetected");
        PullProgress.Value = 0;
        InstallPanel.Visibility = Visibility.Visible;
        InstallButton.IsEnabled = true;
        InstallButtonText.Text = _loc.GetString("Wizard_Llm_InstallButton");
        ManualInstallLink.Visibility = Visibility.Collapsed;
        ManualInstallText.Text = _loc.GetString("Wizard_Llm_ManualInstall");
    }

    /// <summary>
    /// Handles the Install Ollama button click — tries winget, falls back to browser.
    /// </summary>
    private async void InstallOllama_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        PullError.Visibility = Visibility.Collapsed;
        PullProgress.IsIndeterminate = true;
        PullStatus.Text = _loc.GetString("Wizard_Llm_Installing");

        _pullCts = new CancellationTokenSource();
        var ct = _pullCts.Token;

        try
        {
            var progress = new Progress<string>(msg =>
            {
                _dispatcher.TryEnqueue(() => PullStatus.Text = msg);
            });

            var (result, error) = await OllamaManager.InstallViaWingetAsync(progress, ct);

            _dispatcher.TryEnqueue(() =>
            {
                PullProgress.IsIndeterminate = false;

                switch (result)
                {
                    case OllamaInstallResult.Success:
                        PullStatus.Text = _loc.GetString("Wizard_Llm_InstallComplete");
                        PullProgress.Value = 100;
                        InstallPanel.Visibility = Visibility.Collapsed;
                        // Re-check Ollama status — should now see Running + ModelNotPulled
                        _ = TryStartAndRecheckAsync();
                        break;

                    case OllamaInstallResult.WingetNotFound:
                        // Fall back to browser
                        Log.Information("Wizard: winget not available, opening browser for Ollama download");
                        OpenOllamaBrowser();
                        PullStatus.Text = _loc.GetString("Wizard_Llm_OllamaOffline");
                        InstallButton.IsEnabled = true;
                        InstallButtonText.Text = _loc.GetString("Wizard_Llm_RetryCheck");
                        ManualInstallLink.Visibility = Visibility.Visible;
                        break;

                    case OllamaInstallResult.Failed:
                        PullError.Text = _loc.GetFormatted("Wizard_Llm_InstallFailed", error ?? "Unknown error");
                        PullError.Visibility = Visibility.Visible;
                        PullStatus.Text = "";
                        InstallButton.IsEnabled = true;
                        InstallButtonText.Text = _loc.GetString("Wizard_Llm_RetryCheck");
                        ManualInstallLink.Visibility = Visibility.Visible;
                        break;
                }
            });
        }
        catch (OperationCanceledException)
        {
            Log.Information("Wizard: Ollama install cancelled");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: Ollama install failed");
            _dispatcher.TryEnqueue(() =>
            {
                PullProgress.IsIndeterminate = false;
                PullError.Text = _loc.GetFormatted("Wizard_Llm_InstallFailed", ex.Message);
                PullError.Visibility = Visibility.Visible;
                PullStatus.Text = "";
                InstallButton.IsEnabled = true;
            });
        }
    }

    /// <summary>
    /// After winget install, try to start Ollama and re-check status.
    /// </summary>
    private async Task TryStartAndRecheckAsync()
    {
        var ollama = App.Current.Services.GetRequiredService<OllamaManager>();

        PullProgress.IsIndeterminate = true;
        PullStatus.Text = _loc.GetString("Wizard_Llm_Installing"); // "Starting Ollama..."

        bool started = await ollama.StartOllamaAsync();
        if (started)
        {
            Log.Information("Wizard: Ollama started after install");
        }

        // Re-check full status (will show Ready or ModelNotPulled)
        await ShowOllamaStatusAsync();
    }

    private static void OpenOllamaBrowser()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://ollama.com/download",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: failed to open Ollama download page");
        }
    }

    /// <summary>
    /// Called by WizardViewModel when the user clicks Next on this step.
    /// If local LLM is selected, validates Ollama status and pulls model if needed.
    /// </summary>
    private async Task<bool> OnBeforeLeaveStepAsync()
    {
        // Cloud LLM — nothing to validate, proceed immediately
        if (!string.Equals(_viewModel?.LlmChoice, "local", StringComparison.Ordinal))
        {
            return true;
        }

        var ollama = App.Current.Services.GetRequiredService<OllamaManager>();
        var settings = App.Current.Services.GetRequiredService<SettingsManager>();
        string modelTag = settings.Current.OllamaModel;

        OllamaCheckResult checkResult;
        try
        {
            checkResult = await ollama.CheckAsync(modelTag);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: Ollama check failed on Next");
            ShowOfflineWithInstallOption();
            return false;
        }

        switch (checkResult.Status)
        {
            case OllamaStatus.Ready:
                return true; // All good, proceed

            case OllamaStatus.Offline:
                ShowOfflineWithInstallOption();
                return false;

            case OllamaStatus.VersionTooOld:
                ShowError(_loc.GetFormatted("Wizard_Llm_OllamaVersionOld",
                    checkResult.OllamaVersion ?? "?", checkResult.RequiredVersion ?? "?"));
                return false;

            case OllamaStatus.ModelNotPulled:
                // Start the pull
                return await PullModelAsync(ollama, modelTag);

            default:
                return false;
        }
    }

    private async Task<bool> PullModelAsync(OllamaManager ollama, string modelTag)
    {
        _viewModel!.CanGoNext = false;
        OllamaPanel.Visibility = Visibility.Visible;
        PullError.Visibility = Visibility.Collapsed;
        PullProgress.IsIndeterminate = false;
        PullProgress.Value = 0;
        PullStatus.Text = _loc.GetFormatted("Wizard_Llm_Pulling", 0);

        _pullCts = new CancellationTokenSource();
        var ct = _pullCts.Token;

        var progress = new Progress<OllamaPullProgress>(p =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                if (p.Total > 0)
                {
                    int percent = (int)(p.Completed * 100 / p.Total);
                    PullProgress.Value = percent;
                    PullStatus.Text = _loc.GetFormatted("Wizard_Llm_Pulling", percent);
                }
            });
        });

        try
        {
            await ollama.PullModelAsync(modelTag, progress, ct);

            _dispatcher.TryEnqueue(() =>
            {
                PullStatus.Text = _loc.GetString("Wizard_Llm_PullComplete");
                PullProgress.Value = 100;
                _viewModel.CanGoNext = true;
            });

            Log.Information("Wizard: Ollama model '{Model}' pulled successfully", modelTag);
            return false; // Stay on step so user sees completion — next click re-checks → Ready → proceeds
        }
        catch (OperationCanceledException)
        {
            Log.Information("Wizard: Ollama model pull cancelled");
            _viewModel.CanGoNext = true;
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: Ollama model pull failed");

            _dispatcher.TryEnqueue(() =>
            {
                PullError.Text = _loc.GetFormatted("Wizard_Llm_PullFailed", ex.Message);
                PullError.Visibility = Visibility.Visible;
                PullStatus.Text = "";
                _viewModel.CanGoNext = true;
            });

            return false; // Stay on this step so user can retry or switch to Cloud
        }
    }

    private void ShowError(string message)
    {
        OllamaPanel.Visibility = Visibility.Visible;
        PullProgress.IsIndeterminate = false;
        PullProgress.Value = 0;
        PullError.Text = message;
        PullError.Visibility = Visibility.Visible;
        PullStatus.Text = "";
    }

    private void CancelPull()
    {
        _pullCts?.Cancel();
        _pullCts?.Dispose();
        _pullCts = null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelPull();
    }
}
