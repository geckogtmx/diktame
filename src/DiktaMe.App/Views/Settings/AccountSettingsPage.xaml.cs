
using DiktaMe.App.ViewModels.Settings;
using DiktaMe.Core.Account;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class AccountSettingsPage : Page
{
    private IAccountService? _accountService;

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
        _accountService = App.Current.Services.GetService<IAccountService>();

        if (_accountService is not null)
        {
            _accountService.AuthStateChanged += OnAuthStateChanged;
        }

        // Always refresh on load to pick up changes that happened while navigated away
        ViewModel.Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_accountService is not null)
        {
            _accountService.AuthStateChanged -= OnAuthStateChanged;
        }
    }

    private void OnAuthStateChanged(bool signedIn)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.Refresh());
    }
}
