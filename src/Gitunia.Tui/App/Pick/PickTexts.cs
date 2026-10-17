using Gitunia.Core.Status;
using Gitunia.Git.Pick;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Pick;

/// <summary>The words of cherry-pick and revert. Pure, so every text is testable without a terminal.</summary>
public static class PickTexts
{
    public static string Verb(PickKind kind) => kind == PickKind.CherryPick ? "cherry-pick" : "revert";

    public static string PastTense(PickKind kind) => kind == PickKind.CherryPick ? "Cherry-picked" : "Reverted";

    /// <summary>Names what is acted on: the one commit with its subject, or the count.</summary>
    public static string Describe(IReadOnlyList<PickCommit> commits)
    {
        if (commits.Count != 1)
        {
            return OperationText.Plural(commits.Count, "commit");
        }

        var commit = commits[0];
        var shortHash = OperationTexts.ShortHash(commit.Hash);
        return commit.Subject.Length == 0 ? shortHash : $"{shortHash} {commit.Subject}";
    }

    public static string MainlinePrompt(PickKind kind, PickCommit merge)
    {
        var parents = merge.Parents.Select((parent, index) => $"[{index + 1}] {Parent(parent)}");
        return $"{Verb(kind)} merge commit {OperationTexts.ShortHash(merge.Hash)}: keep the mainline of " +
            $"{string.Join("  ", parents)}  (Esc cancels)";
    }

    public static OperationReport Report(PickResult result, PickKind kind, string target, string what)
    {
        var tip = OperationTexts.ShortHash(result.Head);
        var was = OperationTexts.ShortHash(result.OldHead);
        var verb = Verb(kind);
        return result.Outcome switch
        {
            PickOutcome.Completed => new OperationReport(
                $"{PastTense(kind)} {what} onto {target} ({tip}), was {was}; undo: git reset --hard {was} " +
                "(the reflog has it)",
                StatusSeverity.Info),
            PickOutcome.NothingToApply => new OperationReport(
                $"Nothing to {verb}: {target} already has the changes of {what}",
                StatusSeverity.Info),
            _ => new OperationReport(
                $"The {verb} of {what} stopped on conflicts: resolve the files marked !, then <space>gc to " +
                "continue, <space>ga to abort or <space>gk to skip the commit",
                StatusSeverity.Warning),
        };
    }

    public static string Failure(PickKind kind, string what, string target, string reason) =>
        $"Could not {Verb(kind)} {what} onto {target}: {reason}";

    private static string Parent(PickParent parent)
    {
        var shortHash = OperationTexts.ShortHash(parent.Hash);
        return parent.Subject.Length == 0 ? shortHash : $"{shortHash} {parent.Subject}";
    }
}
