
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardReadyPage : Page, IWizardStepPage
{
    private readonly LocalizationService _loc;

    public WizardReadyPage()
    {
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        string sttName = string.Equals(viewModel.SttChoice, "local", StringComparison.Ordinal)
            ? _loc.GetString("Wizard_Ready_Stt_Whisper")
            : _loc.GetString("Wizard_Ready_Stt_Deepgram");
        string llmName = viewModel.LlmChoice switch
        {
            "local" => _loc.GetString("Wizard_Ready_Llm_Ollama"),
            "skip" => _loc.GetString("Wizard_Ready_Llm_None"),
            _ => _loc.GetString("Wizard_Ready_Llm_Gemini"),
        };

        SttSummary.Text = _loc.GetFormatted("Wizard_Ready_SttSummary", sttName);
        LlmSummary.Text = _loc.GetFormatted("Wizard_Ready_LlmSummary", llmName);
    }
}
