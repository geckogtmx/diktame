namespace DiktaMe.App.Views;

using DiktaMe.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

public sealed partial class QuickChatWindow : Window
{
    public QuickChatViewModel ViewModel { get; }

    public QuickChatWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<QuickChatViewModel>();
        this.InitializeComponent();

        // Small, always-on-top window
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(420, 340));

        var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsAlwaysOnTop = true;
        }

        // Scroll to bottom when messages change
        ViewModel.Messages.CollectionChanged += (_, _) =>
        {
            if (MessageList.Items.Count > 0)
                MessageList.ScrollIntoView(MessageList.Items[^1]);
        };
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
    }
}
