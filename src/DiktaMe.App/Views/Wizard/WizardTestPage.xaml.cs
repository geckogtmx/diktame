
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Audio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace DiktaMe.App.Views.Wizard;
public sealed partial class WizardTestPage : Page, IWizardStepPage
{
    private WizardViewModel? _viewModel;

    public WizardTestPage()
    {
        this.InitializeComponent();
        LoadMicrophones();
    }

    public void SetViewModel(WizardViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    private void LoadMicrophones()
    {
        try
        {
            var devices = AudioDeviceManager.GetInputDevices();
            MicrophoneComboBox.ItemsSource = devices;

            // Select default device (index 0) if available
            if (devices.Count > 0)
            {
                MicrophoneComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load microphone devices");
            ResultText.Text = "Warning: Could not load microphone list";
        }
    }

    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        RecordButton.IsEnabled = false;
        RecordingProgress.IsActive = true;
        ResultText.Text = "Recording...";

        try
        {
            var recorder = App.Current.Services.GetRequiredService<AudioRecorder>();

            // Get selected device index from ComboBox
            var selectedDevice = MicrophoneComboBox.SelectedItem as AudioDevice;
            int deviceIndex = selectedDevice?.Index ?? 0;

            // Record for 3 seconds using selected device
            recorder.StartRecording(
                deviceId: deviceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                maxDurationSeconds: 3);
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
