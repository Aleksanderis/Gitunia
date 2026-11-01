using Gitunia.Core.Effects;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;

namespace Gitunia.Core.Compare;

/// <summary>
/// Compares two commits of the graph (#535, #636, ux-workflows.md "Comparing two commits") as the pure transition
/// <c>(CompareFollow, CompareCommand, CompareContext) -> CompareUpdate</c>. The key marks the selected commit; while a
/// mark is set, every other commit under the cursor is compared with it (<c>git diff older newer</c>, older and newer
/// by graph order, in the Changes tab and the diff pane). On another commit the key moves the mark, on the marked one
/// it clears it. Esc in the graph clears the mark and the comparison. The mark is part of the tab's view state.
/// </summary>
public static class CompareReducer
{
    /// <summary>Shown when the key is pressed without a commit under the cursor.</summary>
    public const string NoCommitNotice = "No commit selected";

    /// <summary>Shown when the mark is cleared.</summary>
    public const string ClearedNotice = "Comparison mark cleared";

    private const int HashLength = 7;

    /// <summary>Applies a command to the follow state of its tab.</summary>
    public static CompareUpdate Reduce(CompareFollow follow, CompareCommand command, CompareContext context) =>
        command switch
        {
            CompareDetailsChanged changed => Changed(follow, changed.Facts, context),
            CompareEscapePressed => Escape(follow, context),
            CompareMarkPressed pressed => Pressed(follow, pressed.Selected, context),
            CompareStarted started => Started(follow, started),
            _ => throw new UnhandledCommandException(command),
        };

    /// <summary>Esc is the comparison's own when a mark is set and the graph has focus.</summary>
    public static bool ClaimsEscape(ObjectId? mark, PaneKind focusedPane) =>
        mark is not null && focusedPane == PaneKind.Graph;

    private static CompareUpdate Pressed(CompareFollow follow, ObjectId? selected, CompareContext context)
    {
        if (selected is not { } commit)
        {
            return new CompareUpdate(follow, [], [Notice(NoCommitNotice)]);
        }

        if (context.Mark is not { } mark)
        {
            return new CompareUpdate(
                follow with { ShownChanges = false },
                [new SetCompareMarkCommand(commit)],
                [Notice($"Marked {Short(commit)}: move to another commit to compare it (Esc ends)")]);
        }

        if (mark == commit)
        {
            return Cleared(follow, context);
        }

        // The mark moves here, so this commit's own changes show and the next move compares with it.
        return new CompareUpdate(
            follow with { Followed = null },
            [new SetCompareMarkCommand(commit)],
            [new ClearComparisonEffect(context.TabId), Notice($"Mark moved to {Short(commit)}")]);
    }

    private static CompareUpdate Escape(CompareFollow follow, CompareContext context) =>
        ClaimsEscape(context.Mark, context.FocusedPane) ? Cleared(follow, context) : new CompareUpdate(follow, [], []);

    private static CompareUpdate Cleared(CompareFollow follow, CompareContext context) => new(
        follow with { Followed = null },
        [new SetCompareMarkCommand(null)],
        [new ClearComparisonEffect(context.TabId), Notice(ClearedNotice)]);

    private static CompareUpdate Changed(CompareFollow follow, CompareFacts facts, CompareContext context)
    {
        var current = follow;
        if (!facts.IsLoaded || facts.Commit != current.Followed)
        {
            // A reload (after a failure, Retry) starts the comparison again.
            current = current with { Followed = null };
        }

        if (context.Mark is not { } mark
            || !facts.IsLoaded
            || facts.IsComparison
            || facts.Commit is not { } selected
            || selected == mark
            || current.Followed == selected
            || facts.MarkIndex < 0
            || facts.CommitIndex < 0)
        {
            return new CompareUpdate(current, [], []);
        }

        // The graph lists the newest commit first, so the larger index is the older commit.
        var markIsOlder = facts.MarkIndex > facts.CommitIndex;
        var older = markIsOlder ? mark : selected;
        var newer = markIsOlder ? selected : mark;

        return new CompareUpdate(current, [], [new StartComparisonEffect(context.TabId, older, newer, selected)]);
    }

    private static CompareUpdate Started(CompareFollow follow, CompareStarted started)
    {
        var followed = follow with { Followed = started.Selected };
        if (follow.ShownChanges)
        {
            return new CompareUpdate(followed, [], []);
        }

        return new CompareUpdate(
            followed with { ShownChanges = true },
            [new SetBottomTabCommand(BottomTab.Changes)],
            []);
    }

    private static PostStatusEffect Notice(string text) => new(text, StatusSeverity.Info);

    private static string Short(ObjectId id) => id.ToString()[..HashLength];
}
