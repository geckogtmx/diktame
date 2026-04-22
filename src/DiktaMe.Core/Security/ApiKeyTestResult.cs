namespace DiktaMe.Core.Security;

/// <summary>
/// Result of an <see cref="ApiKeyTester"/> whoami-style probe. Never thrown — the
/// tester wraps every failure path in this structure so UI can render inline.
/// </summary>
public sealed record ApiKeyTestResult(bool Ok, string Message, int LatencyMs);
