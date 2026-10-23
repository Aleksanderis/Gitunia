namespace Gitunia.Git.Conflicts.Models;

/// <summary>A conflicted path as the conflict editor needs it.</summary>
/// <param name="Kind">Text with hunks, or unsupported.</param>
/// <param name="Parts">The file as common text and hunks in file order; empty when unsupported.</param>
/// <param name="Reason">Why the path is unsupported, worded for the status bar; null for text.</param>
public sealed record ConflictReadResult(ConflictReadKind Kind, IReadOnlyList<ConflictPart> Parts, string? Reason)
{
    /// <summary>The number of conflict hunks.</summary>
    public int HunkCount => Parts.Count(part => part.Hunk is not null);

    /// <summary>A path the editor cannot take, with the reason.</summary>
    public static ConflictReadResult Unsupported(string reason) => new(ConflictReadKind.Unsupported, [], reason);

    /// <summary>A text file with hunks.</summary>
    public static ConflictReadResult OfText(IReadOnlyList<ConflictPart> parts) =>
        new(ConflictReadKind.Text, parts, null);
}
