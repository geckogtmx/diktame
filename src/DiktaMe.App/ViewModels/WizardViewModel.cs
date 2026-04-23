
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Serilog;
using WinUI3Localizer;

namespace DiktaMe.App.ViewModels;
public sealed partial class WizardViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly SecureStorage _secureStorage;
    private readonly IAccountService _accountService;
    private readonly LocalizationService _loc;
    [ObservableProperty] private int _currentStep;
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoNext = true;
    [ObservableProperty] private string _nextButtonText = "";

    // Step 0: Language choice
    [ObservableProperty] private string _languageChoice = "en";

    // Step 1: Onboarding choice ("wallet", "apikeys", or "local")
    [ObservableProperty] private string _onboardingChoice = "wallet";

    // Step 2: STT choice
    [ObservableProperty] private string _sttChoice = "cloud";

    // Step 3: LLM choice
    [ObservableProperty] private string _llmChoice = "cloud";

    // Step 4: TTS choice ("off", "cloud", or "local")
    [ObservableProperty] private string _ttsChoice = "off";

    // Cloud provider selections (BYOK path)
    [ObservableProperty] private string _cloudLlmProvider = "gemini";     // gemini, openai, anthropic, openrouter, requesty
    [ObservableProperty] private string _cloudTtsProvider = "deepgram";   // deepgram, openai, gemini, inworld

    // Specific cloud LLM model chosen by the user after testing their API key
    // in the wizard — populated via ModelListService.GetModelsForProviderAsync.
    // Empty string means the user skipped the model pick (e.g. no API key
    // provided, or Test failed). On Finish, an empty value means we do NOT
    // rewrite DictationModes/UtilityPipelines CloudProfile.ModelName — they
    // keep whatever DictationModeDefaults seeded. Populated → we persist it
    // as the new default AND propagate to every pipeline.
    [ObservableProperty] private string _cloudLlmModelId = "";
    [ObservableProperty] private string _cloudLlmModelDisplayName = "";

    // API Keys (keyed by provider name)
    [ObservableProperty] private string _deepgramApiKey = "";
    [ObservableProperty] private string _geminiApiKey = "";
    [ObservableProperty] private string _openAiApiKey = "";
    [ObservableProperty] private string _anthropicApiKey = "";
    [ObservableProperty] private string _openRouterApiKey = "";
    [ObservableProperty] private string _requestyApiKey = "";
    [ObservableProperty] private string _inworldApiKey = "";

    public const int TotalSteps = 9; // Steps 0-8 are sequential; step 9 (Activate) is a detour

    /// <summary>Step index of the activation detour page (not part of the sequential flow).</summary>
    public const int ActivateStep = 9;

    /// <summary>
    /// Optional async callback set by the current page. Called before leaving the step.
    /// Return <c>true</c> to allow navigation, <c>false</c> to block it.
    /// Reset to <c>null</c> when navigating to a new step.
    /// </summary>
    public Func<Task<bool>>? BeforeLeaveStep { get; set; }

    public event Action? StepChanged;
    public event Action? WizardCompleted;

    public WizardViewModel(SettingsManager settings, SecureStorage secureStorage, IAccountService accountService, LocalizationService loc)
    {
        _settings = settings;
        _secureStorage = secureStorage;
        _accountService = accountService;
        _loc = loc;
        _nextButtonText = _loc.GetString("Wizard_Next");

        // BUG-027: respect the user's saved default cloud LLM provider.
        // Falls back to "gemini" (the field's default) when unset or invalid.
        string savedDefault = _settings.Current.CloudLlm.DefaultCloudLlmProvider;
        if (!string.IsNullOrWhiteSpace(savedDefault))
        {
            _cloudLlmProvider = savedDefault;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        // Activate detour → return to Get Started
        if (CurrentStep == ActivateStep)
        {
            ReturnToGetStarted();
            return;
        }

        if (CurrentStep > 0)
        {
            CurrentStep--;
            BeforeLeaveStep = null;

            // Skip API Keys step (6) — keys entered inline now
            if (CurrentStep == 6)
            {
                CurrentStep--;
            }

            // Skip features page (2) when going back for non-wallet paths
            if (CurrentStep == 2 && OnboardingChoice is not "wallet")
            {
                CurrentStep--;
            }

            UpdateNavState();
            StepChanged?.Invoke();
        }
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        // Apply language when leaving the language step (step 0)
        if (CurrentStep == 0)
        {
            await ApplyLanguageAsync();
        }

        // Pre-select defaults when leaving Get Started (step 1)
        if (CurrentStep == 1 && string.Equals(OnboardingChoice, "local", StringComparison.Ordinal))
        {
            SttChoice = "local";
            LlmChoice = "local";
            TtsChoice = "local";
        }
        else if (CurrentStep == 1 && string.Equals(OnboardingChoice, "apikeys", StringComparison.Ordinal))
        {
            SttChoice = "cloud";
            LlmChoice = "cloud";
            TtsChoice = "off";
        }

        // Wallet fork — features page shown at step 2, then sign-in on Next
        if (CurrentStep == 2 && string.Equals(OnboardingChoice, "wallet", StringComparison.Ordinal))
        {
            await StartWalletAsync();
            return;
        }

        // Let the current page run pre-navigation logic (e.g. Whisper download)
        if (BeforeLeaveStep is not null)
        {
            bool canLeave = await BeforeLeaveStep();
            if (!canLeave)
            {
                return;
            }
        }

        if (CurrentStep < TotalSteps - 1)
        {
            CurrentStep++;
            BeforeLeaveStep = null; // Reset for next page

            // Skip features page (2) for BYOK/Local — they go straight to STT
            if (CurrentStep == 2 && OnboardingChoice is not "wallet")
            {
                CurrentStep++;
                Log.Information("Wizard: skipped features page (non-wallet path)");
            }

            // Skip API Keys step (6) — keys are now entered inline on each provider page
            if (CurrentStep == 6)
            {
                CurrentStep++;
                Log.Information("Wizard: skipped legacy API Keys step (keys entered inline)");
            }

            UpdateNavState();
            StepChanged?.Invoke();
        }
        else
        {
            // Final step — save and complete
            await CompleteWizardAsync();
        }
    }

    private async Task ApplyLanguageAsync()
    {
        try
        {
            // Switch UI language for remaining wizard steps (always apply — handles Back→re-select)
            await Localizer.Get().SetLanguage(LanguageChoice);
            Log.Information("Wizard: switched UI language to {Lang}", LanguageChoice);

            // Persist language to settings
            var general = _settings.Current.General with
            {
                Language = LanguageChoice,
                UiLanguage = LanguageChoice,
            };
            var updated = _settings.Current with { General = general };
            await _settings.UpdateAsync(updated);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wizard: failed to apply language {Lang}", LanguageChoice);
        }
    }

    private async Task StartWalletAsync()
    {
        try
        {
            // Set Wallet mode but do NOT mark wizard completed yet.
            // WizardCompleted will be set by the deeplink callback after successful sign-in.
            // This ensures that if the user closes the app before signing in, the wizard re-appears.
            var updated = _settings.Current with
            {
                AuthMode = AuthMode.Wallet,
            };
            await _settings.UpdateAsync(updated);

            // Open browser for login — token will arrive via deeplink.
            _accountService.Login();

            Log.Information("Wizard: wallet path — wizard completed, browser opened for login");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Wizard: failed to start wallet path");
        }

        WizardCompleted?.Invoke();
    }

    private async Task CompleteWizardAsync()
    {
        try
        {
            // Apply STT/LLM choices to default mode settings
            string defaultStt = string.Equals(SttChoice, "local", StringComparison.Ordinal) ? "whisper" : "deepgram";
            string defaultLlm = LlmChoice switch
            {
                "local" => "ollama",
                _ => CloudLlmProvider,
            };

            // LLM choice determines ActiveProfileName (controls model name resolution).
            // STT choice is independent — handled by ModeProfiles provider field.
            string profileName = string.Equals(LlmChoice, "local", StringComparison.Ordinal)
                ? "Local" : "Cloud";

            // Apply TTS choice
            bool ttsEnabled = !string.Equals(TtsChoice, "off", StringComparison.Ordinal);
            string ttsProvider = TtsChoice switch
            {
                "local" => "kokoro",
                "cloud" => CloudTtsProvider,
                _ => "kokoro", // default provider even if disabled
            };

            var updated = _settings.Current with
            {
                WizardCompleted = true,
                ActiveProfileName = profileName,
                Tts = _settings.Current.Tts with
                {
                    Enabled = ttsEnabled,
                    Provider = ttsProvider,
                    KokoroModelVariant = string.Equals(ttsProvider, "kokoro", StringComparison.Ordinal)
                        ? "int8"
                        : _settings.Current.Tts.KokoroModelVariant,
                },
            };

            // Update default mode profiles to use chosen providers
            var profiles = new Dictionary<string, ModeSettings>(updated.ModeProfiles);
            string[] modes = { "dictate", "refine", "ask", "translate", "note", "chat" };
            foreach (var mode in modes)
            {
                for (int p = 0; p < 2; p++)
                {
                    string key = $"{mode}_{p}";
                    var existing = profiles.TryGetValue(key, out var ms) ? ms : new ModeSettings();
                    profiles[key] = existing with
                    {
                        SttProvider = defaultStt,
                        LlmProvider = defaultLlm,
                        UseLlm = !string.Equals(defaultLlm, "none", StringComparison.Ordinal),
                    };
                }
            }

            updated = updated with { ModeProfiles = profiles };

            // Propagate the user's picked provider + specific model to every
            // DictationModes[].CloudProfile.ModelName and UtilityPipelines[].
            // CloudProfile.ModelName. Only fires when the user actually picked
            // a model from the live list (CloudLlmModelId non-empty) — skipped
            // pickers leave DictationModeDefaults seeds intact. The model ID is
            // written verbatim; zero hardcoded canonical model IDs on this path.
            updated = ApplyCloudLlmChoice(updated);

            await _settings.UpdateAsync(updated);

            // Save API keys if provided
            var keysToSave = new (string name, string value)[]
            {
                ("deepgram", DeepgramApiKey),
                ("gemini", GeminiApiKey),
                ("openai", OpenAiApiKey),
                ("anthropic", AnthropicApiKey),
                ("openrouter", OpenRouterApiKey),
                ("requesty", RequestyApiKey),
                ("inworld", InworldApiKey),
            };
            foreach (var (name, value) in keysToSave)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _secureStorage.StoreKey(name, value);
                    Log.Information("Wizard: saved {Provider} API key", name);
                }
            }

            Log.Information("Wizard completed: STT={Stt}, LLM={Llm}", defaultStt, defaultLlm);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save wizard settings");
        }

        WizardCompleted?.Invoke();
    }

    private bool NeedsApiKeys()
        => string.Equals(SttChoice, "cloud", StringComparison.Ordinal)
        || string.Equals(LlmChoice, "cloud", StringComparison.Ordinal)
        || string.Equals(TtsChoice, "cloud", StringComparison.Ordinal);

    /// <summary>
    /// Applies the user's cloud LLM provider + model pick to settings on wizard Finish:
    /// persists the default, propagates the model to every DictationModes/UtilityPipelines
    /// CloudProfile.ModelName, and auto-enables the pick in the CloudLlm catalog
    /// (SeenModelIds + DisabledModelIds). Skipped entirely for local-LLM wizard paths
    /// and for cloud paths where the user didn't pick a specific model.
    /// </summary>
    private AppSettings ApplyCloudLlmChoice(AppSettings updated)
    {
        if (string.Equals(LlmChoice, "local", StringComparison.Ordinal))
        {
            return updated;
        }

        if (string.IsNullOrWhiteSpace(CloudLlmModelId))
        {
            // Cloud path but no model pick: save only the provider so Settings
            // opens on the right tab; leave pipelines + catalog untouched.
            Log.Information(
                "Wizard: default LLM provider saved as {Provider}, model pick skipped",
                CloudLlmProvider);
            return updated with
            {
                CloudLlm = updated.CloudLlm with { DefaultCloudLlmProvider = CloudLlmProvider },
            };
        }

        // Defensive: JSON may hand back null for these lists on installs
        // that pre-date the SeenModelIds field.
        var seen = new HashSet<string>(updated.CloudLlm.SeenModelIds ?? [], StringComparer.Ordinal)
        {
            CloudLlmModelId,
        };
        var disabled = (updated.CloudLlm.DisabledModelIds ?? [])
            .Where(id => !string.Equals(id, CloudLlmModelId, StringComparison.Ordinal))
            .ToList();

        Log.Information(
            "Wizard: default LLM set to {Provider} / {Model} — propagated to pipelines + auto-enabled in catalog",
            CloudLlmProvider, CloudLlmModelId);

        return updated with
        {
            CloudLlm = updated.CloudLlm with
            {
                DefaultCloudLlmProvider = CloudLlmProvider,
                DefaultCloudLlmModelId = CloudLlmModelId,
                SeenModelIds = [.. seen],
                DisabledModelIds = disabled,
            },
            DictationModes =
            [
                .. updated.DictationModes.Select(m => m with
                {
                    CloudProfile = m.CloudProfile with { ModelName = CloudLlmModelId },
                }),
            ],
            UtilityPipelines =
            [
                .. updated.UtilityPipelines.Select(p => p with
                {
                    CloudProfile = p.CloudProfile with { ModelName = CloudLlmModelId },
                }),
            ],
        };
    }

    /// <summary>Navigate to the activation detour page. Called by "I Have a Key!" button.</summary>
    public void NavigateToActivation()
    {
        CurrentStep = ActivateStep;
        BeforeLeaveStep = null;
        CanGoBack = true;
        NextButtonText = _loc.GetString("Wizard_Next");
        StepChanged?.Invoke();
    }

    /// <summary>Navigate back to Get Started after successful activation.</summary>
    public void ReturnToGetStarted()
    {
        CurrentStep = 1;
        BeforeLeaveStep = null;
        UpdateNavState();
        StepChanged?.Invoke();
    }

    private void UpdateNavState()
    {
        CanGoBack = CurrentStep > 0;
        NextButtonText = CurrentStep == TotalSteps - 1 ? _loc.GetString("Wizard_Finish") : _loc.GetString("Wizard_Next");
    }
}
