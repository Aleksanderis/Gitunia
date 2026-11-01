using Gitunia.Core.Overlays;
using Gitunia.Core.Text;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Commits;

/// <summary>The texts the commit reducer shows.</summary>
public static class CommitTexts
{
    /// <summary>Shown when a key reaches the box while a commit runs.</summary>
    public const string RunningNotice = "A commit is running: Esc cancels it";

    /// <summary>Shown when Esc asks the running commit to stop.</summary>
    public const string CancellingNotice = "Cancelling the commit...";

    /// <summary>Shown when the commit was cancelled; git may have finished first.</summary>
    public const string CancelledNotice = "Commit cancelled: check the history, git may have finished first";

    /// <summary>Shown when Amend is switched on.</summary>
    public const string AmendOnNotice = "Amend on: the staged changes join the last commit";

    /// <summary>Shown when Amend is switched off.</summary>
    public const string AmendOffNotice = "Amend off";

    /// <summary>Shown when an undo has nothing to go back to.</summary>
    public const string NothingToUndoNotice = "Nothing to undo";

    /// <summary>Shown when HEAD's message could not be read for an amend.</summary>
    public const string HeadMessageFailure = "Could not read the last commit's message";

    private const string StageAllQuestion = "Nothing is staged. Stage all changes and commit?";

    /// <summary>The question asked when nothing is staged but unstaged changes exist.</summary>
    public static ConfirmPrompt StageAll { get; } = new(
        "Commit",
        [StageAllQuestion],
        "Untracked files are included; conflicted files are skipped. Unstage them again with <space>U.",
        "Stage all and commit");

    /// <summary>The status after a toggle of SignOff.</summary>
    /// <param name="isOn">The option is on.</param>
    public static string SignOff(bool isOn) => $"SignOff {OnOff(isOn)}";

    /// <summary>The status after a toggle of No-Verify.</summary>
    /// <param name="isOn">The option is on.</param>
    public static string NoVerify(bool isOn) =>
        $"No-Verify {OnOff(isOn)}: hooks {(isOn ? "are skipped" : "run")}";

    /// <summary>The status after a successful commit.</summary>
    /// <param name="request">What was committed.</param>
    /// <param name="result">The new commit.</param>
    /// <param name="forcePushUpstream">The upstream an amended commit was already on, null otherwise.</param>
    public static string Success(CommitRequest request, CommitResult result, string? forcePushUpstream)
    {
        var hash = ShortHash.Of(result.Hash);
        if (!request.Amend || result.PreviousHash is not { } previous)
        {
            return $"Committed {hash}";
        }

        var old = ShortHash.Of(previous);
        var warning = string.Empty;
        if (forcePushUpstream is not null)
        {
            warning = $"; it was on {forcePushUpstream}: pushing needs a force push";
        }

        return $"Amended to {hash}; previous commit {old}, `git reset --soft {old}` restores it{warning}";
    }

    private static string OnOff(bool isOn) => isOn ? "on" : "off";
}
