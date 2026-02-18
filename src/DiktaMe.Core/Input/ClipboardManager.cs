
using System.ComponentModel;
using System.Runtime.InteropServices;
using Serilog;

namespace DiktaMe.Core.Input;
/// <summary>
/// Reads and writes plain-text clipboard content using Win32 P/Invoke.
/// Using Win32 directly (rather than WinUI 3's DataPackage) keeps this
/// class usable from background threads without a dispatcher.
/// Port of pyperclip usage in python/core/injector.py.
/// </summary>
public static class ClipboardManager
{
    // Retry parameters for transient clipboard lock failures
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 20;

    /// <summary>
    /// Gets the current plain-text content of the clipboard.
    /// Returns an empty string if the clipboard is empty or contains no text.
    /// </summary>
    public static string GetText()
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (!NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(RetryDelayMs);
                continue;
            }

            try
            {
                IntPtr handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
                if (handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                IntPtr ptr = NativeMethods.GlobalLock(handle);
                if (ptr == IntPtr.Zero)
                {
                    return string.Empty;
                }

                try
                {
                    return Marshal.PtrToStringUni(ptr) ?? string.Empty;
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ClipboardManager: GetText failed on attempt {Attempt}", attempt + 1);
                return string.Empty;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        Log.Warning("ClipboardManager: GetText could not open clipboard after {MaxRetries} attempts", MaxRetries);
        return string.Empty;
    }

    /// <summary>
    /// Sets the clipboard to the specified plain-text content.
    /// Passing an empty string or null clears the clipboard.
    /// </summary>
    public static void SetText(string? text)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (!NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(RetryDelayMs);
                continue;
            }

            try
            {
                NativeMethods.EmptyClipboard();

                if (!string.IsNullOrEmpty(text))
                {
                    // Allocate global memory for the string (including null terminator)
                    int byteCount = (text.Length + 1) * 2;
                    IntPtr hMem = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)byteCount);
                    if (hMem == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalAlloc failed");
                    }

                    IntPtr ptr = NativeMethods.GlobalLock(hMem);
                    if (ptr == IntPtr.Zero)
                    {
                        NativeMethods.GlobalFree(hMem);
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalLock failed");
                    }

                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                        Marshal.WriteInt16(ptr + text.Length * 2, 0); // null terminator
                    }
                    finally
                    {
                        NativeMethods.GlobalUnlock(hMem);
                    }

                    NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hMem);
                    // Clipboard now owns hMem — do not free it
                }

                return; // success
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ClipboardManager: SetText failed on attempt {Attempt}", attempt + 1);
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }

            Thread.Sleep(RetryDelayMs);
        }

        Log.Warning("ClipboardManager: SetText could not open clipboard after {MaxRetries} attempts", MaxRetries);
    }

    // ── P/Invoke ─────────────────────────────────────────────────────────────

    private static class NativeMethods
    {
        internal const uint CF_UNICODETEXT = 13;
        internal const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GlobalFree(IntPtr hMem);
    }
}
