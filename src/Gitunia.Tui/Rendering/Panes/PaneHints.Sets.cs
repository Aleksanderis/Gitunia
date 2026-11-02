using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Rendering.Panes;

// The fixed hint sets of the panes, overlays and editors.
public static partial class PaneHints
{
    /// <summary>The hints of the detail pane while its Changes tab is selected.</summary>
    public static IReadOnlyList<KeyHint> ForDetailChanges() => DetailChanges;

    /// <summary>The hints of the graph while a commit is marked for a comparison (#636).</summary>
    public static IReadOnlyList<KeyHint> ForGraphComparing() => GraphComparing;

    public static IReadOnlyList<KeyHint> For(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => Sidebar,
        PaneKind.Detail => Detail,
        PaneKind.Diff => Diff,
        PaneKind.WorkingUnstaged => WorkingUnstaged,
        PaneKind.WorkingStaged => WorkingStaged,
        PaneKind.WorkingUnstagedDiff => WorkingUnstagedDiff,
        PaneKind.WorkingStagedDiff => WorkingStagedDiff,
        PaneKind.WorkingMessage => WorkingMessage,
        _ => Graph,
    };

    /// <summary>
    /// The hints while the commit message box is in Insert mode (typed keys are text, so only these act).
    /// </summary>
    public static IReadOnlyList<KeyHint> ForMessageEditing() => MessageEditing;

    /// <summary>The hints while a confirm dialog is open (only its answers act).</summary>
    public static IReadOnlyList<KeyHint> ForConfirm() => Confirm;

    /// <summary>The hints while the file history is open.</summary>
    public static IReadOnlyList<KeyHint> ForFileHistory() => FileHistory;

    /// <summary>The hints while the reflog list is open.</summary>
    public static IReadOnlyList<KeyHint> ForReflog() => Reflog;

    /// <summary>The hints while the rebase todo editor is open.</summary>
    public static IReadOnlyList<KeyHint> ForRebaseEditor() => RebaseEditor;

    /// <summary>The hints while the conflict editor's cursor is in a version pane (base, ours or theirs).</summary>
    public static IReadOnlyList<KeyHint> ForConflictVersions() => ConflictVersions;

    /// <summary>The hints while lines are selected in a version pane of the conflict editor.</summary>
    public static IReadOnlyList<KeyHint> ForConflictVisual() => ConflictVisual;

    /// <summary>The hints while the conflict editor's cursor is in the result pane (Normal mode).</summary>
    public static IReadOnlyList<KeyHint> ForConflictResult() => ConflictResult;

    /// <summary>The hints while the conflict editor's result is typed in (Insert mode).</summary>
    public static IReadOnlyList<KeyHint> ForConflictEdit() => ConflictEdit;

    /// <summary>The hints while a reword's message is edited in the rebase todo editor.</summary>
    public static IReadOnlyList<KeyHint> ForRebaseMessage() => RebaseMessage;

    /// <summary>The hints while the settings screen is open.</summary>
    public static IReadOnlyList<KeyHint> ForSettings() => Settings;

    /// <summary>The hints while the repository picker is open.</summary>
    public static IReadOnlyList<KeyHint> ForPicker() => Picker;

    /// <summary>The hints in the footer of the commands popup.</summary>
    public static IReadOnlyList<KeyHint> ForPalette() => Palette;
}
