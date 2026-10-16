namespace Gitunia.Git.Diff.Models;

/// <summary>
/// An added or removed line the user picked in a diff shown earlier. It is identified by what it says and where it is in the file
/// (not by its position in a hunk), so it can be found again in a diff read with other context lines.
/// </summary>
/// <param name="Kind">Added or removed (a context line is never a change).</param>
/// <param name="Number">The line number on the new side for an added line, on the old side for a removed one.</param>
/// <param name="Text">The text without prefix.</param>
public sealed record ChangedLine(DiffLineKind Kind, int Number, string Text);
