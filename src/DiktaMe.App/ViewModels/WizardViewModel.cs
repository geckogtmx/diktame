
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using Serilog;

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

    // Step 0: Onboarding choice ("trial" or "apikeys")
    [ObservableProperty] private string _onboardingChoice = "trial";

    // Step 1: STT choice
    [ObservableProperty] private string _sttChoice = "cloud";

    // Step 2: LLM choice
    [ObservableProperty] private string _llmChoice = "cloud";

    // Step 3: API Keys (only shown if cloud providers selected)
    [ObservableProperty] private string _deepgramApiKey = "";
    [ObservableProperty] private string _geminiApiKey = "";

    public const int TotalSteps = 6;

    public event Action? StepChanged;
    public event Action? WizardCompleted;

    public WizardViewModel(SettingsManager settings, SecureStorage secureStorage, IAccountService accountService, LocalizationService loc)
    {
        _settings = settings;
        _secureStorage = secureStorage;
        _accountService = accountService;
        _loc = loc;
        _nextButtonText = _loc.GetString("Wizard_Next");
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            UpdateNavState();
            StepChanged?.Invoke();
        }
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        // Trial fork — skip entire wizard when "Try for free" is selected at step 0
        if (CurrentStep == 0 && string.Equals(OnboardingChoice, "trial", StringComparison.Ordinal))
        {
            await StartTrialAsync();
            return;
        }

        if (CurrentStep < TotalSteps - 1)
        {
            CurrentStep++;
            UpdateNavState();
            StepChanged?.Invoke();
        }
        else
        {
            // Final step — save and complete
            await CompleteWizardAsync();
        }
    }

    private async Task StartTrialAsync()
    {
        try
        {
            // Mark wizard as completed immediately (zero friction)
            var updated = _settings.Current with
            {
                WizardCompleted = true,
            };
            await _settings.UpdateAsync(updated);

            // Open browser for login — token will arrive via deeplink.
            // AuthMode will be set to Account by HandleAuthCallbackAsync,
            // then upgraded to Trial by RefreshStatusAsync if server confirms.
            _accountService.Login();

            Log.Information("Wizard: trial path — wizard completed, browser opened for login");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Wizard: failed to start trial");
        }

        WizardCompleted?.Invoke();
    }

    [RelayCommand]
    private async Task SkipWizardAsync()
    {
        var updated = _settings.Current with { WizardCompleted = true };
        await _settings.UpdateAsync(updated);
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
                "skip" => "none",
                _ => "gemini",
            };

            // LLM choice determines ActiveProfileName (controls model name resolution).
            // STT choice is independent — handled by ModeProfiles provider field.
            string profileName = string.Equals(LlmChoice, "local", StringComparison.Ordinal)
                ? "Local" : "Cloud";

            var updated = _settings.Current with
            {
                WizardCompleted = true,
                ActiveProfileName = profileName,
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
            await _settings.UpdateAsync(updated);

            // Save API keys if provided
            if (!string.IsNullOrWhiteSpace(DeepgramApiKey))
            {
                _secureStorage.StoreKey("deepgram", DeepgramApiKey);
                Log.Information("Wizard: saved Deepgram API key");
            }
            if (!string.IsNullOrWhiteSpace(GeminiApiKey))
            {
                _secureStorage.StoreKey("gemini", GeminiApiKey);
                Log.Information("Wizard: saved Gemini API key");
            }

            Log.Information("Wizard completed: STT={Stt}, LLM={Llm}", defaultStt, defaultLlm);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save wizard settings");
        }

        WizardCompleted?.Invoke();
    }

    private void UpdateNavState()
    {
        CanGoBack = CurrentStep > 0;
        NextButtonText = CurrentStep == TotalSteps - 1 ? _loc.GetString("Wizard_Finish") : _loc.GetString("Wizard_Next");
    }
}
