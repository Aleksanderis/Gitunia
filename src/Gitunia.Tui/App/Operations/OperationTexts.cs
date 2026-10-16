using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Operations;

/// <summary>
/// The words of the in-progress operation actions (ux.md, workflow 4): the refusals, the two Confirm dialogs and the
/// status
/// line for each way a call can end. Pure, so every text is testable without a terminal.
/// </summary>
public static class OperationTexts
{
    private const int ShortHashLength = 7;

    public const string NoOperationNotice = "No merge, rebase, cherry-pick or revert is in progress";

    public static string StillConflicted(int count) =>
        $"{OperationText.Plural(count, "file")} still conflicted: resolve and mark them (<space>a) first";

    public static string CannotSkipMerge => "A merge cannot be skipped: continue or abort it";

    public static string ShortHash(string? hash) =>
        hash is null ? string.Empty : hash[..Math.Min(ShortHashLength, hash.Length)];

    public static ConfirmPrompt AbortPrompt(OperationInfo info, string? branch, string? oldTip)
    {
        var name = OperationBannerBuilder.NameOf(info.Kind);
        var where = branch is null ? string.Empty : $" on {branch}";
        var back = info.Kind == RepositoryOperation.Merge
            ? $"The working tree and index go back to {ShortHash(oldTip)}; the branch itself has not moved."
            : oldTip is null
                ? "Everything the operation changed is thrown away."
                : $"{branch ?? "HEAD"} goes back to {ShortHash(oldTip)}; that commit stays in the reflog.";
        return new ConfirmPrompt(
            $"Abort {name}",
            [
                $"Abort the {name}{where}?",
                "The work of the operation is undone and the conflict resolutions made so far are lost.",
            ],
            back,
            $"Abort the {name}");
    }

    public static ConfirmPrompt SkipPrompt(OperationInfo info)
    {
        var name = OperationBannerBuilder.NameOf(info.Kind);
        var commit = info.Commit is null ? "this commit" : ShortHash(info.Commit);
        var subject = info.Subject is null ? string.Empty : $" {info.Subject}";
        return new ConfirmPrompt(
            $"Skip commit in {name}",
            [
                $"Skip {commit}{subject}?",
                $"Its changes are dropped from the {name}; the {name} goes on with the next commit, if any.",
            ],
            "The commit itself is not deleted; resolutions made for it are lost.",
            "Skip the commit");
    }

    public static OperationReport ContinueReport(OperationInfo info, string? branch, OperationResult result)
    {
        var name = OperationBannerBuilder.NameOf(info.Kind);
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return StoppedAgain($"Continued the {name}", name);
        }

        return new OperationReport(Finished(info, branch, result.Head), StatusSeverity.Info);
    }

    public static OperationReport SkipReport(OperationInfo info, OperationResult result)
    {
        var name = OperationBannerBuilder.NameOf(info.Kind);
        var skipped = $"Skipped {(info.Commit is null ? "the commit" : ShortHash(info.Commit))}";
        return result.Outcome == OperationOutcome.Conflicted
            ? StoppedAgain(skipped, name)
            : new OperationReport($"{skipped}; the {name} is finished ({ShortHash(result.Head)})", StatusSeverity.Info);
    }

    public static OperationReport AbortReport(OperationInfo info, string? branch, OperationResult result)
    {
        var name = OperationBannerBuilder.NameOf(info.Kind);
        var at = branch is null ? ShortHash(result.Head) : $"{branch} at {ShortHash(result.Head)}";
        return new OperationReport($"Aborted the {name}; back to {at}", StatusSeverity.Info);
    }

    public static string Failure(string verb, OperationInfo info, string reason) =>
        $"Could not {verb} the {OperationBannerBuilder.NameOf(info.Kind)}: {reason}";

    private static OperationReport StoppedAgain(string done, string name) =>
        new(
            $"{done}; the {name} stopped on conflicts: resolve the files marked !, then <space>gc",
            StatusSeverity.Warning);

    private static string Finished(OperationInfo info, string? branch, string? head)
    {
        var tip = ShortHash(head);
        var into = branch is null ? string.Empty : $" into {branch}";
        return info.Kind switch
        {
            RepositoryOperation.Merge => $"Merged {info.Target ?? "the branch"}{into} ({tip})",
            RepositoryOperation.Rebase => $"Rebased {info.Target ?? branch ?? "the branch"} ({tip})",
            RepositoryOperation.CherryPick => $"Cherry-pick finished ({tip})",
            _ => $"Revert finished ({tip})",
        };
    }
}
