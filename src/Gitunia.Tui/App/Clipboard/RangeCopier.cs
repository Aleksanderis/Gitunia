using Gitunia.Core.Lists;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>Copies the rows of a Visual-mode range of the graph, detail, diff or Changes view file list pane through the clipboard.</summary>
/// <param name="clipboard">Copies the text.</param>
/// <param name="sessions">The active tab's session, none when there is no tab.</param>
/// <param name="workingTree">The active tab's working tree for the file lists; omitted where the lists have no ranges.</param>
public sealed class RangeCopier(
    ClipboardCopier clipboard,
    IActiveSessions sessions,
    IActiveWorkingTree? workingTree = null)
{
    private static readonly RangeCopyResult NothingToCopy = new(false, "Nothing to copy");

    /// <summary>Copies the range of <paramref name="pane"/>.</summary>
    /// <returns>Whether the text was copied, with a notice for the status bar when nothing was copied (a failed copy posts its own warning).</returns>
    /// <param name="pane">The pane the selection is in.</param>
    /// <param name="range">The rows.</param>
    /// <param name="chars">The characters of a character-wise selection, null for whole rows.</param>
    /// <param name="isOldSide">The characters are those of the left (old) side of the side-by-side diff.</param>
    public RangeCopyResult Copy(PaneKind pane, RowRange range, TextSelection? chars = null, bool isOldSide = false)
    {
        if (pane is PaneKind.WorkingUnstaged or PaneKind.WorkingStaged)
        {
            return CopyFiles(pane, range);
        }

        var session = sessions.ActiveSession();
        if (session is null)
        {
            return NothingToCopy;
        }

        if (chars is { } selection)
        {
            return CopyChars(pane, session, selection, isOldSide);
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

    private RangeCopyResult CopyChars(
        PaneKind pane,
        TerminalTabSession session,
        TextSelection selection,
        bool isOldSide)
    {
        ITextSource? source = pane switch
        {
            PaneKind.Diff => session.Diff?.Source,
            PaneKind.Detail => session.DetailSource,
            PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => session.WorkingDiff?.Source,
            _ => null,
        };
        var text = source?.CopyText(selection, isOldSide) ?? string.Empty;
        if (text.Length == 0)
        {
            return NothingToCopy;
        }

        var characters = text.EnumerateRunes().Count();
        var description = characters == 1 ? "1 selected character" : $"{characters} selected characters";
        var copied = clipboard.Copy(text, description);

        return new RangeCopyResult(copied, string.Empty);
    }

    private RangeCopyResult CopyFiles(PaneKind pane, RowRange range)
    {
        if (workingTree?.WorkingTree() is not { } tree)
        {
            return NothingToCopy;
        }

        var section = pane == PaneKind.WorkingStaged ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
        var entries = tree.EntriesOf(section);
        var rows = tree.RowsOf(section);
        var paths = new List<string>();
        for (var row = Math.Max(0, range.First); row <= Math.Min(range.Last, rows.Count - 1); row++)
        {
            var found = rows[row];
            var path = found.IsFolder ? found.FolderPath : entries[found.EntryIndex].Path;
            paths.Add(WorkingTreeRowRenderer.Printable(path));
        }

        if (paths.Count == 0)
        {
            return NothingToCopy;
        }

        var description = paths.Count == 1 ? "1 file path" : $"{paths.Count} file paths";
        var copied = clipboard.Copy(string.Join('\n', paths), description);
        return new RangeCopyResult(copied, string.Empty);
    }
}
