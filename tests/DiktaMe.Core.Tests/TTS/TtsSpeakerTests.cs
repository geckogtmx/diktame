using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.TTS;
using FluentAssertions;
using Moq;

namespace DiktaMe.Core.Tests.TTS;

/// <summary>
/// Unit tests for <see cref="TtsSpeaker"/> — SPEC_003 Phase D.
/// Mocks ITTSProviderFactory and ITtsPlayerService to test the speaker in isolation.
/// Uses a real AudioDucker with ducking disabled (safe — no audio device needed).
/// </summary>
[Trait("Category", "Unit")]
public sealed class TtsSpeakerTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private readonly string _tempPath;
    private readonly SettingsManager _settings;
    private readonly Mock<ITTSProviderFactory> _factoryMock;
    private readonly Mock<ITtsPlayerService> _playerMock;
    private readonly Mock<ITTSProvider> _providerMock;
    private readonly AudioDucker _ducker;

    private static TtsResult OkTtsResult() => new(
        AudioData: new byte[100],
        Duration: TimeSpan.FromSeconds(2),
        SampleRate: 24_000,
        Provider: "MockTTS",
        LatencyMs: 50,
        Format: "pcm");

    private static TtsResult EmptyTtsResult() => new(
        AudioData: [],
        Duration: TimeSpan.Zero,
        SampleRate: 24_000,
        Provider: "MockTTS",
        LatencyMs: 0,
        Format: "pcm");

    public TtsSpeakerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"diktame_ttsspeaker_{Guid.NewGuid()}.json");
        _settings = new SettingsManager(_tempPath);

        _factoryMock = new Mock<ITTSProviderFactory>();
        _playerMock = new Mock<ITtsPlayerService>();
        _providerMock = new Mock<ITTSProvider>();

        _providerMock.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkTtsResult());

        _factoryMock.Setup(f => f.CreateProvider(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(_providerMock.Object);

        _playerMock.Setup(p => p.PlayAsync(
                It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _playerMock.SetupProperty(p => p.Volume, 0.8f);

        // AudioDucker is sealed — use real instance with ducking disabled (no audio device needed)
        _ducker = new AudioDucker(isEnabled: false);
    }

    private TtsSpeaker CreateSpeaker() =>
        new(_factoryMock.Object, _playerMock.Object, _settings, _ducker);

    private async Task EnableTtsAsync(
        bool enabled = true,
        bool speakAsk = false,
        bool speakChat = false,
        bool speakTranslate = false,
        bool speakNotification = false,
        string voiceId = "",
        int volumePercent = 80,
        bool duckDuringPlayback = false)
    {
        await _settings.UpdateAsync(_settings.Current with
        {
            Tts = new TtsSettings
            {
                Enabled = enabled,
                Provider = "kokoro",
                VoiceId = voiceId,
                VolumePercent = volumePercent,
                DuckDuringPlayback = duckDuringPlayback,
                SpeakAskResponses = speakAsk,
                SpeakChatResponses = speakChat,
                SpeakTranslations = speakTranslate,
                SpeakNotifications = speakNotification,
            },
        });
    }

    public void Dispose()
    {
        _ducker.Dispose();
        try { if (File.Exists(_tempPath)) File.Delete(_tempPath); }
        catch { /* best effort cleanup */ }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SpeakAsync — basic flow
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SpeakAsync_EmptyText_ReturnsZero()
    {
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakAsync("   ");

        ms.Should().Be(0);
        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SpeakAsync_ValidText_SynthesizesAndPlays()
    {
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakAsync("Hello world");

        ms.Should().BeGreaterThanOrEqualTo(0);
        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _playerMock.Verify(
            p => p.PlayAsync(It.IsAny<byte[]>(), 24_000, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_SetsVolumeFromSettings()
    {
        await EnableTtsAsync(volumePercent: 60);
        var speaker = CreateSpeaker();

        await speaker.SpeakAsync("test");

        _playerMock.VerifySet(p => p.Volume = 0.6f, Times.Once());
    }

    [Fact]
    public async Task SpeakAsync_PassesVoiceIdFromSettings()
    {
        await EnableTtsAsync(voiceId: "af_nicole");
        var speaker = CreateSpeaker();

        await speaker.SpeakAsync("test");

        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), "af_nicole", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_EmptyVoiceId_PassesNull()
    {
        await EnableTtsAsync(voiceId: "");
        var speaker = CreateSpeaker();

        await speaker.SpeakAsync("test");

        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SpeakAsync — failure paths
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SpeakAsync_SynthesisFails_ReturnsZero_NoPlay()
    {
        _providerMock.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyTtsResult());
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakAsync("test");

        ms.Should().Be(0);
        _playerMock.Verify(
            p => p.PlayAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SpeakAsync_SynthesisThrows_ReturnsZero_StopsPlayer()
    {
        _providerMock.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("model missing"));
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakAsync("test");

        ms.Should().Be(0);
        _playerMock.Verify(p => p.Stop(), Times.Once);
    }

    [Fact]
    public async Task SpeakAsync_Cancelled_ReturnsZero_StopsPlayer()
    {
        _providerMock.Setup(p => p.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakAsync("test");

        ms.Should().Be(0);
        _playerMock.Verify(p => p.Stop(), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SpeakAsync — ducking
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SpeakAsync_DuckingEnabled_SetsDuckerProperties()
    {
        await EnableTtsAsync(duckDuringPlayback: true);
        await _settings.UpdateAsync(_settings.Current with
        {
            AudioDucking = _settings.Current.AudioDucking with { Enabled = true },
        });
        var speaker = CreateSpeaker();

        await speaker.SpeakAsync("test");

        // AudioDucker is sealed so we can't verify Duck() calls via Moq.
        // But we can verify the ducker was configured correctly.
        _ducker.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SpeakAsync_DuckingDisabled_DuckerNotEnabled()
    {
        await EnableTtsAsync(duckDuringPlayback: false);
        var speaker = CreateSpeaker();

        await speaker.SpeakAsync("test");

        // Ducker should remain disabled when DuckDuringPlayback is false
        _ducker.IsEnabled.Should().BeFalse();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SpeakIfEnabledAsync — toggle checks
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SpeakIfEnabledAsync_MasterDisabled_NoSynthesis()
    {
        await EnableTtsAsync(enabled: false, speakAsk: true);
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakIfEnabledAsync("test", "ask");

        ms.Should().Be(0);
        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SpeakIfEnabledAsync_ModeDisabled_NoSynthesis()
    {
        await EnableTtsAsync(enabled: true, speakAsk: false);
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakIfEnabledAsync("test", "ask");

        ms.Should().Be(0);
        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SpeakIfEnabledAsync_AskEnabled_Speaks()
    {
        await EnableTtsAsync(enabled: true, speakAsk: true);
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakIfEnabledAsync("Hello", "ask");

        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakIfEnabledAsync_ChatEnabled_Speaks()
    {
        await EnableTtsAsync(enabled: true, speakChat: true);
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakIfEnabledAsync("Hello", "chat");

        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakIfEnabledAsync_TranslateEnabled_Speaks()
    {
        await EnableTtsAsync(enabled: true, speakTranslate: true);
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakIfEnabledAsync("Hola mundo", "translate");

        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SpeakIfEnabledAsync_UnknownMode_NoSynthesis()
    {
        await EnableTtsAsync(enabled: true);
        var speaker = CreateSpeaker();

        var ms = await speaker.SpeakIfEnabledAsync("test", "unknown_mode");

        ms.Should().Be(0);
        _providerMock.Verify(
            p => p.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
