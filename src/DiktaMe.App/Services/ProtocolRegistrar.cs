
using Microsoft.Win32;
using Serilog;

namespace DiktaMe.App.Services;
/// <summary>
/// Registers and manages the <c>diktame://</c> URL protocol in the Windows registry.
/// Uses HKCU (no admin elevation required).
/// </summary>
public static class ProtocolRegistrar
{
    private const string Protocol = "diktame";
    private const string RegistryKeyPath = @"Software\Classes\diktame";

    /// <summary>
    /// Registers the <c>diktame://</c> URL protocol handler pointing to the current executable.
    /// </summary>
    public static void Register()
    {
        string exePath = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrEmpty(exePath))
        {
            Log.Warning("ProtocolRegistrar: could not determine executable path — skipping registration");
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue("", "dIKta.me");
            key.SetValue("URL Protocol", "");

            using var commandKey = Registry.CurrentUser.CreateSubKey(RegistryKeyPath + @"\shell\open\command");
            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");

            Log.Information("ProtocolRegistrar: registered {Protocol}:// → {Exe}", Protocol, exePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ProtocolRegistrar: failed to register protocol");
        }
    }

    /// <summary>
    /// Returns true if the <c>diktame://</c> protocol is currently registered.
    /// </summary>
    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        return key is not null;
    }

    /// <summary>
    /// Removes the <c>diktame://</c> protocol registration from the registry.
    /// </summary>
    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryKeyPath, throwOnMissingSubKey: false);
            Log.Information("ProtocolRegistrar: unregistered {Protocol}://", Protocol);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ProtocolRegistrar: failed to unregister protocol");
        }
    }
}
