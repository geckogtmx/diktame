using System.Diagnostics;
using Serilog;

namespace DiktaMe.Core.SystemManagement;

/// <summary>
/// Registers and unregisters a Windows Task Scheduler task that launches
/// dIKta.me at user logon. Shell out to schtasks.exe (built into Windows)
/// rather than pulling in the COM TaskScheduler interop — zero dependencies,
/// zero trim warnings, behaviour identical to what Verify-AutoStart.ps1
/// observes via Get-ScheduledTask.
///
/// Task layout (matches test-helpers/Verify-AutoStart.ps1):
///   - Task name: "dIKta.me"
///   - Task folder: "\dIKta.me\"  (full path: "dIKta.me\dIKta.me")
///   - Trigger: ONLOGON for the current user
///   - Action: Environment.ProcessPath (the exe that registered the task)
/// </summary>
public sealed class AutoStartManager
{
    private const string TaskName = "dIKta.me";
    private const string TaskPath = @"dIKta.me\dIKta.me";

    /// <summary>
    /// Checks whether the auto-start task is currently registered.
    /// </summary>
    public bool IsRegistered()
    {
        try
        {
            var (exitCode, _) = RunSchtasks($"/Query /TN \"{TaskPath}\"");
            return exitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AutoStartManager: IsRegistered query failed");
            return false;
        }
    }

    /// <summary>
    /// Creates (or replaces) the ONLOGON task pointing at the current exe.
    /// Returns true on success, false on failure (failure is logged, not thrown).
    /// </summary>
    public bool Register()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            Log.Warning("AutoStartManager: Register aborted — Environment.ProcessPath is not a usable exe path ({Path})", exePath);
            return false;
        }

        // /F forces replace if the task already exists (idempotent toggle).
        // /RL LIMITED keeps the task at the user's standard privilege level —
        // the app doesn't need elevation and HIGHEST would trigger UAC prompts.
        string args = $"/Create /TN \"{TaskPath}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL LIMITED /F";
        var (exitCode, output) = RunSchtasks(args);
        if (exitCode == 0)
        {
            Log.Information("AutoStartManager: registered auto-start task at '{Exe}'", exePath);
            return true;
        }

        Log.Warning("AutoStartManager: schtasks /Create failed (exit {Code}): {Out}", exitCode, output);
        return false;
    }

    /// <summary>
    /// Removes the ONLOGON task. Returns true on success or if the task was
    /// already absent. Returns false only on unexpected error.
    /// </summary>
    public bool Unregister()
    {
        if (!IsRegistered())
        {
            return true;
        }

        var (exitCode, output) = RunSchtasks($"/Delete /TN \"{TaskPath}\" /F");
        if (exitCode == 0)
        {
            Log.Information("AutoStartManager: unregistered auto-start task");
            return true;
        }

        Log.Warning("AutoStartManager: schtasks /Delete failed (exit {Code}): {Out}", exitCode, output);
        return false;
    }

    /// <summary>
    /// Applies the desired state. Safe to call repeatedly — Register is idempotent
    /// via /F and Unregister no-ops when the task is already absent.
    /// </summary>
    public bool ApplyAsync(bool enabled)
    {
        return enabled ? Register() : Unregister();
    }

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo("schtasks.exe", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start schtasks.exe");

        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        string combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
        return (proc.ExitCode, combined.Trim());
    }
}
