using Xunit;

namespace DiktaMe.Core.Tests;

/// <summary>
/// Tests that use the real Win32 clipboard must not run in parallel with each other.
/// The system clipboard is a global shared resource, so concurrent reads/writes from
/// multiple tests cause race conditions and flaky failures.
/// Placing all clipboard-using test classes in this collection forces sequential execution.
/// </summary>
[CollectionDefinition("Clipboard", DisableParallelization = true)]
public sealed class ClipboardCollection { }
