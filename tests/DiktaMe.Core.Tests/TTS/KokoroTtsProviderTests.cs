using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

[Trait("Category", "Unit")]
public sealed class KokoroTtsProviderTests
{
    // ── Provider properties ──────────────────────────────────────────────────

    [Fact]
    public void ProviderName_ReturnsExpected()
    {
        using var provider = new KokoroTtsProvider("int8");
        provider.ProviderName.Should().Be("Kokoro-ONNX (Local)");
    }

    [Fact]
    public void SupportsStreaming_ReturnsFalse()
    {
        using var provider = new KokoroTtsProvider("int8");
        provider.SupportsStreaming.Should().BeFalse();
    }

    [Fact]
    public void SampleRate_Is24000()
    {
        KokoroTtsProvider.SampleRate.Should().Be(24_000);
    }

    // ── Constructor — speed clamping ─────────────────────────────────────────

    [Fact]
    public void Ctor_InvalidVariant_Throws()
    {
        var act = () => new KokoroTtsProvider("invalid");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("int8")]
    [InlineData("fp16")]
    [InlineData("fp32")]
    public void Ctor_ValidVariants_DoNotThrow(string variant)
    {
        var act = () =>
        {
            using var _ = new KokoroTtsProvider(variant);
        };
        act.Should().NotThrow();
    }

    // ── SynthesizeAsync — edge cases (no model needed) ───────────────────────

    [Fact]
    public async Task Synthesize_EmptyText_ReturnsEmptyResult()
    {
        using var provider = new KokoroTtsProvider("int8");
        var result = await provider.SynthesizeAsync("");

        result.IsSuccess.Should().BeFalse();
        result.AudioData.Should().BeEmpty();
    }

    [Fact]
    public async Task Synthesize_WhitespaceText_ReturnsEmptyResult()
    {
        using var provider = new KokoroTtsProvider("int8");
        var result = await provider.SynthesizeAsync("   \t  \n  ");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_NullText_ReturnsEmptyResult()
    {
        using var provider = new KokoroTtsProvider("int8");
        var result = await provider.SynthesizeAsync(null!);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Synthesize_ModelNotDownloaded_ThrowsFileNotFound()
    {
        // Use fp32 variant (310MB) — virtually never downloaded in dev/CI environments
        using var provider = new KokoroTtsProvider("fp32");

        var act = () => provider.SynthesizeAsync("hello world");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    // ── IsAvailableAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task IsAvailable_ModelNotDownloaded_ReturnsFalse()
    {
        // Use fp32 variant (310MB) — virtually never downloaded in dev/CI environments
        using var provider = new KokoroTtsProvider("fp32");

        (await provider.IsAvailableAsync()).Should().BeFalse();
    }

    // ── ConvertToPcm16 ───────────────────────────────────────────────────────

    [Fact]
    public void ConvertToPcm16_EmptyArray_ReturnsEmpty()
    {
        var result = KokoroTtsProvider.ConvertToPcm16([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ConvertToPcm16_OutputLength_IsDoubleInputLength()
    {
        var input = new float[] { 0.5f, -0.5f, 0f };
        var result = KokoroTtsProvider.ConvertToPcm16(input);

        // 16-bit = 2 bytes per sample
        result.Length.Should().Be(input.Length * 2);
    }

    [Fact]
    public void ConvertToPcm16_Silence_ReturnsZeroBytes()
    {
        var input = new float[] { 0f, 0f, 0f };
        var result = KokoroTtsProvider.ConvertToPcm16(input);

        // All samples are 0 → all bytes should be 0
        result.Should().AllSatisfy(b => b.Should().Be(0));
    }

    [Fact]
    public void ConvertToPcm16_MaxPositive_ClampsToShortMax()
    {
        var input = new float[] { 1.0f };
        var result = KokoroTtsProvider.ConvertToPcm16(input);

        short value = BitConverter.ToInt16(result);
        value.Should().Be(short.MaxValue);
    }

    [Fact]
    public void ConvertToPcm16_MaxNegative_ClampsToShortMin()
    {
        var input = new float[] { -1.0f };
        var result = KokoroTtsProvider.ConvertToPcm16(input);

        short value = BitConverter.ToInt16(result);
        // -1.0 * 32767 = -32767 (not -32768 because we multiply by MaxValue)
        value.Should().Be((short)(-short.MaxValue));
    }

    [Fact]
    public void ConvertToPcm16_BeyondRange_ClampedToValid()
    {
        // Values beyond ±1.0 should be clamped
        var input = new float[] { 2.0f, -3.0f };
        var result = KokoroTtsProvider.ConvertToPcm16(input);

        short first = BitConverter.ToInt16(result, 0);
        short second = BitConverter.ToInt16(result, 2);

        first.Should().Be(short.MaxValue);
        second.Should().Be((short)(-short.MaxValue));
    }

    [Fact]
    public void ConvertToPcm16_HalfAmplitude_CorrectValue()
    {
        var input = new float[] { 0.5f };
        var result = KokoroTtsProvider.ConvertToPcm16(input);

        short value = BitConverter.ToInt16(result);
        // 0.5 * 32767 = 16383 (truncated from 16383.5)
        value.Should().Be((short)(0.5f * short.MaxValue));
    }

    // ── ModelManager ─────────────────────────────────────────────────────────

    [Fact]
    public void ModelManager_ReturnsNonNull()
    {
        using var provider = new KokoroTtsProvider("int8");
        provider.ModelManager.Should().NotBeNull();
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var provider = new KokoroTtsProvider("int8");
        var act = () =>
        {
            provider.Dispose();
            provider.Dispose();
        };
        act.Should().NotThrow();
    }
}
