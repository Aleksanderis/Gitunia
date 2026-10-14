namespace Gitunia.Git.Tests.Diff;

/// <summary>The selected lines of one hunk, for patch writer tests.</summary>
/// <param name="Hunk">Zero-based hunk index.</param>
/// <param name="Lines">Selected line indexes within the hunk.</param>
public sealed record HunkLineSelection(int Hunk, int[] Lines);
