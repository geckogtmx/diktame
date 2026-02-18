namespace DiktaMe.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using DiktaMe.Core.Pipeline;
using Microsoft.UI.Dispatching;
using Serilog;

public sealed partial class QuickChatViewModel : ObservableObject
{
    private readonly PipelineFactory _pipelineFactory;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public QuickChatViewModel(PipelineFactory pipelineFactory)
    {
        _pipelineFactory = pipelineFactory;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        string question = InputText.Trim();
        if (string.IsNullOrEmpty(question) || IsBusy) return;

        // Add user message
        Messages.Add(new ChatMessage(question, IsUser: true));
        InputText = "";
        IsBusy = true;

        try
        {
            var pipeline = _pipelineFactory.CreateChatPipeline();
            var options = new ChatOptions
            {
                SystemPrompt = "You are a helpful assistant. Answer concisely.",
                TextInput = question,
            };

            var result = await pipeline.RunAsync(options);

            _dispatcher.TryEnqueue(() =>
            {
                if (result.IsSuccess)
                {
                    Messages.Add(new ChatMessage(result.Text, IsUser: false));
                }
                else
                {
                    Messages.Add(new ChatMessage($"Error: {result.ErrorMessage}", IsUser: false));
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Quick chat failed");
            _dispatcher.TryEnqueue(() =>
            {
                Messages.Add(new ChatMessage($"Error: {ex.Message}", IsUser: false));
            });
        }
        finally
        {
            _dispatcher.TryEnqueue(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Messages.Clear();
        InputText = "";
    }
}

public sealed record ChatMessage(string Text, bool IsUser);
