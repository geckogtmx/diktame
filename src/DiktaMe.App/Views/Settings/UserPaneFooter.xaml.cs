
using DiktaMe.Core.Account;
using DiktaMe.Core.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiktaMe.App.Views.Settings;
public sealed partial class UserPaneFooter : UserControl
{
    private IAccountService? _accountService;
    private bool _isCompact;

    /// <summary>
    /// Raised when the user clicks the footer,
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

    /// <summary>
    /// Switches between expanded (avatar + text) and compact (avatar only) layouts.
    /// Called by SettingsWindow when the nav pane toggles.
    /// </summary>
    public void SetCompactMode(bool isCompact)
    {
        _isCompact = isCompact;
        UpdateDisplay();
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

        // Avatar/icon sizing: 28px expanded, 20px compact (matches logo)
        double avatarSize = _isCompact ? 20 : 28;
        double dotSize = _isCompact ? 6 : 8;
        double dotStroke = _isCompact ? 1.5 : 2;

        AvatarCircle.Width = avatarSize;
        AvatarCircle.Height = avatarSize;
        AvatarInitial.FontSize = _isCompact ? 10 : 13;
        StatusDot.Width = dotSize;
        StatusDot.Height = dotSize;
        StatusDot.StrokeThickness = dotStroke;

        if (signedIn)
        {
            string email = _accountService.Email!;

            // Use OAuth display name if available, fall back to email prefix
            var settings = App.Current.Services.GetRequiredService<SettingsManager>();
            string displayName = settings.Current.Account.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                int atIndex = email.IndexOf('@', StringComparison.Ordinal);
                displayName = atIndex > 0 ? email[..atIndex] : email;
            }

            // Avatar initial
            string initial = displayName.Length > 0
                ? displayName[0].ToString().ToUpperInvariant()
                : "?";
            AvatarInitial.Text = initial;

            // Show avatar + status dot, hide person icon
            AvatarContainer.Visibility = Visibility.Visible;
            SignedOutIcon.Visibility = Visibility.Collapsed;

            // Text: show display name + email when expanded, hide when compact
            DisplayNameText.Text = displayName;
            UserText.Text = email;
            DisplayNameText.Visibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
            TextPanel.Visibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            // Signed out: person icon + "Sign in"
            AvatarContainer.Visibility = Visibility.Collapsed;
            SignedOutIcon.Visibility = Visibility.Visible;
            SignedOutIcon.FontSize = _isCompact ? 14 : 16;

            DisplayNameText.Visibility = Visibility.Collapsed;
            UserText.Text = "Sign in";
            TextPanel.Visibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void UserButton_Click(object sender, RoutedEventArgs e)
    {
        // Always navigate to Account page, regardless of sign-in state
        NavigateToAccountRequested?.Invoke();
    }
}
