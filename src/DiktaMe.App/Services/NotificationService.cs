
using System.Text.RegularExpressions;
using DiktaMe.Core.Config;
using Microsoft.Toolkit.Uwp.Notifications;
using Serilog;

namespace DiktaMe.App.Services;
/// <summary>
/// Notification types for toast and sound feedback.
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
    ModeChange,
}

/// <summary>
/// Provides toast notifications and sound feedback.
/// </summary>
public sealed partial class NotificationService
{
    private readonly SettingsManager _settings;
    private static readonly string SoundsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");

    [GeneratedRegex(@"^[a-zA-Z0-9_-]+$")]
    private static partial Regex SafeStemPattern();

    public NotificationService(SettingsManager settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Shows a silent Windows toast notification (visual only, no sound).
    /// </summary>
    public void ShowToast(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddAudio(new Uri("ms-winsoundevent:Notification.Default"), silent: true)
                .Show();
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
        {
            return;
        }

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

    /// <summary>
    /// Plays a custom WAV sound file by stem name (e.g. "a" → Assets\Sounds\a.wav).
    /// Respects the SoundFeedback master toggle.
    /// </summary>
    public void PlayCustomSound(string soundStem)
    {
        if (!_settings.Current.General.SoundFeedback)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(soundStem))
        {
            return;
        }

        try
        {
            // Validate stem is safe (alphanumeric, hyphens, underscores only)
            if (!SafeStemPattern().IsMatch(soundStem))
            {
                Log.Warning("NotificationService: invalid sound stem '{Stem}'", soundStem);
                return;
            }

            string filePath = Path.Combine(SoundsDirectory, soundStem + ".wav");
            if (!File.Exists(filePath))
            {
                Log.Warning("NotificationService: sound file not found '{Path}'", filePath);
                PlaySound(NotificationType.Info); // fallback to system sound
                return;
            }

            var player = new Windows.Media.Playback.MediaPlayer
            {
                Volume = 0.5,
            };

            player.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(filePath));
            player.Play();

            // Auto-dispose after playback
            _ = Task.Delay(3000).ContinueWith(_ => player.Dispose());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to play custom sound '{Stem}'", soundStem);
        }
    }

    /// <summary>
    /// Returns available sound stem names by scanning the Assets\Sounds\ directory for .wav files.
    /// </summary>
    public static List<string> GetAvailableSounds()
    {
        try
        {
            if (!Directory.Exists(SoundsDirectory))
            {
                Log.Warning("NotificationService: sounds directory not found '{Dir}'", SoundsDirectory);
                return [];
            }

            return Directory.GetFiles(SoundsDirectory, "*.wav")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => name is not null)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate sound files");
            return [];
        }
    }
}
