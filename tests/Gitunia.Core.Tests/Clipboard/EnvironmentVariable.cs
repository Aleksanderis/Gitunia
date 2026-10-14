namespace Gitunia.Core.Tests.Clipboard;

/// <summary>An environment variable for tests that inject a lookup.</summary>
/// <param name="Name">The variable name.</param>
/// <param name="Value">The value.</param>
public sealed record EnvironmentVariable(string Name, string Value);
