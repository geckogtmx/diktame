
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using DiktaMe.App.Views.Wizard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DiktaMe.App.Views;
public sealed partial class WizardWindow : Window
{
    public WizardViewModel ViewModel { get; }
    private readonly LocalizationService _loc;

    private readonly Type[] _stepPages =
    {
        typeof(WizardLanguagePage),      // 0
        typeof(WizardGetStartedPage),    // 1
        typeof(WizardSttPage),           // 2
        typeof(WizardLlmPage),           // 3
        typeof(WizardTtsPage),           // 4
        typeof(WizardApiKeysPage),       // 5 (skipped if no cloud providers)
        typeof(WizardTestPage),          // 6
        typeof(WizardReadyPage),         // 7
    };

    public WizardWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<WizardViewModel>();
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        this.InitializeComponent();

        // Size the window
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(600, 500));

        // Set window icon
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }

        ViewModel.StepChanged += OnStepChanged;
        ViewModel.WizardCompleted += OnWizardCompleted;

        // Navigate to first page
        NavigateToCurrentStep();
    }

    private void OnStepChanged()
    {
        NavigateToCurrentStep();
    }

    private void NavigateToCurrentStep()
    {
        int step = ViewModel.CurrentStep;
        StepLabel.Text = _loc.GetFormatted("Wizard_StepLabel", step + 1, WizardViewModel.TotalSteps);
        StepProgress.Value = step;
        ContentFrame.Navigate(_stepPages[step]);

        // Pass ViewModel to the page if it implements IWizardStepPage
        if (ContentFrame.Content is IWizardStepPage page)
        {
            page.SetViewModel(ViewModel);
        }
    }

    private void OnWizardCompleted()
    {
        // Re-run the loading screen so it picks up the wizard's settings
        // (downloads Whisper model, warms Ollama, registers hotkeys, etc.)
        var loading = new LoadingWindow();
        loading.Activate();
        loading.StartLoading();
        this.Close();
    }
}
