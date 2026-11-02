using Gitunia.Core.Operations;
using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.App.Screen;

/// <summary>The key hints of the status bar: those of the overlay or editor that has the keys, else of the focused pane.</summary>
/// <param name="host">Holds the active tab.</param>
/// <param name="store">The confirm dialog.</param>
/// <param name="editors">The settings screen and the two editors.</param>
/// <param name="reflog">The reflog list.</param>
/// <param name="blame">The blame view.</param>
/// <param name="context">The repeat mode, the diff pane and the hint options.</param>
public sealed class StatusHints(
    Dispatcher host,
    OverlayStore store,
    EditorOverlays editors,
    ReflogInput reflog,
    BlameInput blame,
    HintContext context)
{
    /// <summary>The hints to show now.</summary>
    public IReadOnlyList<KeyHint> Current()
    {
        if (store.State.Kind == OverlayKind.Confirm)
        {
            return PaneHints.ForConfirm();
        }

        if (editors.Settings.IsOpen)
        {
            return PaneHints.ForSettings();
        }

        if (editors.Conflict.IsOpen)
        {
            return ConflictEditorHints();
        }

        if (editors.Rebase.IsOpen)
        {
            return editors.Rebase.IsEditingMessage ? PaneHints.ForRebaseMessage() : PaneHints.ForRebaseEditor();
        }

        if (blame.IsOpen)
        {
            return PaneHints.ForBlame();
        }

        if (reflog.IsOpen)
        {
            return PaneHints.ForReflog();
        }

        if (CommitRules.IsEditing(host.State))
        {
            return PaneHints.ForMessageEditing();
        }

        if (context.Repeat.Active is { } repeating)
        {
            return PaneHints.ForRepeat(repeating.Kind);
        }

        // Dialogs, editors and the repeat mode above keep their hints: they say which keys act right now.
        return PaneHints.Limit(
            PaneAndConflictHints(),
            context.Config.Options.StatusBarHints,
            context.Config.Options.CommandLevel);
    }

    private IReadOnlyList<KeyHint> ConflictEditorHints()
    {
        if (editors.Conflict.IsEditing)
        {
            return PaneHints.ForConflictEdit();
        }

        if (editors.Conflict.IsResultActive)
        {
            return PaneHints.ForConflictResult();
        }

        return editors.Conflict.IsVisual ? PaneHints.ForConflictVisual() : PaneHints.ForConflictVersions();
    }

    private IReadOnlyList<KeyHint> PaneAndConflictHints()
    {
        var hints = PaneHintsOfFocus();
        if (!ConflictPending())
        {
            return hints;
        }

        if (ConflictEditorSupport.AnyCanOpen(host.Workspace.ActiveTab?.WorkingTree))
        {
            return PaneHints.WithConflictEditor(hints);
        }

        return PaneHints.WithConflictMergetool(hints);
    }

    private IReadOnlyList<KeyHint> PaneHintsOfFocus()
    {
        var view = host.Workspace.ActiveTab?.View;
        if (view is { FocusedPane: PaneKind.Detail, BottomTab: BottomTab.Changes })
        {
            return PaneHints.ForDetailChanges();
        }

        if (view is { FocusedPane: PaneKind.Graph, CompareMark: not null })
        {
            return PaneHints.ForGraphComparing();
        }

        var focused = view?.FocusedPane ?? PaneKind.Graph;

        return PaneHints.For(
            focused,
            context.DiffView.IsShowingContent,
            OnConflictedFile(),
            OnRefusedConflictedFile());
    }

    // A merge, rebase, cherry-pick or revert is stopped on a conflict: <space>gr opens the editor from every pane.
    private bool ConflictPending() =>
        host.Workspace.ActiveTab is { Operation: not null } tab
        && OperationBannerBuilder.HasConflicts(tab.WorkingTree);

    // Only the Unstaged list and its diff lead with the conflict editor, so no other pane pays for the lookup.
    private bool OnConflictedFile() =>
        host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff } tab
        && ConflictedSelection.Find(tab.WorkingTree) is { } entry
        && ConflictEditorSupport.CanOpen(entry.Conflict);

    // The same panes, on a conflicted file the editor refuses: the hints name the mergetool.
    private bool OnRefusedConflictedFile() =>
        host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff } tab
        && ConflictedSelection.Find(tab.WorkingTree) is { } entry
        && !ConflictEditorSupport.CanOpen(entry.Conflict);
}
