using Gitunia.Git.History.Models;

namespace Gitunia.Core.Diff;

/// <summary>The file lines picked from a shown diff, or why none could be.</summary>
/// <param name="FileIndex">The file of the diff the lines are in; -1 when refused.</param>
/// <param name="Range">The lines; meaningless when refused.</param>
/// <param name="Notice">A remark to show (refusal reason, or what was left out); empty when none.</param>
public sealed record DiffLineSelection(int FileIndex, LineRange Range, string Notice)
{
    /// <summary>True when there are lines to follow.</summary>
    public bool IsAccepted => FileIndex >= 0;

    /// <summary>A selection that holds no usable line.</summary>
    /// <param name="notice">Why.</param>
    public static DiffLineSelection Refused(string notice) => new(-1, default, notice);
}
