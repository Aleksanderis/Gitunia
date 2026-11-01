using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Panes;

/// <summary>Which Changes view pane belongs to which list: the two lists and the two sides of the diff.</summary>
public static class WorkingPanes
{
    /// <summary>The panes of the Changes view that are file lists.</summary>
    public static readonly PaneKind[] Lists = [PaneKind.WorkingUnstaged, PaneKind.WorkingStaged];

    /// <summary>The panes of the Changes view that show a diff.</summary>
    public static readonly PaneKind[] Diffs = [PaneKind.WorkingUnstagedDiff, PaneKind.WorkingStagedDiff];

    /// <summary>The pane is the Unstaged or Staged file list.</summary>
    public static bool IsList(PaneKind pane) => pane is PaneKind.WorkingUnstaged or PaneKind.WorkingStaged;

    /// <summary>The pane is the diff of the Unstaged or Staged side.</summary>
    public static bool IsDiff(PaneKind pane) => pane is PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff;

    /// <summary>The pane is the commit message box.</summary>
    public static bool IsMessage(PaneKind pane) => pane == PaneKind.WorkingMessage;

    /// <summary>The pane belongs to the Changes view.</summary>
    public static bool IsWorking(PaneKind pane) => IsList(pane) || IsDiff(pane) || IsMessage(pane);

    /// <summary>The file list of a section.</summary>
    public static PaneKind ListOf(WorkingTreeSection section) =>
        section == WorkingTreeSection.Unstaged ? PaneKind.WorkingUnstaged : PaneKind.WorkingStaged;

    /// <summary>The diff of a section.</summary>
    public static PaneKind DiffOf(WorkingTreeSection section) =>
        section == WorkingTreeSection.Unstaged ? PaneKind.WorkingUnstagedDiff : PaneKind.WorkingStagedDiff;

    /// <summary>The section a list or diff pane shows (Unstaged for any other pane).</summary>
    public static WorkingTreeSection SectionOf(PaneKind pane) =>
        pane is PaneKind.WorkingStaged or PaneKind.WorkingStagedDiff ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
}
