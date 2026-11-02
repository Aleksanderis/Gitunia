using Gitunia.Git.History.Models;

namespace Gitunia.Tui.App.FileHistory;

/// <summary>The lines whose history an action opens, or why there are none.</summary>
/// <param name="Path">The repository-relative path of the file as the history starts from, null when refused.</param>
/// <param name="Revision">Where the history starts and whose line numbers the range uses (a commit or <c>HEAD</c>).</param>
/// <param name="Range">The lines.</param>
/// <param name="Notice">Why nothing opens, or what was left out of the range; empty when there is nothing to say.</param>
public sealed record LineHistoryTarget(string? Path, string Revision, LineRange Range, string Notice)
{
    /// <summary>True when there are lines to follow.</summary>
    public bool IsAccepted => Path is not null;

    /// <summary>No lines, and the reason.</summary>
    /// <param name="notice">Why.</param>
    public static LineHistoryTarget Refused(string notice) => new(null, string.Empty, default, notice);
}
