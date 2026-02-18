
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Serilog;

namespace DiktaMe.Core.Input;
/// <summary>
/// Registers and manages global hotkeys using Win32 RegisterHotKey / UnregisterHotKey.
/// Hotkeys are identified by a <see cref="HotkeyId"/> enum.
/// Runs a hidden message-only window on a dedicated background thread to pump
/// WM_HOTKEY messages without requiring a UI dispatcher.
/// Port of src/services/hotkeyManager.ts from V1.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int DebouncePeriodMs = 500;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised on the message-pump thread when a registered hotkey is pressed.
    /// </summary>
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>
    /// Raised when a hotkey fails to register (e.g., already taken by another app).
    /// </summary>
    public event EventHandler<HotkeyRegistrationFailedEventArgs>? RegistrationFailed;

    // ── State ────────────────────────────────────────────────────────────────

    private Thread? _pumpThread;
    private IntPtr _hwnd = IntPtr.Zero;
    private readonly ManualResetEventSlim _hwndReady = new(false);
    private readonly ConcurrentDictionary<HotkeyId, HotkeyRegistration> _registrations = new();
    private readonly ConcurrentDictionary<HotkeyId, long> _lastPressTimestamps = new();
    private volatile bool _running;
    private bool _disposed;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the background message pump. Must be called before registering hotkeys.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running)
        {
            return;
        }

        _running = true;
        _pumpThread = new Thread(MessagePumpLoop)
        {
            IsBackground = true,
            Name = "HotkeyManager.MessagePump",
        };
        _pumpThread.Start();

        // Wait until the background thread has created the HWND (or failed)
        _hwndReady.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Registers a hotkey string for a named <see cref="HotkeyId"/>.
    /// Any existing registration for that ID is unregistered first.
    /// </summary>
    /// <param name="id">The logical hotkey ID.</param>
    /// <param name="hotkeyString">
    /// Hotkey combination string, e.g. <c>"Ctrl+Alt+D"</c> or <c>"Control+Alt+D"</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if registration succeeded; <c>false</c> if the key combination
    /// is already taken by another application.
    /// </returns>
    public bool Register(HotkeyId id, string hotkeyString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Call Start() before registering hotkeys.");
        }

        // Parse the hotkey string
        if (!HotkeyParser.TryParse(hotkeyString, out uint modifiers, out uint vk))
        {
            Log.Warning("HotkeyManager: cannot parse hotkey string '{HotkeyString}' for {Id}", hotkeyString, id);
            RegistrationFailed?.Invoke(this, new HotkeyRegistrationFailedEventArgs(id, hotkeyString,
                $"Cannot parse hotkey string: '{hotkeyString}'"));
            return false;
        }

        // Unregister any existing registration for this ID
        Unregister(id);

        int intId = (int)id;
        bool ok = NativeMethods.RegisterHotKey(_hwnd, intId, modifiers, vk);

        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            Log.Warning("HotkeyManager: RegisterHotKey failed for {Id} ('{HotkeyString}'), Win32 error {Error}",
                id, hotkeyString, err);
            RegistrationFailed?.Invoke(this, new HotkeyRegistrationFailedEventArgs(id, hotkeyString,
                $"Win32 RegisterHotKey failed (error {err}) — another app may be using '{hotkeyString}'"));
            return false;
        }

        _registrations[id] = new HotkeyRegistration(id, hotkeyString, modifiers, vk);
        Log.Information("HotkeyManager: registered {Id} → '{HotkeyString}'", id, hotkeyString);
        return true;
    }

    /// <summary>
    /// Unregisters the hotkey for the specified <see cref="HotkeyId"/>.
    /// No-op if not currently registered.
    /// </summary>
    public void Unregister(HotkeyId id)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        if (_registrations.TryRemove(id, out _))
        {
            NativeMethods.UnregisterHotKey(_hwnd, (int)id);
            Log.Debug("HotkeyManager: unregistered {Id}", id);
        }
    }

    /// <summary>
    /// Unregisters all currently registered hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (HotkeyId id in _registrations.Keys)
        {
            Unregister(id);
        }
    }

    /// <summary>
    /// Re-registers a hotkey with a new key combination.
    /// Equivalent to <c>Unregister(id); Register(id, newHotkeyString);</c>
    /// </summary>
    public bool Reregister(HotkeyId id, string newHotkeyString)
        => Register(id, newHotkeyString);

    // ── Message pump ─────────────────────────────────────────────────────────

    private void MessagePumpLoop()
    {
        // Create a message-only window to receive WM_HOTKEY messages
        _hwnd = NativeMethods.CreateMessageOnlyWindow();

        if (_hwnd == IntPtr.Zero)
        {
            Log.Error("HotkeyManager: failed to create message-only window (Win32 error {Error})",
                Marshal.GetLastWin32Error());
            _running = false;
            _hwndReady.Set(); // unblock Start() so it doesn't hang
            return;
        }

        Log.Debug("HotkeyManager: message pump started (hwnd=0x{Hwnd:X})", _hwnd.ToInt64());
        _hwndReady.Set(); // signal Start() that HWND is ready

        try
        {
            while (_running)
            {
                int result = NativeMethods.GetMessage(out NativeMethods.MSG msg, _hwnd, 0, 0);

                if (result == 0 || result == -1)
                {
                    break; // WM_QUIT or error
                }

                if (msg.message == WmHotkey)
                {
                    OnWmHotkey((int)msg.wParam);
                }

                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            UnregisterAll();
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
            Log.Debug("HotkeyManager: message pump stopped");
        }
    }

    private void OnWmHotkey(int id)
    {
        if (!Enum.IsDefined(typeof(HotkeyId), id))
        {
            Log.Debug("HotkeyManager: received WM_HOTKEY for unknown id {Id}", id);
            return;
        }

        var hotkeyId = (HotkeyId)id;

        // Debounce: ignore presses within 500ms of the last one for this ID
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long last = _lastPressTimestamps.GetOrAdd(hotkeyId, 0L);

        if (now - last < DebouncePeriodMs)
        {
            Log.Debug("HotkeyManager: debounced {Id}", hotkeyId);
            return;
        }

        _lastPressTimestamps[hotkeyId] = now;

        Log.Debug("HotkeyManager: {Id} pressed", hotkeyId);
        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkeyId));
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _running = false;

        // Post WM_QUIT to unblock GetMessage
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(_hwnd, NativeMethods.WmQuit, IntPtr.Zero, IntPtr.Zero);
        }

        _pumpThread?.Join(millisecondsTimeout: 2000);
        _hwndReady.Dispose();
    }

    // ── P/Invoke ─────────────────────────────────────────────────────────────

    private static class NativeMethods
    {
        internal const uint WmQuit = 0x0012;
        internal const string MessageWindowClassName = "DiktaMe_HotkeyWindow";

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        internal static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        internal static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        // Static field keeps the delegate rooted for the process lifetime,
        // preventing the GC from collecting it while the native window holds a reference.
        private static readonly WndProcDelegate StaticWndProc = DefWindowProc;

        internal static IntPtr CreateMessageOnlyWindow()
        {
            // HWND_MESSAGE parent makes it message-only (no visible window)
            const IntPtr HwndMessage = unchecked((IntPtr)(-3));

            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(StaticWndProc),
                hInstance = GetModuleHandle(null),
                lpszClassName = MessageWindowClassName,
            };

            RegisterClassEx(ref wc); // ignore error if already registered

            return CreateWindowEx(
                0, MessageWindowClassName, "DiktaMe Hotkey Window",
                0, 0, 0, 0, 0,
                HwndMessage, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX, ptY;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}

// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>
/// The logical identifier for each hotkey slot.
/// Integer values are used as the Win32 RegisterHotKey ID parameter.
/// </summary>
public enum HotkeyId
{
    Dictate = 1,
    Ask = 2,
    Translate = 3,
    Refine = 4,
    Oops = 5,
    Note = 6,
}

/// <summary>Event data for <see cref="HotkeyManager.HotkeyPressed"/>.</summary>
public sealed class HotkeyPressedEventArgs(HotkeyId id) : EventArgs
{
    /// <summary>The hotkey that was pressed.</summary>
    public HotkeyId Id { get; } = id;
}

/// <summary>Event data for <see cref="HotkeyManager.RegistrationFailed"/>.</summary>
public sealed class HotkeyRegistrationFailedEventArgs(HotkeyId id, string hotkeyString, string reason) : EventArgs
{
    /// <summary>The hotkey that failed to register.</summary>
    public HotkeyId Id { get; } = id;

    /// <summary>The hotkey string that was attempted.</summary>
    public string HotkeyString { get; } = hotkeyString;

    /// <summary>Human-readable reason for the failure.</summary>
    public string Reason { get; } = reason;
}

/// <summary>Internal record of an active hotkey registration.</summary>
internal sealed record HotkeyRegistration(HotkeyId Id, string HotkeyString, uint Modifiers, uint Vk);
