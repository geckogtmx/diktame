using System.IO.Compression;
using System.Runtime.InteropServices;
using Serilog;

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
    /// Converts a GDI HBITMAP to a PNG-encoded byte array using a pure
    /// synchronous PNG encoder (no WinRT async — avoids UI thread deadlock).
    /// </summary>
    private static byte[] HBitmapToPng(IntPtr hBitmap, int width, int height)
    {
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

        // Convert BGRA → RGB for PNG (drop alpha, swap channels)
        byte[] rgb = new byte[width * height * 3];
        for (int i = 0, j = 0; i < pixels.Length; i += 4, j += 3)
        {
            rgb[j] = pixels[i + 2];     // R (was at offset 2 in BGRA)
            rgb[j + 1] = pixels[i + 1]; // G
            rgb[j + 2] = pixels[i];     // B (was at offset 0 in BGRA)
        }

        return EncodePngSync(rgb, width, height);
    }

    /// <summary>
    /// Minimal synchronous PNG encoder. Writes a valid PNG with a single
    /// uncompressed (deflate-stored) IDAT chunk. No async, no WinRT.
    /// </summary>
    private static byte[] EncodePngSync(byte[] rgb, int width, int height)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // PNG signature
        bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // IHDR chunk
        WriteChunk(bw, "IHDR"u8, writer =>
        {
            writer.WriteBigEndian(width);
            writer.WriteBigEndian(height);
            writer.Write((byte)8);  // bit depth
            writer.Write((byte)2);  // color type: RGB
            writer.Write((byte)0);  // compression
            writer.Write((byte)0);  // filter
            writer.Write((byte)0);  // interlace
        });

        // IDAT chunk — deflate-compressed scanlines with filter byte 0 (None) per row
        WriteChunk(bw, "IDAT"u8, writer =>
        {
            using var deflateStream = new MemoryStream();
            using (var compressor = new DeflateStream(deflateStream, CompressionLevel.Fastest, leaveOpen: true))
            {
                // zlib header (CMF=0x78, FLG=0x01 for fastest compression)
                // We write raw deflate and prepend zlib wrapper manually
                int stride = width * 3;
                for (int y = 0; y < height; y++)
                {
                    compressor.WriteByte(0); // filter: None
                    compressor.Write(rgb, y * stride, stride);
                }
            }

            // Write zlib wrapper: header + deflate data + Adler-32
            byte[] compressed = deflateStream.ToArray();
            writer.Write((byte)0x78); // CMF
            writer.Write((byte)0x01); // FLG
            writer.Write(compressed);
            writer.WriteBigEndian((int)ComputeAdler32(rgb, width, height));
        });

        // IEND chunk
        WriteChunk(bw, "IEND"u8, _ => { });

        return ms.ToArray();
    }

    private static void WriteChunk(BinaryWriter bw, ReadOnlySpan<byte> type, Action<PngChunkWriter> writeData)
    {
        using var dataMs = new MemoryStream();
        using var dataWriter = new BinaryWriter(dataMs);
        var chunkWriter = new PngChunkWriter(dataWriter);
        writeData(chunkWriter);

        byte[] data = dataMs.ToArray();
        byte[] typeBytes = type.ToArray();

        WriteBigEndianInt(bw, data.Length);
        bw.Write(typeBytes);
        bw.Write(data);

        // CRC32 over type + data
        uint crc = Crc32(typeBytes, data);
        WriteBigEndianInt(bw, (int)crc);
    }

    private static uint ComputeAdler32(byte[] rgb, int width, int height)
    {
        uint a = 1, b = 0;
        int stride = width * 3;
        for (int y = 0; y < height; y++)
        {
            // Filter byte (0)
            a = (a + 0) % 65521;
            b = (b + a) % 65521;
            // Row data
            for (int x = 0; x < stride; x++)
            {
                a = (a + rgb[(y * stride) + x]) % 65521;
                b = (b + a) % 65521;
            }
        }

        return (b << 16) | a;
    }

    private static readonly uint[] Crc32Table = InitCrc32Table();

    private static uint[] InitCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }

        return table;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in type) crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data) crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    /// <summary>Helper for writing big-endian integers in PNG chunks.</summary>
    private readonly struct PngChunkWriter(BinaryWriter writer)
    {
        public void Write(byte value) => writer.Write(value);
        public void Write(byte[] data) => writer.Write(data);
        public void Write(byte[] data, int offset, int count) => writer.Write(data, offset, count);
        public void WriteByte(byte value) => writer.Write(value);
        public void WriteBigEndian(int value) => WriteBigEndianInt(writer, value);
    }

    private static void WriteBigEndianInt(BinaryWriter bw, int value)
    {
        bw.Write((byte)((value >> 24) & 0xFF));
        bw.Write((byte)((value >> 16) & 0xFF));
        bw.Write((byte)((value >> 8) & 0xFF));
        bw.Write((byte)(value & 0xFF));
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
