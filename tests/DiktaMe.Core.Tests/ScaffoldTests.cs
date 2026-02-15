using DiktaMe.Core;

namespace DiktaMe.Core.Tests;

/// <summary>
/// Scaffold tests to verify the solution builds and basic types are accessible.
/// These will be replaced with real tests as features are implemented.
/// </summary>
public class ScaffoldTests
{
    [Fact]
    public void CapabilityReport_ToSummary_CloudBoth_ReturnsExpected()
    {
        // Arrange
        var report = new CapabilityReport
        {
            HasCloudSTT = true,
            HasCloudLLM = true,
        };

        // Act
        var summary = report.ToSummary();

        // Assert
        Assert.Equal("Cloud STT + Cloud LLM", summary);
    }

    [Fact]
    public void CapabilityReport_ToSummary_LocalBoth_ReturnsExpected()
    {
        // Arrange
        var report = new CapabilityReport
        {
            HasLocalSTT = true,
            HasLocalLLM = true,
        };

        // Act
        var summary = report.ToSummary();

        // Assert
        Assert.Equal("Local STT + Ollama", summary);
    }

    [Fact]
    public void CapabilityReport_ToSummary_NothingConfigured_ReturnsExpected()
    {
        // Arrange
        var report = new CapabilityReport();

        // Act
        var summary = report.ToSummary();

        // Assert
        Assert.Equal("No STT + No LLM", summary);
    }

    [Fact]
    public void TranscriptionResult_IsSuccess_WithText_ReturnsTrue()
    {
        // Arrange
        var result = new DiktaMe.Core.STT.TranscriptionResult
        {
            Text = "Hello world",
            Provider = "Test",
        };

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TranscriptionResult_IsSuccess_WithEmptyText_ReturnsFalse()
    {
        // Arrange
        var result = new DiktaMe.Core.STT.TranscriptionResult
        {
            Text = "",
            Provider = "Test",
        };

        // Assert
        Assert.False(result.IsSuccess);
    }
}
