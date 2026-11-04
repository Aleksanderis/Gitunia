using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Staging;

/// <summary>
/// Stages and unstages whole files from the Changes view lists and hunks or lines from its diff (ux-workflows.md,
/// workflow 2, "Stage and unstage"), as pure transitions <c>(StagingCommand, StagingContext) -> StagingUpdate</c>. The
/// effect names the paths taken from the lists as they are now; its result arrives only after the status has been read
/// again, and until then the tab counts as busy, so the next key sees the list the cursor sits in. Marking a conflicted
/// file resolved asks first. One stage or unstage runs per tab.
/// </summary>
public static class StagingReducer
{
    /// <summary>Shown when a second stage or unstage is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: the last stage or unstage is still running";

    /// <summary>Shown when another operation of the tab is changing the index or the working tree.</summary>
    public const string BlockedNotice = "Busy: another operation of this tab is still running";

    private const string TooSmallNotice = "The screen is too small to ask: nothing was marked resolved";
    private const string NotLoadedNotice = "The status is not loaded yet";

    /// <summary>Applies a command and returns the effects to run and the commands that follow.</summary>
    public static StagingUpdate Reduce(StagingCommand command, StagingContext context) => command switch
    {
        ResolveConfirmed confirmed => ResolveAccepted(confirmed, context),
        StageAllThenRequested then => StageAllThen(then, context),
        StageResolvedRequested resolved => StageResolved(resolved, context),
        StageSelectionRequested selection => StageSelection(selection, context),
        StagingCancelled => StagingUpdate.Of(),
        StagingFailed failed => StagingUpdate.Of(Post(failed.Text, StatusSeverity.Error)),
        StagingRequested requested => Requested(requested, context),
        StagingSucceeded succeeded => Succeeded(succeeded, context),
        _ => throw new UnhandledCommandException(command),
    };

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static StagingUpdate Refuse(string text) => StagingUpdate.Of(Post(text));

    private static StagingUpdate Requested(StagingRequested requested, StagingContext context)
    {
        var isStage = requested.Action is StagingAction.Stage or StagingAction.StageAll;
        var isAll = requested.Action is StagingAction.StageAll or StagingAction.UnstageAll;
        var section = isStage ? WorkingTreeSection.Unstaged : WorkingTreeSection.Staged;
        var focus = context.FocusedPane;
        if (focus != PaneKind.WorkingUnstaged && focus != PaneKind.WorkingStaged)
        {
            return Refuse("Focus a file list first");
        }

        var focused = focus == PaneKind.WorkingStaged ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
        if (focused != section)
        {
            return Refuse(isStage ? "Stage works in the Unstaged list" : "Unstage works in the Staged list");
        }

        if (context.Tree is not { } tree)
        {
            return Refuse(NotLoadedNotice);
        }

        if (RefusalOfBusy(context) is { } busy)
        {
            return busy;
        }

        var conflicted = StagingPlanner.ConflictedUnderCursor(tree);
        if (isStage && !isAll && requested.Range is null && conflicted is not null)
        {
            return AskResolved(conflicted, context);
        }

        var plan = StagingPlanner.For(tree, section, isAll, requested.Range);
        if (plan.Notice.Length > 0)
        {
            return Refuse(plan.Notice);
        }

        return StagingUpdate.Of(Paths(context, plan, isStage, StagingFollow.None));
    }

    private static StagingUpdate StageAllThen(StageAllThenRequested then, StagingContext context)
    {
        if (context.Tree is not { } tree)
        {
            return Refuse(NotLoadedNotice);
        }

        if (RefusalOfBusy(context) is { } busy)
        {
            return busy;
        }

        var plan = StagingPlanner.All(tree, WorkingTreeSection.Unstaged);
        if (plan.Notice.Length > 0)
        {
            return Refuse(plan.Notice);
        }

        var follow = new StagingFollow(Then: then.Then);

        return StagingUpdate.Of(Paths(context, plan, true, follow));
    }

    private static StagingUpdate StageSelection(StageSelectionRequested request, StagingContext context)
    {
        if (RefusalOfBusy(context) is { } busy)
        {
            return busy;
        }

        var effect = new StageSelectionEffect(
            context.TabId,
            context.Location,
            request.Selection,
            IsUnborn(context.Tree),
            request.Move);

        return StagingUpdate.Of(effect);
    }

    private static StagingUpdate StageResolved(StageResolvedRequested resolved, StagingContext context)
    {
        var entry = context.Tree?.Unstaged
            .FirstOrDefault(candidate => candidate.Path == resolved.Path && candidate.Kind == StatusEntryKind.Unmerged);
        var name = PrintableText.Of(resolved.Path);
        if (entry is null)
        {
            return SavedNotStaged($"{name} is no longer listed as conflicted: saved, not staged", context);
        }

        if (context.IsBusy || context.IsBlocked)
        {
            var notice = context.IsBusy ? BusyNotice : BlockedNotice;

            return SavedNotStaged($"{notice}; the saved file was not staged", context);
        }

        var plan = StagingPlanner.Resolve(entry, true);
        var follow = new StagingFollow(Resolved: new ResolvedFile(resolved.Path, name));

        return StagingUpdate.Of(Paths(context, plan, true, follow));
    }

    // The conflict editor closed on a file that was saved but not staged: the lists are read again, so they show it.
    private static StagingUpdate SavedNotStaged(string text, StagingContext context) =>
        StagingUpdate.Of(Post(text), new RefreshTabEffect(context.TabId));

    // Marking a conflicted file resolved is an `add`: the markers stay in the file, so the user confirms first.
    private static StagingUpdate AskResolved(StatusEntry entry, StagingContext context)
    {
        var name = PrintableText.Of(entry.Path);
        var prompt = new ConfirmPrompt(
            "Mark conflict resolved",
            [
                $"Mark {name} as resolved?",
                "Conflict markers still in the file are not removed or checked: they are committed as they are.",
            ],
            "Afterwards you can re-create the conflict from the conflict editor (Ctrl-x there); that overwrites the file's current content.",
            "Mark resolved");

        var accepted = new ResolveConfirmed(context.TabId, entry);

        return StagingUpdate.Of(new ShowConfirmEffect(prompt, accepted, TooSmallNotice));
    }

    private static StagingUpdate ResolveAccepted(ResolveConfirmed confirmed, StagingContext context)
    {
        if (context.IsBusy || context.IsBlocked)
        {
            var notice = context.IsBusy ? BusyNotice : BlockedNotice;

            return Refuse($"{notice}; nothing was marked resolved");
        }

        var plan = StagingPlanner.Resolve(confirmed.Entry, false);

        return StagingUpdate.Of(Paths(context, plan, true, StagingFollow.None));
    }

    private static StagePathsEffect Paths(
        StagingContext context,
        StagingPlan plan,
        bool isStage,
        StagingFollow follow) =>
        new(context.TabId, context.Location, plan, isStage, IsUnborn(context.Tree), follow);

    private static StagingUpdate? RefusalOfBusy(StagingContext context)
    {
        if (context.IsBusy)
        {
            return Refuse(BusyNotice);
        }

        return context.IsBlocked ? Refuse(BlockedNotice) : null;
    }

    private static bool IsUnborn(WorkingTreeState? tree) => tree?.Snapshot.Branch.Head.Kind == HeadKind.Unborn;

    // The status has been read again by now: the list the cursor sits in and the conflicts left are current.
    private static StagingUpdate Succeeded(StagingSucceeded succeeded, StagingContext context)
    {
        var follow = succeeded.Follow;
        List<AppEffect> effects = [];
        List<AppCommand> next = [];
        if (follow.Resolved is { } resolved && context.Tree is { } tree)
        {
            var report = ReportResolved(resolved, tree, context.HasOperation);
            effects.Add(Post(report.Text, StatusSeverity.Info));
            next.AddRange(report.Next.Select(command => new TabCommand(context.TabId, command)));
        }
        else
        {
            effects.Add(Post(succeeded.Text, succeeded.Severity));
        }

        if (follow.Move is { } move)
        {
            effects.Add(new MoveDiffCursorEffect(context.TabId, move));
        }

        if (follow.Then is { } then)
        {
            next.Add(then);
        }

        return new StagingUpdate([.. effects], [.. next]);
    }

    private sealed record ResolvedReport(string Text, ImmutableArray<RepositoryCommand> Next);

    private static ResolvedReport ReportResolved(ResolvedFile resolved, WorkingTreeState tree, bool hasOperation)
    {
        var left = tree.Unstaged.Count(entry => entry.Kind == StatusEntryKind.Unmerged);
        if (left == 0)
        {
            var next = hasOperation ? $"{StagingReports.ContinueKeys} continue" : "commit it";
            var text = $"Resolved and staged {resolved.Name} (Staged list): all conflicts resolved, {next}";

            return new ResolvedReport(text, SelectStaged(tree, resolved.Path));
        }

        var counted = CountText.Plural(left, "conflict");

        return new ResolvedReport(
            $"Resolved and staged {resolved.Name} (Staged list), {counted} left",
            SelectFirstConflicted(tree));
    }

    // Lands the cursor on the file's own row, so the user sees where the resolved file went; the selection alone would
    // leave the focus in the other list, with the cursor drawn in an unfocused one.
    private static ImmutableArray<RepositoryCommand> SelectStaged(WorkingTreeState tree, string path)
    {
        var entries = tree.Staged;
        var rows = tree.RowsOf(WorkingTreeSection.Staged);
        for (var row = 0; row < rows.Count; row++)
        {
            if (!rows[row].IsFolder && entries[rows[row].EntryIndex].Path == path)
            {
                return
                [
                    new SelectWorkingTreeFileCommand(WorkingTreeSection.Staged, row),
                    new FocusPaneCommand(PaneKind.WorkingStaged),
                ];
            }
        }

        return [];
    }

    private static ImmutableArray<RepositoryCommand> SelectFirstConflicted(WorkingTreeState tree)
    {
        var rows = tree.RowsOf(WorkingTreeSection.Unstaged);
        for (var row = 0; row < rows.Count; row++)
        {
            if (!rows[row].IsFolder && tree.Unstaged[rows[row].EntryIndex].Kind == StatusEntryKind.Unmerged)
            {
                return [new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, row)];
            }
        }

        return [];
    }
}
