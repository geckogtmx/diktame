using DiktaMe.Core.Audio;

namespace DiktaMe.Core.Tests.Audio;

/// <summary>
/// Tests for AudioDeviceManager — device enumeration and index resolution.
/// Note: GetInputDevices() calls the real WaveIn API and may return 0 devices
/// in a headless CI environment, which is expected.
/// </summary>
public class AudioDeviceManagerTests
{
    // ── GetInputDevices ───────────────────────────────────────────────────────

    [Fact]
    public void GetInputDevices_ReturnsNonNullList()
    {
        // Act
        IReadOnlyList<AudioDevice> devices = AudioDeviceManager.GetInputDevices();

        // Assert — list is never null; may be empty on headless CI
        Assert.NotNull(devices);
    }

    [Fact]
    public void GetInputDevices_AllDevicesHaveNonNullName()
    {
        // Act
        IReadOnlyList<AudioDevice> devices = AudioDeviceManager.GetInputDevices();

        // Assert
        foreach (AudioDevice device in devices)
            Assert.NotNull(device.Name);
    }

    [Fact]
    public void GetInputDevices_IndicesAreSequential()
    {
        // Act
        IReadOnlyList<AudioDevice> devices = AudioDeviceManager.GetInputDevices();

        // Assert — each device's Index matches its position in the list
        for (int i = 0; i < devices.Count; i++)
            Assert.Equal(i, devices[i].Index);
    }

    // ── ResolveDeviceIndex ────────────────────────────────────────────────────

    [Fact]
    public void ResolveDeviceIndex_NullArgs_ReturnsZero()
    {
        // Act
        int index = AudioDeviceManager.ResolveDeviceIndex(null, null);

        // Assert — always falls back to 0 (system default)
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveDeviceIndex_EmptyArgs_ReturnsZero()
    {
        // Act
        int index = AudioDeviceManager.ResolveDeviceIndex("", "");

        // Assert
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveDeviceIndex_UnknownLabel_FallsBackToZero()
    {
        // Arrange — a label that will never match any real device
        const string label = "ZZZ_NO_DEVICE_WILL_EVER_MATCH_THIS_ZZZ";

        // Act
        int index = AudioDeviceManager.ResolveDeviceIndex(deviceLabel: label);

        // Assert — graceful fallback
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveDeviceIndex_InvalidNumericId_FallsBackToZero()
    {
        // Arrange
        const string id = "not-a-number";

        // Act
        int index = AudioDeviceManager.ResolveDeviceIndex(deviceId: id);

        // Assert
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveDeviceIndex_NegativeNumericId_FallsBackToZero()
    {
        // Act
        int index = AudioDeviceManager.ResolveDeviceIndex(deviceId: "-1");

        // Assert
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveDeviceIndex_OutOfRangeNumericId_FallsBackToZero()
    {
        // Arrange — a device index far beyond what any machine would have
        const string id = "99999";

        // Act
        int index = AudioDeviceManager.ResolveDeviceIndex(deviceId: id);

        // Assert
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveDeviceIndex_ValidNumericId_WithinRange_ReturnsId()
    {
        // Only meaningful if there are at least 1 devices on this machine
        IReadOnlyList<AudioDevice> devices = AudioDeviceManager.GetInputDevices();
        if (devices.Count == 0)
            return; // Skip on headless CI with no audio devices

        // Act — resolve device 0 by numeric ID
        int index = AudioDeviceManager.ResolveDeviceIndex(deviceId: "0");

        // Assert
        Assert.Equal(0, index);
    }

    [Fact]
    public void ResolveDeviceIndex_LabelMatchIsCaseInsensitive()
    {
        // Only meaningful if there are devices to match against
        IReadOnlyList<AudioDevice> devices = AudioDeviceManager.GetInputDevices();
        if (devices.Count == 0)
            return;

        // Arrange — get first device name and uppercase it entirely
        string upperName = devices[0].Name.ToUpperInvariant();

        // Act
        int index = AudioDeviceManager.ResolveDeviceIndex(deviceLabel: upperName);

        // Assert — should still match device 0
        Assert.Equal(0, index);
    }
}
