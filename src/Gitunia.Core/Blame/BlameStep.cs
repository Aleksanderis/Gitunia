namespace Gitunia.Core.Blame;

/// <summary>One earlier blame of the back stack: what to blame again when the user steps back.</summary>
/// <param name="Path">The file as it was named at that step.</param>
/// <param name="Revision">The revision blamed, null for the working tree file.</param>
/// <param name="Cursor">The highlighted line to restore.</param>
public sealed record BlameStep(string Path, string? Revision, int Cursor);
