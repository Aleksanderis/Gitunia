using Gitunia.Core.Rebase;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Pick;

namespace Gitunia.Core.Pick;

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
            return RebaseCounts.Plural(commits.Count, "commit");
        }

        var commit = commits[0];
        var shortHash = ShortHash.Of(commit.Hash);
        return commit.Subject.Length == 0 ? shortHash : $"{shortHash} {commit.Subject}";
    }

    public static string MainlinePrompt(PickKind kind, PickCommit merge)
    {
        var parents = merge.Parents.Select((parent, index) => $"[{index + 1}] {Parent(parent)}");
        return $"{Verb(kind)} merge commit {ShortHash.Of(merge.Hash)}: keep the mainline of " +
            $"{string.Join("  ", parents)}  (Esc cancels)";
    }

    /// <summary>The title of the mainline overlay.</summary>
    public static string MainlineTitle(PickKind kind, PickCommit merge) =>
        $"{Verb(kind)} merge commit {ShortHash.Of(merge.Hash)}: keep the mainline of";

    /// <summary>The rows of the mainline overlay, one per parent of the merge commit.</summary>
    public static IReadOnlyList<string> MainlineRows(PickCommit merge) => merge.Parents.Select(Parent).ToList();

    public static PostedText Report(PickResult result, PickKind kind, string target, string what)
    {
        var tip = ShortHash.Of(result.Head);
        var was = ShortHash.Of(result.OldHead);
        var verb = Verb(kind);
        return result.Outcome switch
        {
            PickOutcome.Completed => new PostedText(
                $"{PastTense(kind)} {what} onto {target} ({tip}), was {was}; undo: git reset --hard {was} " +
                "(the reflog has it)",
                StatusSeverity.Info),
            PickOutcome.NothingToApply => new PostedText(
                $"Nothing to {verb}: {target} already has the changes of {what}",
                StatusSeverity.Info),
            _ => new PostedText(
                $"The {verb} of {what} stopped on conflicts: resolve the files marked !, then <space>gc to " +
                "continue, <space>ga to abort or <space>gs to skip the commit",
                StatusSeverity.Warning),
        };
    }

    public static string Failure(PickKind kind, string what, string target, string reason) =>
        $"Could not {Verb(kind)} {what} onto {target}: {reason}";

    private static string Parent(PickParent parent)
    {
        var shortHash = ShortHash.Of(parent.Hash);
        return parent.Subject.Length == 0 ? shortHash : $"{shortHash} {parent.Subject}";
    }
}
