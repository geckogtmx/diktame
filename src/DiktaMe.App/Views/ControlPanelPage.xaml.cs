
using System;
using DiktaMe.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace DiktaMe.App.Views;
public sealed partial class ControlPanelPage : Page
{
    public ControlPanelViewModel ViewModel { get; }

    public ControlPanelPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ControlPanelViewModel>();
        this.InitializeComponent();
        RootGrid.SizeChanged += OnRootGridSizeChanged;
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var window = App.Current.MainWindow;
        if (window is null)
        {
            return;
        }

        double scale = XamlRoot?.RasterizationScale ?? 1.0;
        int physicalWidth = (int)(420 * scale);
        int physicalHeight = (int)(e.NewSize.Height * scale);

        var appWindow = window.AppWindow;
        var current = appWindow.Size;

        // Only resize if height actually changed (prevents infinite loop)
        if (Math.Abs(current.Height - physicalHeight) > 1)
        {
            appWindow.Resize(new SizeInt32(physicalWidth, physicalHeight));
        }
    }
}
