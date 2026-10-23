namespace Gitunia.Git.Conflicts.Models;

/// <summary>
/// A piece of a conflicted file in file order: text both sides agree on, or a conflict hunk. Exactly one of
/// <see cref="Common"/> and <see cref="Hunk"/> is set.
/// </summary>
/// <param name="Common">The unconflicted text, null for a hunk.</param>
/// <param name="Hunk">The conflicted region, null for common text.</param>
public sealed record ConflictPart(string? Common, ConflictHunk? Hunk)
{
    /// <summary>A piece of text outside any conflict.</summary>
    public static ConflictPart OfCommon(string text) => new(text, null);

    /// <summary>A conflicted region.</summary>
    public static ConflictPart OfHunk(ConflictHunk hunk) => new(null, hunk);
}
