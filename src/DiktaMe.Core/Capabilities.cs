using DiktaMe.Core.Resources;

namespace DiktaMe.Core;

/// <summary>
/// Runtime capability report — describes what features are available
/// based on the current system configuration (GPU, installed models, API keys, etc.).
/// </summary>
public sealed record CapabilityReport
{
    /// <summary>Whether a GPU with sufficient VRAM is available.</summary>
    public bool HasGpu { get; init; }

    /// <summary>GPU VRAM in MB, if detected.</summary>
    public int? GpuVramMb { get; init; }

    /// <summary>Whether local Whisper models are available.</summary>
    public bool HasLocalSTT { get; init; }

    /// <summary>Whether cloud STT API keys are configured.</summary>
    public bool HasCloudSTT { get; init; }

    /// <summary>Whether Ollama is running and has models pulled.</summary>
    public bool HasLocalLLM { get; init; }

    /// <summary>Whether cloud LLM API keys are configured.</summary>
    public bool HasCloudLLM { get; init; }

    /// <summary>Ollama version string, if detected.</summary>
    public string? OllamaVersion { get; init; }

    /// <summary>List of available Ollama model names.</summary>
    public IReadOnlyList<string> OllamaModels { get; init; } = [];

    /// <summary>Summary string for display in system tray tooltip.</summary>
    public string ToSummary()
    {
        var stt = HasCloudSTT ? CoreStrings.Capabilities_CloudStt : HasLocalSTT ? CoreStrings.Capabilities_LocalStt : CoreStrings.Capabilities_NoStt;
        var llm = HasCloudLLM ? CoreStrings.Capabilities_CloudLlm : HasLocalLLM ? CoreStrings.Capabilities_Ollama : CoreStrings.Capabilities_NoLlm;
        return $"{stt} + {llm}";
    }
}
