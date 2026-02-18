namespace DiktaMe.App.Services;

using DiktaMe.Core.Config;
using Microsoft.Toolkit.Uwp.Notifications;
using Serilog;

/// <summary>
/// Notification types for toast and sound feedback.
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Error,
    ModeChange,
}

/// <summary>
/// Provides toast notifications and sound feedback.
/// </summary>
public sealed class NotificationService
{
    private readonly SettingsManager _settings;

    public NotificationService(SettingsManager settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Shows a Windows toast notification and optionally plays a sound.
    /// </summary>
    public void ShowToast(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();

            if (_settings.Current.General.SoundFeedback)
                PlaySound(type);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show toast notification");
        }
    }

    /// <summary>
    /// Plays a system sound matching the notification type.
    /// </summary>
    public void PlaySound(NotificationType type)
    {
        if (!_settings.Current.General.SoundFeedback)
            return;

        try
        {
            // Use Windows system sounds via MediaPlayer
            var player = new Windows.Media.Playback.MediaPlayer
            {
                Volume = 0.5,
            };

            // Map notification type to a system sound URI
            string soundUri = type switch
            {
                NotificationType.Success => "ms-winsoundevent:Notification.Default",
                NotificationType.Error => "ms-winsoundevent:Notification.Looping.Alarm",
                NotificationType.ModeChange => "ms-winsoundevent:Notification.Default",
                _ => "ms-winsoundevent:Notification.Default",
            };

            player.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(soundUri));
            player.Play();

            // Auto-dispose after a short delay
            _ = Task.Delay(3000).ContinueWith(_ => player.Dispose());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to play notification sound");
        }
    }
}
