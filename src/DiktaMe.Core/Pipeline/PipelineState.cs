namespace DiktaMe.Core.Pipeline;

/// <summary>
/// Lifecycle states shared across all processing pipelines.
/// Mirrors V1's <c>State</c> enum in models.py.
/// </summary>
public enum PipelineState
{
    Idle,
    Recording,
    Transcribing,
    Streaming,
    Processing,
    Injecting,
    Speaking,
    Error,
}
