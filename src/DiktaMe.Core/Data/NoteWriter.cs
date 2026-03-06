
using System.Security;
using Serilog;

namespace DiktaMe.Core.Data;
/// <summary>
/// Appends formatted notes to a markdown file.
/// Used by <see cref="Pipeline.NotePipeline"/> to persist voice notes.
/// </summary>
public sealed class NoteWriter
{
    private const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Appends a note entry to the given file, creating parent directories as needed.
    /// Each entry is prefixed with a markdown heading containing the timestamp.
    /// </summary>
    /// <param name="filePath">Absolute path to the markdown notes file.</param>
    /// <param name="text">The note content to append.</param>
    /// <param name="timestampFormat">
    /// DateTime format string for the heading. Defaults to <c>yyyy-MM-dd HH:mm</c>.
    /// </param>
    public static async Task AppendAsync(
        string filePath,
        string text,
        string timestampFormat = DefaultTimestampFormat,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ValidateFilePath(filePath);

        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string timestamp = DateTime.Now.ToString(timestampFormat, System.Globalization.CultureInfo.InvariantCulture);
        string entry = $"\n## {timestamp}\n\n{text.Trim()}\n";

        await File.AppendAllTextAsync(filePath, entry, cancellationToken).ConfigureAwait(false);

        Log.Information("NoteWriter: appended {Chars} chars to '{Path}'", text.Length, filePath);
    }

    /// <summary>
    /// Validates that the file path is within allowed directories
    /// (user's Documents folder or %APPDATA%\DiktaMe).
    /// Prevents path traversal attacks via <c>../</c> sequences or absolute paths.
    /// </summary>
    internal static void ValidateFilePath(string filePath)
    {
        string canonicalPath = Path.GetFullPath(filePath);

        // Reject UNC paths
        if (canonicalPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new SecurityException($"NoteWriter: UNC paths are not allowed: '{filePath}'");
        }

        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiktaMe");

        bool isAllowed =
            canonicalPath.StartsWith(documentsPath, StringComparison.OrdinalIgnoreCase) ||
            canonicalPath.StartsWith(appDataPath, StringComparison.OrdinalIgnoreCase);

        if (!isAllowed)
        {
            throw new SecurityException(
                $"NoteWriter: path must be within Documents or AppData\\DiktaMe: '{filePath}'");
        }
    }
}
