using Gitunia.Core.Editing;

namespace Gitunia.Core.Commits;

/// <summary>
/// The commit message box of one tab: the draft (message, options, amend bookkeeping) and what the box is doing. The draft
/// is never changed once it is in the state: <see cref="CommitReducer"/> clones it, changes the clone and stores that.
/// </summary>
/// <param name="Draft">The tab's draft, null while the tab never had one.</param>
/// <param name="IsInserting">The message box owns the Insert mode (typed keys go to its editor).</param>
/// <param name="PendingFailure">The output of a commit that failed while the tab was in the background, shown once the
/// tab is the active one (#364); null when none.</param>
public sealed record CommitState(CommitDraft? Draft, bool IsInserting, IReadOnlyList<string>? PendingFailure)
{
    /// <summary>No draft, nothing pending.</summary>
    public static CommitState Empty { get; } = new(null, false, null);

    /// <summary>The tab holds a message that was typed or edited and not committed.</summary>
    public bool HasUnsentText => Draft?.HasUnsentText == true;
}
