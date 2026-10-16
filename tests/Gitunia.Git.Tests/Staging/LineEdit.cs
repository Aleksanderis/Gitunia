namespace Gitunia.Git.Tests.Staging;

/// <summary>A line of a generated test file that gets other text.</summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Text">The new text.</param>
public sealed record LineEdit(int Line, string Text);
