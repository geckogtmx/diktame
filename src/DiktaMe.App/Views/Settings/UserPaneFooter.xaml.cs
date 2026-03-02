
using DiktaMe.Core.Account;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class UserPaneFooter : UserControl
{
    private ITrialAccountService? _trialService;

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
        _trialService = App.Current.Services.GetService<ITrialAccountService>();
        if (_trialService is not null)
        {
            _trialService.StatusChanged += OnStatusChanged;
            UpdateDisplay();
        }
    }

    private void OnStatusChanged(TrialStatus? status)
    {
        DispatcherQueue.TryEnqueue(UpdateDisplay);
    }

    private void UpdateDisplay()
    {
        if (_trialService is null)
        {
            return;
        }

        bool signedIn = _trialService.HasValidToken && _trialService.Email is not null;

        if (signedIn)
        {
            string email = _trialService.Email!;
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
        if (_trialService is null)
        {
            return;
        }

        if (_trialService.HasValidToken)
        {
            // Navigate to Account settings page
            NavigateToAccountRequested?.Invoke();
        }
        else
        {
            // Open browser for login
            _trialService.Login();
        }
    }
}
