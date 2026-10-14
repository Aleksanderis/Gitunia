namespace Gitunia.Git.Diff.Models;

/// <summary>One line of a hunk, without its prefix character and line terminator.</summary>
/// <param name="OldLineNumber">1-based line number on the old side; null for added lines.</param>
/// <param name="NewLineNumber">1-based line number on the new side; null for removed lines.</param>
/// <param name="NoNewlineAtEnd">The line is followed by <c>\ No newline at end of file</c>.</param>
public sealed record DiffLine(DiffLineKind Kind, string Text, int? OldLineNumber, int? NewLineNumber, bool NoNewlineAtEnd);
