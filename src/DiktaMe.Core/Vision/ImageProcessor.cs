using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DiktaMe.Core.Vision;

/// <summary>
/// Static utilities for resizing and compressing screenshots before
/// sending them to a vision-capable LLM API.
/// </summary>
public static class ImageProcessor
{
    /// <summary>
    /// Down-scales the image so its longest side does not exceed
    /// <paramref name="maxDimension"/> pixels. Returns the original
    /// bytes unchanged when no resize is needed.
    /// </summary>
    public static byte[] ResizeIfNeeded(byte[] pngData, int maxDimension)
    {
        ArgumentNullException.ThrowIfNull(pngData);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDimension);

        return ResizeIfNeededAsync(pngData, maxDimension).GetAwaiter().GetResult();
    }

    /// <summary>
    /// If the PNG data exceeds <paramref name="maxSizeBytes"/>, re-encodes
    /// as JPEG at quality 85. Returns the data and the corresponding MIME type.
    /// </summary>
    public static (byte[] Data, string MimeType) CompressToJpegIfNeeded(byte[] pngData, int maxSizeBytes = 1_048_576)
    {
        ArgumentNullException.ThrowIfNull(pngData);

        if (pngData.Length <= maxSizeBytes)
        {
            return (pngData, "image/png");
        }

        byte[] jpeg = ConvertToJpegAsync(pngData, 0.85).GetAwaiter().GetResult();
        return (jpeg, "image/jpeg");
    }

    /// <summary>
    /// Convenience method: resize then compress. Returns image data ready
    /// for API upload together with its MIME type.
    /// </summary>
    public static (byte[] Data, string MimeType) PrepareForApi(byte[] rawScreenshot, int maxDimension = 2048)
    {
        byte[] resized = ResizeIfNeeded(rawScreenshot, maxDimension);
        return CompressToJpegIfNeeded(resized);
    }

    // ── Private async helpers ────────────────────────────────────────────────

    private static async Task<byte[]> ResizeIfNeededAsync(byte[] pngData, int maxDimension)
    {
        using var inputStream = new InMemoryRandomAccessStream();
        await inputStream.WriteAsync(pngData.AsBuffer());
        inputStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(inputStream);
        uint origW = decoder.PixelWidth;
        uint origH = decoder.PixelHeight;
        uint longest = Math.Max(origW, origH);

        if (longest <= (uint)maxDimension)
        {
            return pngData;
        }

        double scale = (double)maxDimension / longest;
        uint newW = (uint)(origW * scale);
        uint newH = (uint)(origH * scale);

        using var outputStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);
        encoder.BitmapTransform.ScaledWidth = newW;
        encoder.BitmapTransform.ScaledHeight = newH;
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();

        outputStream.Seek(0);
        byte[] result = new byte[outputStream.Size];
        var reader = new DataReader(outputStream);
        await reader.LoadAsync((uint)outputStream.Size);
        reader.ReadBytes(result);
        return result;
    }

    private static async Task<byte[]> ConvertToJpegAsync(byte[] pngData, double quality)
    {
        using var inputStream = new InMemoryRandomAccessStream();
        await inputStream.WriteAsync(pngData.AsBuffer());
        inputStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(inputStream);
        var pixelData = await decoder.GetPixelDataAsync();
        byte[] pixels = pixelData.DetachPixelData();

        using var outputStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);

        var properties = new BitmapPropertySet
        {
            { "ImageQuality", new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single) },
        };

        // Re-create encoder with quality property
        outputStream.Size = 0;
        encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream, properties);
        encoder.SetPixelData(
            decoder.BitmapPixelFormat,
            decoder.BitmapAlphaMode,
            decoder.PixelWidth,
            decoder.PixelHeight,
            dpiX: decoder.DpiX,
            dpiY: decoder.DpiY,
            pixels);
        await encoder.FlushAsync();

        outputStream.Seek(0);
        byte[] result = new byte[outputStream.Size];
        var reader = new DataReader(outputStream);
        await reader.LoadAsync((uint)outputStream.Size);
        reader.ReadBytes(result);
        return result;
    }
}
