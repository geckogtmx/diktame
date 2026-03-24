using DiktaMe.Core.Config;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

public sealed class ApiCommandParserTests
{
    // ── Trigger commands ────────────────────────────────────────────────────

    [Fact]
    public void TryParse_TriggerDictate_ReturnsCorrectCommand()
    {
        var cmd = ApiCommandParser.TryParse("""{"action":"trigger","pipeline":"dictate"}""");

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("trigger");
        cmd.Pipeline.Should().Be("dictate");
        cmd.ModeId.Should().BeNull();
    }

    [Fact]
    public void TryParse_TriggerDictateWithModeId_ParsesModeId()
    {
        var cmd = ApiCommandParser.TryParse("""{"action":"trigger","pipeline":"dictate","modeId":"abc123def456"}""");

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("trigger");
        cmd.Pipeline.Should().Be("dictate");
        cmd.ModeId.Should().Be("abc123def456");
    }

    [Fact]
    public void TryParse_TriggerAsk_ReturnsCorrectCommand()
    {
        var cmd = ApiCommandParser.TryParse("""{"action":"trigger","pipeline":"ask"}""");

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("trigger");
        cmd.Pipeline.Should().Be("ask");
    }

    [Fact]
    public void TryParse_TriggerOops_ReturnsCorrectCommand()
    {
        var cmd = ApiCommandParser.TryParse("""{"action":"trigger","pipeline":"oops"}""");

        cmd.Should().NotBeNull();
        cmd!.Pipeline.Should().Be("oops");
    }

    [Theory]
    [InlineData("refine_auto")]
    [InlineData("refine_voice")]
    [InlineData("translate")]
    [InlineData("note")]
    [InlineData("read_selection")]
    public void TryParse_TriggerAllPipelineTypes_ParsesCorrectly(string pipelineType)
    {
        string json = $$"""{"action":"trigger","pipeline":"{{pipelineType}}"}""";

        var cmd = ApiCommandParser.TryParse(json);

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("trigger");
        cmd.Pipeline.Should().Be(pipelineType);
    }

    // ── Toggle commands ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("RawModeOverride")]
    [InlineData("StreamingEnabled")]
    [InlineData("AudioDucking")]
    [InlineData("Engine")]
    public void TryParse_ToggleSettings_ParsesCorrectly(string setting)
    {
        string json = $$"""{"action":"toggle","setting":"{{setting}}"}""";

        var cmd = ApiCommandParser.TryParse(json);

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("toggle");
        cmd.Setting.Should().Be(setting);
    }

    // ── Query commands ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("modes")]
    [InlineData("settings")]
    public void TryParse_QueryTargets_ParsesCorrectly(string target)
    {
        string json = $$"""{"action":"query","target":"{{target}}"}""";

        var cmd = ApiCommandParser.TryParse(json);

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("query");
        cmd.Target.Should().Be(target);
    }

    // ── Error cases ─────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_Null_ReturnsNull()
    {
        ApiCommandParser.TryParse(null).Should().BeNull();
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsNull()
    {
        ApiCommandParser.TryParse("").Should().BeNull();
    }

    [Fact]
    public void TryParse_Whitespace_ReturnsNull()
    {
        ApiCommandParser.TryParse("   ").Should().BeNull();
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsNull()
    {
        ApiCommandParser.TryParse("{not json at all").Should().BeNull();
    }

    [Fact]
    public void TryParse_MissingActionField_ReturnsNull()
    {
        ApiCommandParser.TryParse("""{"pipeline":"dictate"}""").Should().BeNull();
    }

    [Fact]
    public void TryParse_EmptyActionField_ReturnsNull()
    {
        ApiCommandParser.TryParse("""{"action":""}""").Should().BeNull();
    }

    [Fact]
    public void TryParse_ActionIsNumber_ReturnsNull()
    {
        ApiCommandParser.TryParse("""{"action":42}""").Should().BeNull();
    }

    [Fact]
    public void TryParse_UnknownAction_StillParses()
    {
        var cmd = ApiCommandParser.TryParse("""{"action":"unknown_future_action"}""");

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("unknown_future_action");
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_ExtraFields_Ignored()
    {
        var cmd = ApiCommandParser.TryParse("""{"action":"trigger","pipeline":"dictate","extra":"ignored","count":42}""");

        cmd.Should().NotBeNull();
        cmd!.Action.Should().Be("trigger");
        cmd.Pipeline.Should().Be("dictate");
    }

    [Fact]
    public void TryParse_NullModeId_TreatedAsAbsent()
    {
        var cmd = ApiCommandParser.TryParse("""{"action":"trigger","pipeline":"dictate","modeId":null}""");

        cmd.Should().NotBeNull();
        cmd!.ModeId.Should().BeNull();
    }
}
