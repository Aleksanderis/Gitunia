using Gitunia.Core.Editing;

namespace Gitunia.Core.Commits;

/// <summary>The result of moving a draft to another tab.</summary>
/// <param name="From">The box of the tab that is replaced.</param>
/// <param name="To">The box of the tab that replaces it.</param>
/// <param name="Moved">False when the replacement has an unsent message of its own, so nothing moved.</param>
public sealed record CommitDraftMoved(CommitState From, CommitState To, bool Moved);

/// <summary>Moves the draft of a tab that is about to be replaced (a located missing tab) to its replacement.</summary>
public static class CommitDraftMove
{
    /// <summary>
    /// Moves what a restart would keep (message, SignOff, No-Verify), never Amend, which belongs to the old repository's
    /// HEAD. Nothing moves when the replacement has an unsent message of its own, so the caller keeps both tabs.
    /// </summary>
    /// <param name="missing">The box of the tab that is replaced.</param>
    /// <param name="to">The box of the tab that replaces it.</param>
    public static CommitDraftMoved Move(CommitState missing, CommitState to)
    {
        if (missing.Draft is not { } draft)
        {
            return new CommitDraftMoved(missing, to, true);
        }

        var saved = CommitRules.SessionOf(draft);
        if (saved.IsEmpty)
        {
            return new CommitDraftMoved(missing, to, true);
        }

        if (saved.Message.Length > 0 && to.HasUnsentText)
        {
            return new CommitDraftMoved(missing, to, false);
        }

        var target = to.Draft?.Clone() ?? new CommitDraft();
        if (saved.Message.Length > 0)
        {
            target.Editor.SetText(saved.Message);
        }

        target.SignOff |= saved.SignOff;
        target.NoVerify |= saved.NoVerify;

        return new CommitDraftMoved(missing with { Draft = null }, to with { Draft = target }, true);
    }
}
