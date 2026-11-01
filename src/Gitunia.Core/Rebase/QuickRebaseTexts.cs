using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Rebase;

/// <summary>The words of fixup, reword and drop of one commit. Pure, so every text is testable.</summary>
public static class QuickRebaseTexts
{
    /// <summary>The confirm dialog: the commit, the replayed commits, the force-push warning and the undo.</summary>
    public static ConfirmPrompt Confirm(QuickRebaseTarget target, GitRef? current, string? autoStashNote)
    {
        var plan = target.Plan;
        var branch = target.BranchName;
        var (title, first, yes) = plan.Kind switch
        {
            QuickRebaseKind.Fixup => (
                "Fix up commit",
                $"Fix up {Quoted(plan)} into {QuotedCommit(plan.IntoCommit!.Hash, plan.IntoCommit.Subject)}? " +
                "Its changes join that commit, which keeps its message; the fixed-up commit disappears.",
                "Fix up"),
            QuickRebaseKind.Reword => (
                "Reword commit",
                $"Reword {Quoted(plan)} to \"{target.NewSubject}\"? The rest of its message is kept.",
                "Reword"),
            _ => (
                "Drop commit",
                $"Drop {Quoted(plan)} from {branch}? Its changes disappear from the branch.",
                "Drop"),
        };
        List<string> lines = [first, ReplayNote(plan, branch)];
        if (RebaseGuard.UpstreamWarning(current) is { } warning)
        {
            lines.Add(warning);
        }

        if (autoStashNote is not null)
        {
            lines.Add(autoStashNote);
        }

        return new ConfirmPrompt(
            title,
            lines,
            $"Undo: git reset --keep {plan.Head} (also ORIG_HEAD, the reflog). A stop on conflicts leaves a " +
            "rebase in progress: <space>gc continues, <space>ga aborts.",
            yes);
    }

    /// <summary>How the quick rebase ended.</summary>
    public static PostedText Report(QuickRebaseTarget target, InteractiveRebaseResult result)
    {
        var plan = target.Plan;
        var (done, noun) = plan.Kind switch
        {
            QuickRebaseKind.Fixup => ("Fixed up", "Fixup"),
            QuickRebaseKind.Reword => ("Reworded", "Reword"),
            _ => ("Dropped", "Drop"),
        };
        if (result.Outcome == OperationOutcome.Conflicted)
        {
            return new PostedText(
                $"{noun} of {Quoted(plan)} stopped on conflicts: resolve the files marked !, then " +
                $"<space>gc (continue), <space>ga (abort, back to {ShortHash.Of(result.OriginalHead)})",
                StatusSeverity.Warning);
        }

        return new PostedText(
            $"{done} {Quoted(plan)} on {target.BranchName} ({ShortHash.Of(result.Head)}), " +
            $"was {ShortHash.Of(result.OriginalHead)}; undo: git reset --keep {result.OriginalHead} " +
            "(or ORIG_HEAD, the reflog)",
            StatusSeverity.Info);
    }

    /// <summary>The verb of the kind in a sentence: "fix up", "reword", "drop".</summary>
    public static string Verb(QuickRebaseKind kind) => kind switch
    {
        QuickRebaseKind.Fixup => "fix up",
        QuickRebaseKind.Reword => "reword",
        _ => "drop",
    };

    /// <summary>The failure line of a quick rebase that did not run.</summary>
    public static string Failure(QuickRebasePlan plan, string reason) =>
        $"Could not {Verb(plan.Kind)} {Quoted(plan)}: {reason}";

    /// <summary>The failure line of a plan that could not be read.</summary>
    public static string PlanFailure(QuickRebaseKind kind, string hash, string reason) =>
        $"Could not {Verb(kind)} {ShortHash.Of(hash)}: {reason}";

    /// <summary>The prompt context of the reword subject.</summary>
    public static string SubjectContext(QuickRebasePlan plan) =>
        $"New subject for {ShortHash.Of(plan.Commit)}; the rest of the message is kept";

    private static string ReplayNote(QuickRebasePlan plan, string branch)
    {
        if (plan.LaterCommits == 0)
        {
            return $"It is the tip of {branch}: the branch gets a new tip commit hash.";
        }

        if (plan.LaterCommits == 1)
        {
            return $"1 later commit of {branch} is replayed and gets new hashes.";
        }

        return $"{plan.LaterCommits} later commits of {branch} are replayed and get new hashes.";
    }

    private static string Quoted(QuickRebasePlan plan) => $"{ShortHash.Of(plan.Commit)} \"{plan.Subject}\"";

    private static string QuotedCommit(string hash, string subject) => $"{ShortHash.Of(hash)} \"{subject}\"";
}
