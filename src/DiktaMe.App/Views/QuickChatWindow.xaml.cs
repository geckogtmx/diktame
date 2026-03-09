
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace DiktaMe.App.Views;
public sealed partial class QuickChatWindow : Window
{
    public QuickChatViewModel ViewModel { get; }

    public QuickChatWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<QuickChatViewModel>();
        this.InitializeComponent();

        // Window sizing from settings
        var settings = App.Current.Services.GetRequiredService<SettingsManager>();
        var chatSettings = settings.Current.Chat;
        int width = (int)chatSettings.WindowWidth;
        int height = (int)chatSettings.WindowHeight;
        if (width < 300) { width = 600; }
        if (height < 300) { height = 600; }

        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

        // Set window icon
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "tray-icon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }

        // Always-on-top from settings
        var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsAlwaysOnTop = chatSettings.AlwaysOnTop;
        }

        // Scroll to bottom when messages change
        ViewModel.Messages.CollectionChanged += (_, _) =>
        {
            if (MessageList.Items.Count > 0)
            {
                MessageList.ScrollIntoView(MessageList.Items[^1]);
            }
        };

        // Load models on window creation
        _ = ViewModel.LoadModelsCommand.ExecuteAsync(null);

        // Window-level keyboard accelerators
        var ctrlN = new KeyboardAccelerator { Key = VirtualKey.N, Modifiers = VirtualKeyModifiers.Control };
        ctrlN.Invoked += (_, _) => _ = ViewModel.NewConversationCommand.ExecuteAsync(null);

        var ctrlL = new KeyboardAccelerator { Key = VirtualKey.L, Modifiers = VirtualKeyModifiers.Control };
        ctrlL.Invoked += (_, _) => ViewModel.ClearMessagesCommand.Execute(null);

        if (this.Content is UIElement root)
        {
            root.KeyboardAccelerators.Add(ctrlN);
            root.KeyboardAccelerators.Add(ctrlL);
        }
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !ViewModel.IsBusy)
        {
            ViewModel.SendCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            this.Close();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Up && string.IsNullOrEmpty(ViewModel.InputText))
        {
            ViewModel.RecallLastMessage();
            e.Handled = true;
        }
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        // Load recent conversations when the flyout opens
        _ = ViewModel.LoadRecentConversationsCommand.ExecuteAsync(null);
    }

    private void HistoryList_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is DiktaMe.Core.Data.ConversationRecord conv)
        {
            _ = ViewModel.LoadConversationCommand.ExecuteAsync(conv.Id);
            HistoryFlyout.Hide();
        }
    }

    private void DeleteConversation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            _ = ViewModel.DeleteConversationCommand.ExecuteAsync(id);
            // Refresh the list after deletion
            _ = ViewModel.LoadRecentConversationsCommand.ExecuteAsync(null);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string text })
        {
            ViewModel.CopyMessageCommand.Execute(text);
        }
    }

    private async void MarkdownText_LinkClicked(object sender, CommunityToolkit.WinUI.UI.Controls.LinkClickedEventArgs e)
    {
        if (Uri.TryCreate(e.Link, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Messages.Count == 0)
        {
            return;
        }

        var picker = new FileSavePicker();
        picker.SuggestedFileName = $"{ViewModel.ConversationTitle}.md";
        picker.FileTypeChoices.Add("Markdown files", new List<string> { ".md" });
        picker.FileTypeChoices.Add("Text files", new List<string> { ".txt" });

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            string markdown = ViewModel.ExportAsMarkdown();
            await Windows.Storage.FileIO.WriteTextAsync(file, markdown);
        }
    }
}
