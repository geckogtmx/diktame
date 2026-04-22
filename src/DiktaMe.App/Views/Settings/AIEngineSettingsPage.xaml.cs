
using DiktaMe.App.ViewModels.Settings;
using DiktaMe.Core.SystemManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class AIEngineSettingsPage : Page
{
    public AIEngineSettingsViewModel ViewModel { get; }

    public AIEngineSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<AIEngineSettingsViewModel>();
        this.InitializeComponent();

        // Show model info dialog when Ollama ViewModel signals it
        ViewModel.Ollama.PropertyChanged += (s, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewModel.Ollama.ShowModelInfo), StringComparison.Ordinal) && ViewModel.Ollama.ShowModelInfo)
            {
                _ = ShowModelInfoDialogAsync();
            }
        };

        // Auto-check Ollama health on page load to populate model list
        this.Loaded += (s, e) => _ = ViewModel.Ollama.CheckHealthCommand.ExecuteAsync(null);

        // BUG-027: when the user changes the default cloud LLM provider, the
        // ViewModel persists the new default immediately and then raises this
        // event so we can offer the "reset all modes" propagation dialog.
        ViewModel.CloudLlm.DefaultProviderChangeRequested += OnDefaultProviderChangeRequested;
    }

    private async void OnDefaultProviderChangeRequested(
        object? sender, DefaultProviderChangeRequestedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset all mode model selections?",
            Content = $"Rewrite all Dictate, Refine, Ask, Translate, Note, and Chat "
                    + $"model selections to {e.ProviderDisplayName} / {e.ModelDisplayName}?\n\n"
                    + $"Choose No to keep your per-mode picks unchanged. The default "
                    + $"provider and model have already been saved either way.",
            PrimaryButtonText = "Yes, reset",
            CloseButtonText = "No, keep my picks",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.CloudLlm.PropagateDefaultModelToAllModesAsync(e.ModelId);
        }
    }

    private async Task ShowModelInfoDialogAsync()
    {
        ModelInfoDialog.XamlRoot = this.XamlRoot;
        await ModelInfoDialog.ShowAsync();
        ViewModel.Ollama.ShowModelInfo = false;
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is OllamaModelDetail model)
        {
            ViewModel.Ollama.ShowModelInfoCommand.Execute(model);
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
                ViewModel.Ollama.DeleteModelCommand.Execute(model);
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
