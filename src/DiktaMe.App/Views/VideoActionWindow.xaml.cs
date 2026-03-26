using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Serilog;
using Windows.Graphics;

namespace DiktaMe.App.Views;

/// <summary>
/// Post-recording action modal — lets user choose an AI action for their video
/// (Describe, Document, Bug Report) or save without AI.
/// </summary>
public sealed partial class VideoActionWindow : Window
{
    private readonly TaskCompletionSource<VideoActionResult> _tcs = new();

    public VideoActionWindow()
    {
        InitializeComponent();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        // Compact window, centered
        AppWindow.Resize(new SizeInt32(380, 320));

        Content.KeyDown += OnKeyDown;
        Activated += (_, _) => QueryInput.Focus(FocusState.Programmatic);
    }

    /// <summary>Sets the file info display.</summary>
    public void SetFileInfo(string fileName, long fileSizeBytes, double? durationSeconds = null)
    {
        FileInfoText.Text = $"{fileName}  ({fileSizeBytes / 1024}KB)";
        if (durationSeconds.HasValue)
        {
            DurationText.Text = $"Duration: {durationSeconds.Value:F1}s";
        }
    }

    public Task<VideoActionResult> GetResultAsync() => _tcs.Task;

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Log.Debug("VideoAction: skip (Esc)");
            _tcs.TrySetResult(new VideoActionResult(VideoAiAction.None, null));
            Close();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(QueryInput.Text))
        {
            // Enter with custom text = Describe with custom prompt
            Log.Debug("VideoAction: custom prompt (Enter)");
            _tcs.TrySetResult(new VideoActionResult(VideoAiAction.Describe, QueryInput.Text.Trim()));
            Close();
            e.Handled = true;
        }
    }

    private void QueryInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(QueryInput.Text))
        {
            _tcs.TrySetResult(new VideoActionResult(VideoAiAction.Describe, QueryInput.Text.Trim()));
            Close();
            e.Handled = true;
        }
    }

    private void OnDescribe(object sender, RoutedEventArgs e)
    {
        string? custom = string.IsNullOrWhiteSpace(QueryInput.Text) ? null : QueryInput.Text.Trim();
        _tcs.TrySetResult(new VideoActionResult(VideoAiAction.Describe, custom));
        Close();
    }

    private void OnDocument(object sender, RoutedEventArgs e)
    {
        string? custom = string.IsNullOrWhiteSpace(QueryInput.Text) ? null : QueryInput.Text.Trim();
        _tcs.TrySetResult(new VideoActionResult(VideoAiAction.Document, custom));
        Close();
    }

    private void OnBugReport(object sender, RoutedEventArgs e)
    {
        string? custom = string.IsNullOrWhiteSpace(QueryInput.Text) ? null : QueryInput.Text.Trim();
        _tcs.TrySetResult(new VideoActionResult(VideoAiAction.BugReport, custom));
        Close();
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(new VideoActionResult(VideoAiAction.None, null));
        Close();
    }
}

/// <summary>AI action to perform on a recorded video.</summary>
public enum VideoAiAction
{
    None,
    Describe,
    Document,
    BugReport,
}

/// <summary>Result from the post-recording action modal.</summary>
public sealed record VideoActionResult(VideoAiAction Action, string? CustomPrompt);
