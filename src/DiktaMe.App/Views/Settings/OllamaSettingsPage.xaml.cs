
using DiktaMe.App.ViewModels.Settings;
using DiktaMe.Core.SystemManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class OllamaSettingsPage : Page
{
    public OllamaSettingsViewModel ViewModel { get; }

    public OllamaSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<OllamaSettingsViewModel>();
        this.InitializeComponent();

        // Show model info dialog when ViewModel signals it
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewModel.ShowModelInfo), StringComparison.Ordinal) && ViewModel.ShowModelInfo)
            {
                _ = ShowModelInfoDialogAsync();
            }
        };

        // Auto-check health on page load to populate model list
        this.Loaded += (s, e) => _ = ViewModel.CheckHealthCommand.ExecuteAsync(null);
    }

    private async Task ShowModelInfoDialogAsync()
    {
        ModelInfoDialog.XamlRoot = this.XamlRoot;
        await ModelInfoDialog.ShowAsync();
        ViewModel.ShowModelInfo = false;
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is OllamaModelDetail model)
        {
            ViewModel.ShowModelInfoCommand.Execute(model);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is OllamaModelDetail model)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Model",
                Content = $"Are you sure you want to delete {model.Name}?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DeleteModelCommand.Execute(model);
            }
        }
    }

    private void SearchView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.Tag is string modelName)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"https://ollama.com/library/{modelName}",
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to open Ollama model page for '{Model}'", modelName);
            }
        }
    }
}
