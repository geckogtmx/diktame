namespace DiktaMe.App.Views.Wizard;

using DiktaMe.App.ViewModels;
using DiktaMe.Core.Audio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

public sealed partial class WizardTestPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;

    public WizardTestPage()
    {
        this.InitializeComponent();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        RecordButton.IsEnabled = false;
        RecordingProgress.IsActive = true;
        ResultText.Text = "Recording...";

        try
        {
            var recorder = App.Current.Services.GetRequiredService<AudioRecorder>();

            // Record for 3 seconds using default device
            recorder.StartRecording(maxDurationSeconds: 3);
            await Task.Delay(3500); // Wait a bit longer than max duration

            string? filePath = await recorder.StopRecordingAsync();

            if (filePath is not null)
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                ResultText.Text = $"Captured {fileInfo.Length / 1024} KB of audio. Your microphone is working!";
            }
            else
            {
                ResultText.Text = "Recording completed but no audio file was produced.";
            }
        }
        catch (Exception ex)
        {
            ResultText.Text = $"Recording failed: {ex.Message}";
            Log.Warning(ex, "Wizard test recording failed");
        }
        finally
        {
            RecordingProgress.IsActive = false;
            RecordButton.IsEnabled = true;
        }
    }
}
