# PROPOSAL: Pure Win32 GDI Overlay Window (Approach E)

## 1. Why This Approach Will Work

The core issue with the previous 5 attempts is **WinUI 3's reliance on DirectComposition**. WinUI 3 windows enforce an opaque background because their swapchains do not support alpha channels natively in a way that respects `Background="Transparent"` for top-level windows. Furthermore, classic Win32 transparency techniques like `LWA_COLORKEY` (Attempt 2) fail on WinUI 3 windows because DirectComposition bypasses the GDI rendering pipeline entirely.

By creating a **pure Win32 Window** (bypassing WinUI 3 completely for this specific overlay) from within our C# code:
1. We can use the classic GDI `LWA_COLORKEY` to make a specific color (e.g., Magenta) 100% transparent. This guarantees a true transparent background.
2. We can draw the dashed white border using pure GDI (`CreatePen`, `Rectangle`).
3. We apply `WS_EX_TRANSPARENT` for perfect click-through (input passes to windows below).
4. We apply `WDA_EXCLUDEFROMCAPTURE` to the HWND, successfully hiding the border from `ScreenRecorderLib`.

This relies on decades-old, battle-tested Win32 API features rather than fighting the WinUI 3 compositor.

## 2. Implementation Code

### A. New File: `src/DiktaMe.App/Views/RecordingBorderWindow.cs`
Create a new C# class to encapsulate the pure Win32 window.

```csharp
using System;
using System.Runtime.InteropServices;

namespace DiktaMe.App.Views;

/// <summary>
/// A pure Win32 overlay window that bypasses WinUI 3's opaque compositor.
/// Uses GDI color-key transparency to provide a click-through, non-recorded border.
/// </summary>
public sealed class RecordingBorderWindow : IDisposable
{
    private IntPtr _hWnd;
    private readonly WndProcDelegate _wndProc; // Keep alive to prevent GC
    private readonly string _className;
    private readonly int _width;
    private readonly int _height;

    public RecordingBorderWindow(int x, int y, int width, int height)
    {
        _width = width;
        _height = height;
        _className = "RecordingBorder_" + Guid.NewGuid().ToString("N");
        _wndProc = WndProc;

        var wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = Marshal.GetHINSTANCE(typeof(RecordingBorderWindow).Module),
            lpszClassName = _className,
            hCursor = LoadCursor(IntPtr.Zero, 32512) // IDC_ARROW
        };

        RegisterClassEx(ref wndClass);

        uint exStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
        uint style = WS_POPUP;

        _hWnd = CreateWindowEx(
            exStyle, _className, "Recording Border", style,
            x, y, width, height,
            IntPtr.Zero, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);

        if (_hWnd == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        // Set Magenta (0x00FF00FF) as the transparent color key
        SetLayeredWindowAttributes(_hWnd, 0x00FF00FF, 0, LWA_COLORKEY);

        // Hide from ScreenRecorderLib and OS capture
        SetWindowDisplayAffinity(_hWnd, WDA_EXCLUDEFROMCAPTURE);

        // Show window without stealing focus
        ShowWindow(_hWnd, SW_SHOWNOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_PAINT:
                IntPtr hdc = BeginPaint(hWnd, out PAINTSTRUCT ps);

                // 1. Fill background with Magenta (which becomes fully transparent via LWA_COLORKEY)
                IntPtr hBgBrush = CreateSolidBrush(0x00FF00FF);
                RECT clientRect = new RECT { left = 0, top = 0, right = _width, bottom = _height };
                FillRect(hdc, ref clientRect, hBgBrush);
                DeleteObject(hBgBrush);

                // 2. Draw dashed white border
                // Note: PS_DASH (1) only works with width=1 in pure GDI. 
                IntPtr hPen = CreatePen(PS_DASH, 1, 0x00FFFFFF); // White dashed pen
                IntPtr hOldPen = SelectObject(hdc, hPen);
                
                // Use a hollow brush for the inside of the rectangle
                IntPtr hNullBrush = GetStockObject(NULL_BRUSH);
                IntPtr hOldBrush = SelectObject(hdc, hNullBrush);

                // Draw outer and inner dashed lines to make it 2px thick
                Rectangle(hdc, 0, 0, _width, _height);
                Rectangle(hdc, 1, 1, _width - 1, _height - 1);

                SelectObject(hdc, hOldBrush);
                SelectObject(hdc, hOldPen);
                DeleteObject(hPen);

                EndPaint(hWnd, ref ps);
                return IntPtr.Zero;

            case WM_DESTROY:
                return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hWnd != IntPtr.Zero)
        {
            DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
    }

    // --- Win32 P/Invokes ---

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_POPUP = 0x80000000;
    private const uint LWA_COLORKEY = 1;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_DESTROY = 0x0002;
    private const int PS_DASH = 1;
    private const int NULL_BRUSH = 5;
    private const int SW_SHOWNOACTIVATE = 4;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int fnObject);

    [DllImport("gdi32.dll")]
    private static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);
}
```

### B. Modified File: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

Add a private field to track the border window lifecycle in `LoadingViewModel`:
```csharp
private Views.RecordingBorderWindow? _recordingBorder;
```

Modify the `HandleVideoCaptureViaCpAsync` method (where video recording is orchestrated) to instantiate the window right before recording starts, and dispose it when recording finishes or is cancelled.

```csharp
// 1. Right before starting the recording in HandleVideoCaptureViaCpAsync:
if (captureType == VisionCaptureType.VideoRegion && region is not null)
{
    _uiDispatcher?.TryEnqueue(() => 
    {
        _recordingBorder = new Views.RecordingBorderWindow(
            region.Value.X, region.Value.Y, 
            region.Value.Width, region.Value.Height);
    });
}

// 2. In the finally block or recording completion handler (e.g., OnRecordingStopRequested):
_uiDispatcher?.TryEnqueue(() =>
{
    _recordingBorder?.Dispose();
    _recordingBorder = null;
});
```

## 3. Dependencies
- **No NuGet packages required.** This solution uses strictly built-in Win32 P/Invokes (`user32.dll`, `gdi32.dll`), avoiding the need to add `System.Drawing.Common` or third-party wrapper dependencies.
- It integrates seamlessly with the current `net8.0-windows10.0.19041.0` target framework used in `DiktaMe.App.csproj`.

## 4. Known Risks & Fallbacks

- **Risk: High DPI / UI Scaling Mismatches.** Win32 windows operate in physical pixels by default. If the user has a 150% scaled display, the X/Y/Width/Height provided from the WinUI 3 selection overlay might be represented in logical pixels, which could cause the pure Win32 window to be misaligned or incorrectly sized.
  - *Mitigation:* `SnippingOverlayWindow` already deals with `ScreenRect` bounds which are typically correctly scaled. But if misalignment occurs, apply standard Win32 DPI scaling calculations (e.g., `GetDpiForMonitor`) to scale the coordinates before passing them to the `RecordingBorderWindow` constructor.
- **Fallback:** If pure Win32 proves too complex due to multi-monitor DPI scaling edge cases, we can fallback to **Approach C (Four Thin Opaque Windows)**. It avoids transparency entirely by creating 4 separate `2px` thick WinUI 3 windows positioned at the edges of the capture region, making them solid white and applying `WDA_EXCLUDEFROMCAPTURE` to all four.
