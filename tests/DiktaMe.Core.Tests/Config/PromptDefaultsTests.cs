using DiktaMe.Core.Config;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Config;

public sealed class PromptDefaultsTests
{
    [Fact]
    public void Dictate_Prompt_IsNotEmpty()
    {
        PromptDefaults.Dictate.Should().NotBeNullOrWhiteSpace();
        PromptDefaults.Dictate.Should().Contain("cleanup");
    }

    [Fact]
    public void Refine_Prompt_IsNotEmpty()
    {
        PromptDefaults.Refine.Should().NotBeNullOrWhiteSpace();
        PromptDefaults.Refine.Should().Contain("grammar");
    }

    [Fact]
    public void Ask_Prompt_IsNotEmpty()
    {
        PromptDefaults.Ask.Should().NotBeNullOrWhiteSpace();
        PromptDefaults.Ask.Should().Contain("question");
    }

    [Fact]
    public void Translate_Prompt_IsNotEmpty()
    {
        PromptDefaults.Translate.Should().NotBeNullOrWhiteSpace();
        PromptDefaults.Translate.Should().Contain("ranslate"); // matches Translate or translate
    }

    [Fact]
    public void Note_Prompt_IsNotEmpty()
    {
        PromptDefaults.Note.Should().NotBeNullOrWhiteSpace();
        PromptDefaults.Note.Should().Contain("note");
    }

    [Fact]
    public void Chat_Prompt_IsNotEmpty()
    {
        PromptDefaults.Chat.Should().NotBeNullOrWhiteSpace();
        PromptDefaults.Chat.Should().Contain("assistant");
    }

    [Theory]
    [InlineData("dictate", nameof(PromptDefaults.Dictate))]
    [InlineData("refine", nameof(PromptDefaults.Refine))]
    [InlineData("ask", nameof(PromptDefaults.Ask))]
    [InlineData("translate", nameof(PromptDefaults.Translate))]
    [InlineData("note", nameof(PromptDefaults.Note))]
    [InlineData("chat", nameof(PromptDefaults.Chat))]
    public void GetDefault_KnownMode_ReturnsCorrectPrompt(string mode, string expectedConstantName)
    {
        var result = PromptDefaults.GetDefault(mode);

        result.Should().NotBeNullOrWhiteSpace();

        // Verify it matches the expected constant
        var expectedPrompt = expectedConstantName switch
        {
            nameof(PromptDefaults.Dictate) => PromptDefaults.Dictate,
            nameof(PromptDefaults.Refine) => PromptDefaults.Refine,
            nameof(PromptDefaults.Ask) => PromptDefaults.Ask,
            nameof(PromptDefaults.Translate) => PromptDefaults.Translate,
            nameof(PromptDefaults.Note) => PromptDefaults.Note,
            nameof(PromptDefaults.Chat) => PromptDefaults.Chat,
            _ => throw new ArgumentException($"Unknown constant: {expectedConstantName}", nameof(expectedConstantName)),
        };

        result.Should().Be(expectedPrompt);
    }

    [Theory]
    [InlineData("DICTATE")]
    [InlineData("Dictate")]
    [InlineData("DiCtAtE")]
    [InlineData("refine")]
    [InlineData("REFINE")]
    public void GetDefault_CaseInsensitive_ReturnsCorrectPrompt(string mode)
    {
        var result = PromptDefaults.GetDefault(mode);

        result.Should().NotBeNullOrWhiteSpace();
        // Should match the lowercase version
        result.Should().Be(PromptDefaults.GetDefault(mode.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("invalid_mode")]
    [InlineData("")]
    [InlineData("xyz")]
    public void GetDefault_UnknownMode_FallsBackToDictate(string mode)
    {
        var result = PromptDefaults.GetDefault(mode);

        result.Should().Be(PromptDefaults.Dictate);
    }
}
