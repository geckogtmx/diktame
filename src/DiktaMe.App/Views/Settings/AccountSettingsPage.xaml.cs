
using DiktaMe.App.ViewModels.Settings;
using DiktaMe.Core.Account;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class AccountSettingsPage : Page
{
    private ITrialAccountService? _trialService;

    public AccountSettingsViewModel ViewModel { get; }

    public AccountSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<AccountSettingsViewModel>();
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _trialService = App.Current.Services.GetService<ITrialAccountService>();
        if (_trialService is not null)
        {
            _trialService.StatusChanged += OnStatusChanged;
        }

        // Always refresh on load to pick up changes that happened while navigated away
        ViewModel.Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_trialService is not null)
        {
            _trialService.StatusChanged -= OnStatusChanged;
        }
    }

    private void OnStatusChanged(TrialStatus? status)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.Refresh());
    }
}
