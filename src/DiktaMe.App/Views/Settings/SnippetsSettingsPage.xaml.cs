
using DiktaMe.App.ViewModels.Settings;
using DiktaMe.Core.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DiktaMe.App.Views.Settings;
public sealed partial class SnippetsSettingsPage : Page
{
    private static readonly Brush EvenRowBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x2a, 0x35)); // AppSurface2Brush
    private static readonly Brush OddRowBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x30, 0x3d));  // AppSurfaceBrush

    public SnippetsSettingsViewModel ViewModel { get; }

    public SnippetsSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SnippetsSettingsViewModel>();
        this.InitializeComponent();
    }

    private void ListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is ListViewItem container
            && container.ContentTemplateRoot is Border border)
        {
            border.Background = args.ItemIndex % 2 == 0 ? EvenRowBrush : OddRowBrush;
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Snippet snippet)
        {
            ViewModel.SelectedSnippet = snippet;
            ViewModel.EditSelectedSnippetCommand.Execute(null);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Snippet snippet)
        {
            ViewModel.SelectedSnippet = snippet;
            ViewModel.DeleteSnippetCommand.Execute(null);
        }
    }
}
