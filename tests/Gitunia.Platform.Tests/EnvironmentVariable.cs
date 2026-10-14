namespace Gitunia.Platform.Tests;

/// <summary>An environment variable for tests that inject a lookup.</summary>
/// <param name="Key">The variable name.</param>
/// <param name="Value">The value.</param>
public sealed record EnvironmentVariable(string Key, string Value);
