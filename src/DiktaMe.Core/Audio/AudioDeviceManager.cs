
using NAudio.Wave;

namespace DiktaMe.Core.Audio;
/// <summary>
/// Enumerates available audio input (microphone) devices and resolves
/// a device index from an optional label or ID, with fallback to default.
/// </summary>
public static class AudioDeviceManager
{
    /// <summary>
    /// Returns all available audio input devices.
    /// </summary>
    public static IReadOnlyList<AudioDevice> GetInputDevices()
    {
        var devices = new List<AudioDevice>();
        int count = WaveIn.DeviceCount;

        for (int i = 0; i < count; i++)
        {
            WaveInCapabilities caps = WaveIn.GetCapabilities(i);
            devices.Add(new AudioDevice
            {
                Index = i,
                Name = caps.ProductName,
                Channels = caps.Channels,
            });
        }

        return devices;
    }

    /// <summary>
    /// Resolves the WaveIn device index to use for recording.
    /// Matches by label (case-insensitive substring) first, then by numeric ID.
    /// Falls back to 0 (system default) if no match found.
    /// </summary>
    /// <param name="deviceLabel">Optional label/name substring to match.</param>
    /// <param name="deviceId">Optional numeric device ID string.</param>
    /// <returns>The resolved WaveIn device index.</returns>
    public static int ResolveDeviceIndex(string? deviceLabel = null, string? deviceId = null)
    {
        IReadOnlyList<AudioDevice> devices = GetInputDevices();

        // Prefer label match (fuzzy, case-insensitive substring)
        if (!string.IsNullOrWhiteSpace(deviceLabel))
        {
            foreach (AudioDevice device in devices)
            {
                if (device.Name.Contains(deviceLabel, StringComparison.OrdinalIgnoreCase))
                {
                    return device.Index;
                }
            }
        }

        // Fall back to numeric ID
        if (!string.IsNullOrWhiteSpace(deviceId) &&
            int.TryParse(deviceId, System.Globalization.CultureInfo.InvariantCulture, out int parsedId) &&
            parsedId >= 0 && parsedId < devices.Count)
        {
            return parsedId;
        }

        // Default: device 0 (system default microphone)
        return 0;
    }
}

/// <summary>
/// Describes a single audio input device.
/// </summary>
public sealed record AudioDevice
{
    /// <summary>WaveIn device index (0-based).</summary>
    public required int Index { get; init; }

    /// <summary>Display name reported by the driver.</summary>
    public required string Name { get; init; }

    /// <summary>Maximum supported input channels.</summary>
    public required int Channels { get; init; }
}
