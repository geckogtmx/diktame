using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Serilog;

namespace DiktaMe.App.Views;

/// <summary>
/// Displays a dashed white border around the active recording region.
/// Uses a pure Win32 layered window (not WinUI 3) so that:
///   1. The background is truly transparent (GDI+ per-pixel alpha via UpdateLayeredWindow).
///   2. The window can be excluded from Windows.Graphics.Capture via WDA_EXCLUDEFROMCAPTURE.
///   3. Mouse input passes through via WS_EX_TRANSPARENT.
/// Call ShowAsync() before recording starts, Dispose() when recording ends.
/// Based on proposal: plans/B7_BORDER_PROPOSAL_SONNET.md
/// </summary>
public sealed class RecordingBorderOverlay : IDisposable
{
    // ── Win32 constants ───────────────────────────────────────────────────────
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;

    private const uint ULW_ALPHA = 0x02;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    private const int HWND_TOPMOST = -1;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

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
    [return: MarshalAs(UnmanagedType.Bool)]
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
        public byte BlendOp;             // AC_SRC_OVER = 0
        public byte BlendFlags;          // 0
        public byte SourceConstantAlpha; // 255 = use per-pixel alpha
        public byte AlphaFormat;         // AC_SRC_ALPHA = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
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

    // Monitor bounds (for dim overlay outside recording region)
    private readonly int _monX, _monY, _monW, _monH;

    // Border appearance
    private const int BorderThickness = 2;
    private const float DashLength = 8f;
    private const float GapLength = 6f;
    private const int AnimIntervalMs = 80; // dash march interval
    private const int DimAlpha = 100; // 0-255, ~40% dim outside recording region

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Create the overlay for the given recording region (screen coordinates).
    /// Monitor bounds are used to dim the area outside the recording region.
    /// </summary>
    public RecordingBorderOverlay(int left, int top, int width, int height,
        int monitorX, int monitorY, int monitorWidth, int monitorHeight)
    {
        _left = left;
        _top = top;
        _width = width;
        _height = height;
        _monX = monitorX;
        _monY = monitorY;
        _monW = monitorWidth;
        _monH = monitorHeight;
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
        // Window covers the full monitor (for dim overlay outside recording region)
        int x = _monX;
        int y = _monY;
        int w = _monW;
        int h = _monH;

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
        Log.Information("RecordingBorderOverlay: created at ({X},{Y} {W}x{H}), WDA_EXCLUDEFROMCAPTURE={Result}",
            x, y, w, h, excluded);

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "GDI+ render frame — inherently sequential Win32 resource lifecycle")]
    private void PaintFrame(float dashOffset)
    {
        if (_hwnd == IntPtr.Zero) return;

        // Window covers the full monitor
        int w = _monW;
        int h = _monH;

        // Recording region relative to the monitor origin
        int relLeft = _left - _monX;
        int relTop = _top - _monY;

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h,   // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0,    // BI_RGB
            },
            bmiColors = new uint[4],
        };

        IntPtr hBitmap = CreateDIBSection(memDc, ref bmi, 0, out IntPtr ppvBits, IntPtr.Zero, 0);
        IntPtr hOldBmp = SelectObject(memDc, hBitmap);

        try
        {
            using var bmp = new Bitmap(w, h, w * 4,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb, ppvBits);
            using var gfx = Graphics.FromImage(bmp);
            gfx.SmoothingMode = SmoothingMode.None;

            // Dim the entire monitor
            gfx.Clear(Color.FromArgb(DimAlpha, 0, 0, 0));

            // Clear the recording region (punch a transparent hole)
            gfx.SetClip(new RectangleF(relLeft, relTop, _width, _height));
            gfx.Clear(Color.Transparent);
            gfx.ResetClip();

            // Dashed white border at the recording region boundary
            using var pen = new Pen(Color.White, BorderThickness)
            {
                DashStyle = DashStyle.Custom,
                DashPattern = [DashLength, GapLength],
                DashOffset = dashOffset,
            };

            gfx.DrawRectangle(pen,
                relLeft - BorderThickness / 2f,
                relTop - BorderThickness / 2f,
                _width + BorderThickness,
                _height + BorderThickness);

            // Inner dark shadow (offset by half cycle) for visibility on light backgrounds
            pen.Color = Color.FromArgb(160, 0, 0, 0);
            pen.DashOffset = dashOffset + (DashLength + GapLength) / 2f;
            gfx.DrawRectangle(pen,
                relLeft + BorderThickness / 2f,
                relTop + BorderThickness / 2f,
                _width - BorderThickness,
                _height - BorderThickness);

            var blend = new BLENDFUNCTION
            {
                BlendOp = 0,   // AC_SRC_OVER
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = 1,   // AC_SRC_ALPHA
            };

            var ptDst = new POINT { x = _monX, y = _monY };
            var sz = new SIZE { cx = w, cy = h };
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
