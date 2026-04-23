
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Security;
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

    private bool _isByok;
    private bool _skipWarningShown;

    // Parallel list of real model IDs for ModelCombo's displayed names.
    // Index-matched: ModelCombo.SelectedIndex → _modelIds[i].
    private readonly List<string> _modelIds = [];

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;

        // Set the callback so the ViewModel calls us before leaving this step
        viewModel.BeforeLeaveStep = OnBeforeLeaveStepAsync;

        // Remove irrelevant options from RadioButtons (Collapsed still leaves gap)
        bool isLocal = string.Equals(viewModel.OnboardingChoice, "local", StringComparison.Ordinal);
        _isByok = string.Equals(viewModel.OnboardingChoice, "apikeys", StringComparison.Ordinal);
        if (isLocal)
        {
            LlmRadio.Items.Remove(LlmCloud);
        }
        else
        {
            LlmRadio.Items.Remove(LlmLocal);
        }

        // Show API key panel for BYOK path
        ApiKeyPanel.Visibility = _isByok ? Visibility.Visible : Visibility.Collapsed;

        if (_isByok)
        {
            // Restore provider selection
            SelectComboByTag(LlmProviderCombo, viewModel.CloudLlmProvider);
            UpdateKeySourceLink(viewModel.CloudLlmProvider);
            LoadKeyForProvider(viewModel.CloudLlmProvider);
        }

        // Select the appropriate option
        if (isLocal || string.Equals(viewModel.LlmChoice, "local", StringComparison.Ordinal))
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
            ApiKeyPanel.Visibility = Visibility.Collapsed;
            HideModelPicker();
            _ = ShowOllamaStatusAsync();
        }
        else
        {
            _viewModel.LlmChoice = "cloud";
            ApiKeyPanel.Visibility = _isByok ? Visibility.Visible : Visibility.Collapsed;
            CancelPull();
            OllamaPanel.Visibility = Visibility.Collapsed;
            InstallPanel.Visibility = Visibility.Collapsed;
        }

        SkipWarning.IsOpen = false;
        _skipWarningShown = false;
    }

    private void LlmProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || LlmProviderCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        string provider = item.Tag as string ?? "gemini";
        _viewModel.CloudLlmProvider = provider;
        UpdateKeySourceLink(provider);
        LoadKeyForProvider(provider);
        KeyStatus.Visibility = Visibility.Collapsed;
        SkipWarning.IsOpen = false;
        _skipWarningShown = false;

        // Provider changed — previous model list + picked model no longer
        // applies. User must Test again to repopulate.
        HideModelPicker();
        _viewModel.CloudLlmModelId = "";
        _viewModel.CloudLlmModelDisplayName = "";
    }

    private void LlmKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        string provider = _viewModel.CloudLlmProvider;
        string key = LlmKeyBox.Password;

        // Store on the correct ViewModel property
        switch (provider)
        {
            case "gemini": _viewModel.GeminiApiKey = key; break;
            case "openai": _viewModel.OpenAiApiKey = key; break;
            case "anthropic": _viewModel.AnthropicApiKey = key; break;
            case "openrouter": _viewModel.OpenRouterApiKey = key; break;
            case "requesty": _viewModel.RequestyApiKey = key; break;
        }

        TestKeyButton.IsEnabled = !string.IsNullOrWhiteSpace(key);
        KeyStatus.Visibility = Visibility.Collapsed;
        SkipWarning.IsOpen = false;
        _skipWarningShown = false;

        // Typed key changed — any previously-loaded model list is stale
        // (was for a different key). Hide until the user re-tests.
        HideModelPicker();
        _viewModel.CloudLlmModelId = "";
        _viewModel.CloudLlmModelDisplayName = "";
    }

    private async void TestKeyButton_Click(object sender, RoutedEventArgs e)
    {
        string apiKey = LlmKeyBox.Password;
        string provider = _viewModel?.CloudLlmProvider ?? "gemini";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        TestKeyButton.IsEnabled = false;
        KeyStatus.Visibility = Visibility.Visible;
        KeyProgress.IsActive = true;
        KeyStatusText.Text = _loc.GetString("Wizard_ApiKeys_Testing");
        HideModelPicker();

        try
        {
            // Reuse the shared ApiKeyTester (same whoami probes as Settings >
            // API Keys Test Connection, BUG-023). No persistence — the key
            // stays in memory via the overrideKey path.
            var tester = App.Current.Services.GetRequiredService<ApiKeyTester>();
            var result = await tester.TestAsync(provider, apiKey);

            KeyStatusText.Text = _loc.GetString(result.Ok ? "Wizard_ApiKeys_Valid" : "Wizard_ApiKeys_Invalid");
            KeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                result.Ok ? Microsoft.UI.Colors.LimeGreen : Microsoft.UI.Colors.Tomato);

            if (result.Ok)
            {
                // Fetch the live model list with the in-memory key (never
                // persisted until the wizard's Finish step).
                await LoadModelsForProviderAsync(provider, apiKey);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: LLM API key test failed for {Provider}", provider);
            KeyStatusText.Text = "Connection error";
            KeyStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Tomato);
        }
        finally
        {
            KeyProgress.IsActive = false;
            TestKeyButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Fetches the live model list for the given provider using an
    /// in-memory <paramref name="overrideKey"/> (typed but not yet persisted).
    /// Populates <see cref="ModelCombo"/> and selects the model previously
    /// chosen on the wizard VM, if it still appears in the list.
    /// </summary>
    private async Task LoadModelsForProviderAsync(string providerType, string overrideKey)
    {
        if (_viewModel is null)
        {
            return;
        }

        var modelListService = App.Current.Services.GetRequiredService<ModelListService>();
        List<ModelInfo> models;
        try
        {
            models = await modelListService.GetModelsForProviderAsync(providerType, overrideKey);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: failed to load models for {Provider}", providerType);
            models = [];
        }

        _dispatcher.TryEnqueue(() =>
        {
            ModelCombo.ItemsSource = null;
            _modelIds.Clear();

            if (models.Count == 0)
            {
                HideModelPicker();
                return;
            }

            var displayNames = new List<string>(models.Count);
            foreach (var m in models)
            {
                displayNames.Add(m.DisplayName);
                _modelIds.Add(m.ModelId);
            }

            ModelCombo.ItemsSource = displayNames;

            // Pre-select the previously-picked model if it's still in the list
            // — but only the PREVIOUS pick from the wizard VM; never auto-pick
            // a default on first-time load (user wants explicit choice).
            int preIdx = !string.IsNullOrWhiteSpace(_viewModel.CloudLlmModelId)
                ? _modelIds.FindIndex(id =>
                    string.Equals(id, _viewModel.CloudLlmModelId, StringComparison.Ordinal))
                : -1;
            ModelCombo.SelectedIndex = preIdx;

            ModelPickerPanel.Visibility = Visibility.Visible;
        });
    }

    private void HideModelPicker()
    {
        ModelPickerPanel.Visibility = Visibility.Collapsed;
        ModelCombo.ItemsSource = null;
        _modelIds.Clear();
    }

    private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        int idx = ModelCombo.SelectedIndex;
        if (idx < 0 || idx >= _modelIds.Count)
        {
            _viewModel.CloudLlmModelId = "";
            _viewModel.CloudLlmModelDisplayName = "";
            return;
        }

        _viewModel.CloudLlmModelId = _modelIds[idx];
        _viewModel.CloudLlmModelDisplayName = ModelCombo.Items[idx]?.ToString() ?? "";
    }

    private void UpdateKeySourceLink(string provider)
    {
        KeySourceLink.Text = provider switch
        {
            "gemini" => "Get your key from: aistudio.google.com/apikey",
            "openai" => "Get your key from: platform.openai.com/api-keys",
            "anthropic" => "Get your key from: console.anthropic.com/settings/keys",
            "openrouter" => "Get your key from: openrouter.ai/keys",
            "requesty" => "Get your key from: app.requesty.ai",
            _ => "",
        };
    }

    private void LoadKeyForProvider(string provider)
    {
        if (_viewModel is null)
        {
            return;
        }

        LlmKeyBox.Password = provider switch
        {
            "gemini" => _viewModel.GeminiApiKey,
            "openai" => _viewModel.OpenAiApiKey,
            "anthropic" => _viewModel.AnthropicApiKey,
            "openrouter" => _viewModel.OpenRouterApiKey,
            "requesty" => _viewModel.RequestyApiKey,
            _ => "",
        };
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
        // BYOK cloud — warn if no key entered (allow skip on second click)
        if (_isByok && string.Equals(_viewModel?.LlmChoice, "cloud", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(LlmKeyBox.Password))
        {
            if (!_skipWarningShown)
            {
                SkipWarning.IsOpen = true;
                _skipWarningShown = true;
                return false;
            }

            return true;
        }

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
