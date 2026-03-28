# B7 Border Overlay — Implementation Proposal (Sonnet)

> **Approach:** Pure Win32 Layered Window with GDI+ rendering
> **Status:** Proposed 2026-03-27
> **Confidence:** High — this is the proven pre-DirectComposition pattern used by screen capture tools since Windows XP

---

## 1. Why the 5 Previous Attempts Failed (Root Cause)

All 5 failed attempts shared the same fatal assumption: **use a WinUI 3 window as the overlay host**. The root cause is:

> WinUI 3 windows use DirectComposition (DComp) for all rendering. The DComp visual tree always has an opaque root. `LWA_COLORKEY` only applies to GDI-rendered pixels; DComp pixels are invisible to the color-key mechanism. `WS_EX_LAYERED` on a DComp window has no effect on background transparency — only `LWA_ALPHA` (whole-window opacity) works.

Attempts 3 and 4 also kept the `SnippingOverlayWindow` alive — that window has a frozen screenshot as its background, so even if transparency had worked, the user would see a frozen image.

Attempt 5 (GuildOfCalamity `TransparentBackdrop`) may work for the main app window on some OS builds because that window uses `WinRT.Interop.WindowNative` and `DesktopWindowTarget` set up by the SDK. Secondary windows created via `new Window()` do not get the same DComp setup.

**The fix:** bypass WinUI 3 entirely for the overlay window. Use `CreateWindowEx` (Win32) to create a layered window, render with GDI+, and call `UpdateLayeredWindow`. This is the exact mechanism that OBS, ShareX, and every other screen capture tool uses for region indicators.

---

## 2. Why This Approach Will Work

- **`CreateWindowEx` with `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`** creates a true Win32 layered window. GDI+ renders into a DIB section (device-independent bitmap); `UpdateLayeredWindow` blends it with the desktop using per-pixel alpha. The background alpha = 0 → fully transparent. The dashed border pixels have alpha = 255 → fully visible.
- **`WDA_EXCLUDEFROMCAPTURE`** applied to this Win32 HWND will exclude it from `Windows.Graphics.Capture`, which is what ScreenRecorderLib uses. The Win32CaptureSample reference confirms this is the correct API.
- **`WS_EX_TRANSPARENT`** makes all mouse input fall through to windows below.
- No WinUI 3 compositor is involved — no DirectComposition, no XAML. This is pure Win32 + GDI+.

---

## 3. Implementation

### 3a. New File: `src/DiktaMe.App/Views/RecordingBorderOverlay.cs`

```csharp
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace DiktaMe.App.Views;

/// <summary>
/// Displays a dashed white border around the active recording region.
/// Uses a pure Win32 layered window (not WinUI 3) so that:
///   1. The background is truly transparent (GDI+ per-pixel alpha via UpdateLayeredWindow).
///   2. The window can be excluded from Windows.Graphics.Capture via WDA_EXCLUDEFROMCAPTURE.
///   3. Mouse input passes through via WS_EX_TRANSPARENT.
/// Call ShowAsync() before recording starts, Dispose() when recording ends.
/// </summary>
public sealed class RecordingBorderOverlay : IDisposable
{
    // ── Win32 constants ───────────────────────────────────────────────────────
    private const int WS_EX_LAYERED    = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST    = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_POPUP         = unchecked((int)0x80000000);
    private const int WS_VISIBLE       = 0x10000000;

    private const uint ULW_ALPHA       = 0x02;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    private const int HWND_TOPMOST     = -1;
    private const uint SWP_NOACTIVATE  = 0x0010;
    private const uint SWP_SHOWWINDOW  = 0x0040;

    // ── P/Invoke ──────────────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, uint crKey,
        ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage,
        out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    // ── Structs ───────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;          // AC_SRC_OVER = 0
        public byte BlendFlags;       // 0
        public byte SourceConstantAlpha; // 255 = use per-pixel alpha
        public byte AlphaFormat;      // AC_SRC_ALPHA = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int    biSize;
        public int    biWidth;
        public int    biHeight;
        public short  biPlanes;
        public short  biBitCount;
        public int    biCompression;
        public int    biSizeImage;
        public int    biXPelsPerMeter;
        public int    biYPelsPerMeter;
        public int    biClrUsed;
        public int    biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] bmiColors;
    }

    // ── Fields ────────────────────────────────────────────────────────────────
    private IntPtr _hwnd = IntPtr.Zero;
    private Thread? _messageThread;
    private volatile bool _disposed;
    private readonly int _left, _top, _width, _height;

    // Border appearance
    private const int BorderThickness = 2;
    private const float DashLength = 8f;
    private const float GapLength  = 6f;
    private const int AnimIntervalMs = 80; // dash march interval

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Create the overlay for the given recording region (screen coordinates).
    /// </summary>
    public RecordingBorderOverlay(int left, int top, int width, int height)
    {
        _left   = left;
        _top    = top;
        _width  = width;
        _height = height;
    }

    /// <summary>
    /// Creates the Win32 window and starts the render loop.
    /// Returns immediately — window runs on its own background thread.
    /// </summary>
    public Task ShowAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        _messageThread = new Thread(() =>
        {
            try
            {
                CreateOverlayWindow();
                tcs.TrySetResult(true);
                RunRenderLoop();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "RecordingBorderOverlay: failed to create window — overlay skipped");
                tcs.TrySetResult(false); // non-fatal
            }
        })
        {
            IsBackground = true,
            Name = "RecordingBorderOverlay",
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
        return tcs.Task;
    }

    /// <summary>
    /// Destroys the overlay window. Safe to call from any thread.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    // ── Private implementation ────────────────────────────────────────────────

    private void CreateOverlayWindow()
    {
        // The window covers the recording region exactly.
        // We make it slightly larger (BorderThickness on each side) so the
        // border sits OUTSIDE the recorded area. This means SourceRect in
        // VideoCapture.cs does not need to change — the border is outside the crop.
        int x = _left   - BorderThickness;
        int y = _top    - BorderThickness;
        int w = _width  + BorderThickness * 2;
        int h = _height + BorderThickness * 2;

        int exStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST
                    | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

        _hwnd = CreateWindowEx(
            dwExStyle: exStyle,
            lpClassName: "Static",      // built-in class, no registration needed
            lpWindowName: "RecordingBorder",
            dwStyle: WS_POPUP | WS_VISIBLE,
            x: x, y: y, nWidth: w, nHeight: h,
            hWndParent: IntPtr.Zero,
            hMenu: IntPtr.Zero,
            hInstance: IntPtr.Zero,
            lpParam: IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }

        // Exclude from Windows.Graphics.Capture (what ScreenRecorderLib uses)
        bool excluded = SetWindowDisplayAffinity(_hwnd, WDA_EXCLUDEFROMCAPTURE);
        Log.Information("RecordingBorderOverlay: WDA_EXCLUDEFROMCAPTURE = {Result}", excluded);

        // Ensure topmost
        SetWindowPos(_hwnd, (IntPtr)HWND_TOPMOST, x, y, w, h,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void RunRenderLoop()
    {
        float dashOffset = 0f;
        while (!_disposed)
        {
            PaintFrame(dashOffset);
            dashOffset += 2f;
            if (dashOffset >= DashLength + GapLength) dashOffset = 0f;
            Thread.Sleep(AnimIntervalMs);
        }
    }

    private void PaintFrame(float dashOffset)
    {
        if (_hwnd == IntPtr.Zero) return;

        int x = _left   - BorderThickness;
        int y = _top    - BorderThickness;
        int w = _width  + BorderThickness * 2;
        int h = _height + BorderThickness * 2;

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc    = CreateCompatibleDC(screenDc);

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize      = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth     = w,
                biHeight    = -h,   // top-down
                biPlanes    = 1,
                biBitCount  = 32,
                biCompression = 0,  // BI_RGB
            },
            bmiColors = new uint[4],
        };

        IntPtr ppvBits;
        IntPtr hBitmap = CreateDIBSection(memDc, ref bmi, 0, out ppvBits, IntPtr.Zero, 0);
        IntPtr hOldBmp = SelectObject(memDc, hBitmap);

        try
        {
            using var bmp    = new Bitmap(w, h, w * 4,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb, ppvBits);
            using var gfx    = Graphics.FromImage(bmp);
            gfx.Clear(Color.Transparent);
            gfx.SmoothingMode = SmoothingMode.None;

            using var pen = new Pen(Color.White, BorderThickness)
            {
                DashStyle  = DashStyle.Custom,
                DashPattern = [DashLength, GapLength],
                DashOffset  = dashOffset,
            };

            // Outer white dashes
            gfx.DrawRectangle(pen,
                BorderThickness / 2f,
                BorderThickness / 2f,
                w - BorderThickness,
                h - BorderThickness);

            // Inner dark shadow (offset by 1px) for visibility on light backgrounds
            pen.Color = Color.FromArgb(160, 0, 0, 0);
            pen.DashOffset = dashOffset + (DashLength + GapLength) / 2f;
            gfx.DrawRectangle(pen,
                BorderThickness / 2f + 1,
                BorderThickness / 2f + 1,
                w - BorderThickness - 2,
                h - BorderThickness - 2);

            var blend = new BLENDFUNCTION
            {
                BlendOp              = 0,   // AC_SRC_OVER
                BlendFlags           = 0,
                SourceConstantAlpha  = 255,
                AlphaFormat          = 1,   // AC_SRC_ALPHA
            };

            var ptDst = new POINT { x = x, y = y };
            var sz    = new SIZE  { cx = w, cy = h };
            var ptSrc = new POINT { x = 0, y = 0 };

            UpdateLayeredWindow(_hwnd, screenDc, ref ptDst, ref sz,
                memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, hOldBmp);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
```

### 3b. Modify: `src/DiktaMe.App/ViewModels/LoadingViewModel.cs`

Add a field alongside `_videoRecordingCts`:
```csharp
private RecordingBorderOverlay? _recordingBorderOverlay;
```

In `HandleVideoCaptureViaCpAsync`, after the `left`/`top`/`width`/`height` are finalised for a region capture (around line 2080, after the snippingResult block) and before `capture.RecordAsync`:

```csharp
// Show recording border (excluded from capture via WDA_EXCLUDEFROMCAPTURE)
if (!isFullScreen)
{
    _recordingBorderOverlay = new Views.RecordingBorderOverlay(left, top, width, height);
    await _recordingBorderOverlay.ShowAsync().ConfigureAwait(false);
}
```

In the `finally` block (around line 2161), dispose the border:
```csharp
_recordingBorderOverlay?.Dispose();
_recordingBorderOverlay = null;
```

---

## 4. Files to Create/Modify

| Action | File | Change |
|--------|------|--------|
| **Create** | `src/DiktaMe.App/Views/RecordingBorderOverlay.cs` | New class — full code above |
| **Modify** | `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Add `_recordingBorderOverlay` field + `ShowAsync()` call before recording + `Dispose()` in finally |

No new NuGet packages. `System.Drawing.Common` is already available on .NET 8 Windows (it's a Windows-only API and is included in the Windows target).

---

## 5. Why Each Design Decision Was Made

**Why `"Static"` window class?**
`CreateWindowEx` requires a registered window class. `"Static"` is a pre-registered Win32 class (like `"Button"`, `"Edit"`) — no `RegisterClassEx` call needed. We don't need window messages; the render loop runs on its own thread without a message pump.

**Why `WS_POPUP` with no message pump?**
A `WS_POPUP` with no `WM_PAINT` handler still renders via `UpdateLayeredWindow`, which is entirely independent of the message queue. No `DefWindowProc` is needed since we never receive mouse events (`WS_EX_TRANSPARENT` discards them) and don't need `WM_PAINT` (`UpdateLayeredWindow` manages the surface directly).

**Why a background thread?**
`UpdateLayeredWindow` + GDI+ can be called from any thread. Running on a background STA thread keeps the UI responsive and avoids blocking `HandleVideoCaptureViaCpAsync` during the render loop.

**Why `WS_EX_NOACTIVATE`?**
Prevents the border window from stealing keyboard focus when it's shown, which would otherwise break hotkeys or the CP Stop button.

**Why expand the window by `BorderThickness` on each side?**
The border is drawn *outside* the recording region. The `SourceRect` in `VideoCapture.cs` already defines the inner crop. The border window is positioned to surround it without overlapping the recorded area. This means even if `WDA_EXCLUDEFROMCAPTURE` somehow fails (edge case), the border pixels would still not appear in the video because they are outside the crop rectangle.

**Animated dashes (marching ants)**
Offset increments 2px every 80ms, cycling through `DashLength + GapLength = 14px`. This gives a subtle "marching ants" animation that makes the border clearly distinguishable from static screen content. Can be removed by skipping the `dashOffset` update if animation is unwanted.

---

## 6. Known Risks and Fallback

### Risk 1: `WDA_EXCLUDEFROMCAPTURE` returns `false`
Requires Windows 10 Build 2004 (20H1, May 2020 update) or later. The app already targets `net8.0-windows10.0.19041.0` — 19041 *is* 2004. `SetWindowDisplayAffinity` will return `false` on builds older than 19041. The log line captures this. If false, the border will appear in the recording. Mitigation: log a warning toast so the user knows. The recording still works.

### Risk 2: `CreateWindowEx` with `"Static"` class and no message pump deadlocks
Low probability — `UpdateLayeredWindow` doesn't require a message pump. The window receives no messages we care about. If Windows internally queues a `WM_DESTROY` we never process, `DestroyWindow` called from another thread should still work. Tested pattern: used by ShareX, Greenshot, and other WinForms-free overlay tools.

### Risk 3: GDI+ `System.Drawing` not available
`System.Drawing.Common` is included in the Windows-targeted .NET 8 runtime. `<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>` already activates it. No csproj changes needed.

### Risk 4: DPI scaling — border doesn't align to recording region
`VideoCapture.cs` uses screen coordinates from `SnippingOverlayWindow`, which already accounts for DPI. The border window uses the same `left`/`top`/`width`/`height` values, so alignment is DPI-consistent.

### Fallback
If `CreateWindowEx` fails (returns `IntPtr.Zero`), `ShowAsync` logs a warning and returns `false` — recording proceeds normally without a border. The `try/catch` in the `Thread` lambda ensures the overlay failure is non-fatal.

---

## 7. Verification Steps

1. Build: `dotnet build DiktaMe.sln -c Debug`
2. Start a region video recording.
3. Observe: dashed white border appears around the selected region on screen.
4. Observe: the recorded MP4 does NOT contain the border pixels (check by opening the file — the border area should show live screen content, not the white dashes).
5. Verify click-through: click on a window underneath the border area — input should pass through normally.
6. Stop recording: border disappears.
7. Check Serilog output: `WDA_EXCLUDEFROMCAPTURE = True` should appear in the log.
