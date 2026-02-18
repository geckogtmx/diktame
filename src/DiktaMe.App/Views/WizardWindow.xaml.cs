namespace DiktaMe.App.Views;

using DiktaMe.App.ViewModels;
using DiktaMe.App.Views.Wizard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

public sealed partial class WizardWindow : Window
{
    public WizardViewModel ViewModel { get; }

    private readonly Type[] _stepPages =
    {
        typeof(WizardWelcomePage),
        typeof(WizardSttPage),
        typeof(WizardLlmPage),
        typeof(WizardTestPage),
        typeof(WizardReadyPage),
    };

    public WizardWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<WizardViewModel>();
        this.InitializeComponent();

        // Size the window
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(600, 500));

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
        StepLabel.Text = $"Step {step + 1} of {WizardViewModel.TotalSteps}";
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
        // Open main window and close wizard
        App.Current.ShowMainWindow();
        this.Close();
    }
}
