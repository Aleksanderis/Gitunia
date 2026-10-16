using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>Copies the rows of a Visual-mode range of the graph, detail, diff or working-tree file list pane through the clipboard.</summary>
/// <param name="clipboard">Copies the text.</param>
/// <param name="activeSession">The active tab's session, null when there is none.</param>
/// <param name="workingTree">The active tab's working tree for the file lists, null when it has none; omitted where the lists have no ranges.</param>
public sealed class RangeCopier(ClipboardCopier clipboard, Func<TabSession?> activeSession, Func<WorkingTreeState?>? workingTree = null)
{
    private static readonly RangeCopyResult NothingToCopy = new(false, "Nothing to copy");

    /// <summary>Copies the range of <paramref name="pane"/>.</summary>
    /// <returns>Whether the text was copied, with a notice for the status bar when nothing was copied (a failed copy posts its own warning).</returns>
    public RangeCopyResult Copy(PaneKind pane, RowRange range)
    {
        if (pane is PaneKind.WorkingUnstaged or PaneKind.WorkingStaged)
        {
            return CopyFiles(pane, range);
        }

        var session = activeSession();
        if (session is null)
        {
            return NothingToCopy;
        }

        string text;
        string description;
        switch (pane)
        {
            case PaneKind.Graph:
                text = RangeTextBuilder.Graph(session.Graph, range);
                description = "commit hashes";
                break;
            case PaneKind.Detail:
                text = RangeTextBuilder.Detail(session.DetailSource, range);
                description = "detail rows";
                break;
            case PaneKind.Diff when session.Diff is { } diff:
                text = RangeTextBuilder.Diff(diff.Source, range);
                description = "diff rows";
                break;
            case PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff when session.WorkingDiff is { } workingDiff:
                text = RangeTextBuilder.Diff(workingDiff.Source, range);
                description = "diff rows";
                break;
            default:
                return NothingToCopy;
        }

        if (text.Length == 0)
        {
            return NothingToCopy;
        }

        var lines = text.Count(character => character == '\n');
        var copied = clipboard.Copy(text, lines == 1 ? $"1 line of {description}" : $"{lines} lines of {description}");
        return new RangeCopyResult(copied, string.Empty);
    }

    private RangeCopyResult CopyFiles(PaneKind pane, RowRange range)
    {
        if (workingTree?.Invoke() is not { } tree)
        {
            return NothingToCopy;
        }

        var entries = pane == PaneKind.WorkingStaged ? tree.Staged : tree.Unstaged;
        var first = Math.Max(0, range.First);
        var paths = entries
            .Skip(first)
            .Take(Math.Max(0, range.Last - first + 1))
            .Select(entry => WorkingTreeRowRenderer.Printable(entry.Path))
            .ToList();
        if (paths.Count == 0)
        {
            return NothingToCopy;
        }

        var description = paths.Count == 1 ? "1 file path" : $"{paths.Count} file paths";
        var copied = clipboard.Copy(string.Join('\n', paths), description);
        return new RangeCopyResult(copied, string.Empty);
    }
}
