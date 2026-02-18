namespace DiktaMe.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using Serilog;

public sealed partial class WizardViewModel : ObservableObject
{
    private readonly SettingsManager _settings;

    [ObservableProperty] private int _currentStep;
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoNext = true;
    [ObservableProperty] private string _nextButtonText = "Next";

    // Step 1: STT choice
    [ObservableProperty] private string _sttChoice = "cloud";

    // Step 2: LLM choice
    [ObservableProperty] private string _llmChoice = "cloud";

    public const int TotalSteps = 5;

    public event Action? StepChanged;
    public event Action? WizardCompleted;

    public WizardViewModel(SettingsManager settings)
    {
        _settings = settings;
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

            var updated = _settings.Current with { WizardCompleted = true };

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
        NextButtonText = CurrentStep == TotalSteps - 1 ? "Finish" : "Next";
    }
}
