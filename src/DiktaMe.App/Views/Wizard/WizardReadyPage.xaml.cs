
using DiktaMe.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardReadyPage : Page, IWizardStepPage
{
    public WizardReadyPage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        string sttName = string.Equals(viewModel.SttChoice, "local", StringComparison.Ordinal) ? "Whisper (local)" : "Deepgram (cloud)";
        string llmName = viewModel.LlmChoice switch
        {
            "local" => "Ollama (local)",
            "skip" => "None (raw dictation)",
            _ => "Gemini (cloud)",
        };

        SttSummary.Text = $"Speech-to-Text: {sttName}";
        LlmSummary.Text = $"AI Processing: {llmName}";
    }
}
