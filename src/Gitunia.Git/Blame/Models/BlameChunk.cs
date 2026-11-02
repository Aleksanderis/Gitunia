namespace Gitunia.Git.Blame.Models;

/// <summary>A run of consecutive lines of the file that come from one commit.</summary>
/// <param name="FirstLine">The index of the first line in the blamed file (zero-based).</param>
/// <param name="LineCount">How many lines the run has.</param>
/// <param name="OriginalFirstLine">The index of the first line in the commit's version of the file (zero-based).</param>
/// <param name="Commit">The commit the lines come from.</param>
public sealed record BlameChunk(int FirstLine, int LineCount, int OriginalFirstLine, BlameCommit Commit);
