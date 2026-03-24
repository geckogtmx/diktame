using System.Runtime.InteropServices;
using Serilog;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DiktaMe.Core.Vision;

/// <summary>
/// Captures screenshots of the active window, a rectangular region, or the
/// full virtual screen using Win32 GDI and returns PNG-encoded byte arrays.
/// </summary>
public static class ScreenCapture
{
    /// <summary>Captures the currently focused window as a PNG byte array.</summary>
    public static byte[] CaptureActiveWindow()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("No foreground window found.");
        }

        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
        {
            throw new InvalidOperationException("GetWindowRect failed.");
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Invalid window dimensions: {width}x{height}.");
        }

        return CaptureWithPrintWindow(hwnd, width, height);
    }

    /// <summary>Captures a rectangular region of the virtual screen as a PNG byte array.</summary>
    public static byte[] CaptureRegion(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        return CaptureFromScreen(x, y, width, height);
    }

    /// <summary>Captures the entire virtual screen as a PNG byte array.</summary>
    public static byte[] CaptureFullScreen()
    {
        int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Invalid virtual screen dimensions: {width}x{height}.");
        }

        return CaptureFromScreen(x, y, width, height);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Uses <c>PrintWindow</c> with <c>PW_RENDERFULLCONTENT</c> to capture
    /// the target window, including DWM-composed / layered content.
    /// </summary>
    private static byte[] CaptureWithPrintWindow(IntPtr hwnd, int width, int height)
    {
        IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        IntPtr hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, width, height);
        IntPtr hOld = NativeMethods.SelectObject(hdcMem, hBitmap);

        try
        {
            // PW_RENDERFULLCONTENT = 0x02 captures layered/DWM windows
            if (!NativeMethods.PrintWindow(hwnd, hdcMem, NativeMethods.PW_RENDERFULLCONTENT))
            {
                Log.Warning("ScreenCapture: PrintWindow returned false, falling back to BitBlt");
                NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect);
                IntPtr hdcWin = NativeMethods.GetDC(hwnd);
                try
                {
                    NativeMethods.BitBlt(hdcMem, 0, 0, width, height, hdcWin, 0, 0, NativeMethods.SRCCOPY);
                }
                finally
                {
                    NativeMethods.ReleaseDC(hwnd, hdcWin);
                }
            }

            return HBitmapToPng(hBitmap, width, height);
        }
        finally
        {
            NativeMethods.SelectObject(hdcMem, hOld);
            NativeMethods.DeleteObject(hBitmap);
            NativeMethods.DeleteDC(hdcMem);
            NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    /// <summary>
    /// Uses <c>BitBlt</c> with <c>SRCCOPY</c> to capture a region from the
    /// virtual screen device context.
    /// </summary>
    private static byte[] CaptureFromScreen(int x, int y, int width, int height)
    {
        IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        IntPtr hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, width, height);
        IntPtr hOld = NativeMethods.SelectObject(hdcMem, hBitmap);

        try
        {
            NativeMethods.BitBlt(hdcMem, 0, 0, width, height, hdcScreen, x, y, NativeMethods.SRCCOPY);
            return HBitmapToPng(hBitmap, width, height);
        }
        finally
        {
            NativeMethods.SelectObject(hdcMem, hOld);
            NativeMethods.DeleteObject(hBitmap);
            NativeMethods.DeleteDC(hdcMem);
            NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    /// <summary>
    /// Converts a GDI HBITMAP to a PNG-encoded byte array using WinRT
    /// <see cref="BitmapEncoder"/>.
    /// </summary>
    private static byte[] HBitmapToPng(IntPtr hBitmap, int width, int height)
    {
        // Read raw BGRA pixel data via GetDIBits
        var bmi = new NativeMethods.BITMAPINFO
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            },
        };

        byte[] pixels = new byte[width * height * 4];
        IntPtr hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        try
        {
            NativeMethods.GetDIBits(hdcScreen, hBitmap, 0, (uint)height, pixels, ref bmi, 0);
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
        }

        // Encode as PNG via WinRT BitmapEncoder
        return EncodePngAsync(pixels, (uint)width, (uint)height).GetAwaiter().GetResult();
    }

    private static async Task<byte[]> EncodePngAsync(byte[] pixels, uint width, uint height)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            width,
            height,
            dpiX: 96.0,
            dpiY: 96.0,
            pixels);
        await encoder.FlushAsync();

        stream.Seek(0);
        byte[] result = new byte[stream.Size];
        var reader = new DataReader(stream);
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(result);
        return result;
    }

    // ── P/Invoke ─────────────────────────────────────────────────────────────

    private static class NativeMethods
    {
        internal const uint SRCCOPY = 0x00CC0020;
        internal const uint PW_RENDERFULLCONTENT = 0x00000002;
        internal const int SM_XVIRTUALSCREEN = 76;
        internal const int SM_YVIRTUALSCREEN = 77;
        internal const int SM_CXVIRTUALSCREEN = 78;
        internal const int SM_CYVIRTUALSCREEN = 79;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
            IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        internal static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);
    }
}
