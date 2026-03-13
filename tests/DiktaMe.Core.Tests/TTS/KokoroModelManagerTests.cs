using DiktaMe.Core.TTS;
using FluentAssertions;

namespace DiktaMe.Core.Tests.TTS;

[Trait("Category", "Unit")]
public sealed class KokoroModelManagerTests
{
    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Ctor_InvalidVariant_Throws()
    {
        var act = () => new KokoroModelManager("unknown");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("variant");
    }

    [Theory]
    [InlineData("gpu")]
    [InlineData("int8")]
    [InlineData("fp16")]
    [InlineData("fp32")]
    public void Ctor_ValidVariant_DoesNotThrow(string variant)
    {
        var act = () => new KokoroModelManager(variant);
        act.Should().NotThrow();
    }

    // ── ModelPath ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("gpu", "kokoro-quant-gpu.onnx")]
    [InlineData("int8", "kokoro-quant-convinteger.onnx")]
    [InlineData("fp16", "kokoro-quant.onnx")]
    [InlineData("fp32", "kokoro.onnx")]
    public void ModelPath_ContainsCorrectFileName(string variant, string expectedFile)
    {
        var mgr = new KokoroModelManager(variant);
        mgr.ModelPath.Should().EndWith(expectedFile);
    }

    [Fact]
    public void ModelPath_IsInModelsDirectory()
    {
        var mgr = new KokoroModelManager("int8");
        mgr.ModelPath.Should().StartWith(KokoroModelManager.ModelsDirectory);
    }

    // ── ModelsDirectory ──────────────────────────────────────────────────────

    [Fact]
    public void ModelsDirectory_ContainsTtsSubfolder()
    {
        KokoroModelManager.ModelsDirectory.Should().Contain("tts");
    }

    [Fact]
    public void ModelsDirectory_ContainsDiktaMe()
    {
        KokoroModelManager.ModelsDirectory.Should().Contain("DiktaMe");
    }

    // ── IsModelDownloaded ────────────────────────────────────────────────────

    [Fact]
    public void IsModelDownloaded_WhenNotPresent_ReturnsFalse()
    {
        // Use fp32 variant (310MB) — virtually never downloaded in dev/CI environments
        var mgr = new KokoroModelManager("fp32");
        mgr.IsModelDownloaded.Should().BeFalse();
    }

    // ── GetApproxSizeMb ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("gpu", 169)]
    [InlineData("int8", 88)]
    [InlineData("fp16", 169)]
    [InlineData("fp32", 310)]
    public void GetApproxSizeMb_ValidVariant_ReturnsExpectedSize(string variant, long expectedMb)
    {
        KokoroModelManager.GetApproxSizeMb(variant).Should().Be(expectedMb);
    }

    [Fact]
    public void GetApproxSizeMb_UnknownVariant_ReturnsZero()
    {
        KokoroModelManager.GetApproxSizeMb("unknown").Should().Be(0);
    }
}
