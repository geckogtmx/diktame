
using DiktaMe.Core.Account;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class UserPaneFooter : UserControl
{
    private IAccountService? _accountService;

    /// <summary>
    /// Raised when the user clicks the footer while signed in,
    /// requesting navigation to the Account settings page.
    /// </summary>
    public event Action? NavigateToAccountRequested;

    public UserPaneFooter()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _accountService = App.Current.Services.GetService<IAccountService>();
        if (_accountService is not null)
        {
            _accountService.AuthStateChanged += OnAuthStateChanged;
            UpdateDisplay();
        }
    }

    private void OnAuthStateChanged(bool signedIn)
    {
        DispatcherQueue.TryEnqueue(UpdateDisplay);
    }

    private void UpdateDisplay()
    {
        if (_accountService is null)
        {
            return;
        }

        bool signedIn = _accountService.HasValidToken && _accountService.Email is not null;

        if (signedIn)
        {
            string email = _accountService.Email!;
            UserText.Text = email;

            // Show colored avatar with first letter
            string initial = email.Length > 0
                ? email[0].ToString().ToUpperInvariant()
                : "?";
            AvatarInitial.Text = initial;
            AvatarCircle.Visibility = Visibility.Visible;
            SignedOutIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            UserText.Text = "Sign in";
            AvatarCircle.Visibility = Visibility.Collapsed;
            SignedOutIcon.Visibility = Visibility.Visible;
        }
    }

    private void UserButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accountService is null)
        {
            return;
        }

        if (_accountService.HasValidToken)
        {
            // Navigate to Account settings page
            NavigateToAccountRequested?.Invoke();
        }
        else
        {
            // Open browser for login
            _accountService.Login();
        }
    }
}
