
using Xunit;

namespace DiktaMe.Core.Tests;
/// <summary>
/// Tests that write to the real %APPDATA%\DiktaMe\settings.json must not run in
/// parallel with each other — SettingsManager uses an atomic write-then-rename that
/// can cause IOException when concurrent tests try to move the .tmp file simultaneously.
/// Placing all such test classes in this collection forces sequential execution.
/// </summary>
[CollectionDefinition(nameof(SettingsWriterCollection), DisableParallelization = true)]
public sealed class SettingsWriterCollection { }
