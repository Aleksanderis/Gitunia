using System.Globalization;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>Draws the panes of the working-tree view as styled lines: the file lists with their empty states, and the placeholders of the diff and the message box.</summary>
public static class WorkingTreePaneRenderer
{
    /// <summary>Shown while the tab has not read its status yet.</summary>
    public const string LoadingText = "Loading status...";

    /// <summary>Shown in the Unstaged list when nothing at all is changed.</summary>
    public const string CleanText = "Nothing to commit, working tree clean";

    /// <summary>Shown in the Unstaged list when only staged changes exist.</summary>
    public const string NoUnstagedText = "No unstaged changes";

    /// <summary>Shown in the Staged list when it is empty.</summary>
    public const string NothingStagedText = "Nothing staged";

    /// <summary>Shown in the diff box when the lists have no file to show.</summary>
    public const string NoFileText = "No file selected";

    /// <summary>The first word of the diff box's title.</summary>
    public const string DiffTitleName = "Diff";

    /// <summary>Title of the message box.</summary>
    public const string MessageTitle = "Commit message";


    /// <summary>The note in the message box until the editor exists.</summary>
    public const string MessagePlaceholderText = "The commit message box is not implemented yet";

    /// <summary>The title of a list box: its name and the number of files.</summary>
    public static string ListTitle(WorkingTreeState? tree, WorkingTreeSection section)
    {
        var name = section == WorkingTreeSection.Unstaged ? "Unstaged" : "Staged";
        return tree is null ? name : $"{name} {Count(tree, section).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>The number of rows of a list, 0 while the status is not loaded.</summary>
    public static int Count(WorkingTreeState? tree, WorkingTreeSection section) => tree?.RowsOf(section).Count ?? 0;

    /// <summary>The row <paramref name="item"/> of a list: a file or (tree style) a folder, a row of the
    /// list's empty-state text (wrapped) when the list is empty, or a blank row.</summary>
    /// <param name="tree">The working tree, null while it is not loaded.</param>
    /// <param name="section">The list.</param>
    /// <param name="item">Row index in the list.</param>
    /// <param name="width">Columns for the row's text.</param>
    /// <param name="isSelected">The row is the cursor of its list.</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="jump">The quick-jump label of the row, null for none.</param>
    /// <param name="pathStyle">How a file row shows its path.</param>
    public static StyledLine RenderRow(
        WorkingTreeState? tree,
        WorkingTreeSection section,
        int item,
        int width,
        bool isSelected,
        GraphGlyphSet glyphs,
        JumpLabel? jump = null,
        FilePathStyle pathStyle = FilePathStyle.FullPath)
    {
        var count = Count(tree, section);
        if (tree is not null && item >= 0 && item < count)
        {
            var entries = tree.EntriesOf(section);
            var rows = tree.RowsOf(section);
            if (!rows.IsTree)
            {
                return WorkingTreeRowRenderer.Render(
                    entries[item], section, width, isSelected, glyphs, jump, pathStyle);
            }

            var row = rows[item];
            if (!row.IsFolder)
            {
                return WorkingTreeRowRenderer.RenderTreeFile(
                    entries[row.EntryIndex], row, section, width, isSelected, glyphs, jump);
            }

            var isClosed = tree.CollapsedOf(section).Contains(row.FolderPath);
            int? closedCount = isClosed ? rows.EntriesUnder(row.FolderPath).Count : null;

            return WorkingTreeRowRenderer.RenderFolder(row, closedCount, width, isSelected, glyphs, jump);
        }

        var builder = new LineBuilder(width, glyphs.Ellipsis);
        if (count == 0 && item >= 0)
        {
            var sentence = WrapWords(EmptyText(tree, section), width);
            if (item < sentence.Count)
            {
                builder.Append(sentence[item], SegmentRole.Faint);
            }
        }

        return builder.Build();
    }

    /// <summary>What an empty list says.</summary>
    public static string EmptyText(WorkingTreeState? tree, WorkingTreeSection section)
    {
        if (tree is null)
        {
            return section == WorkingTreeSection.Unstaged ? LoadingText : string.Empty;
        }

        if (section == WorkingTreeSection.Staged)
        {
            return NothingStagedText;
        }

        return tree.Staged.Count == 0 ? CleanText : NoUnstagedText;
    }

    /// <summary>The visible rows of a list, padded like the panes of the history view.</summary>
    /// <param name="tree">The working tree, null while it is not loaded.</param>
    /// <param name="section">The list.</param>
    /// <param name="rect">The pane's rectangle.</param>
    /// <param name="top">Index of the first visible row.</param>
    /// <param name="hasCursor">The list holds the selection, so its row is highlighted.</param>
    /// <param name="padding">Blank columns on each side.</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="pathStyle">How a file row shows its path.</param>
    public static IReadOnlyList<StyledLine> ListLines(
        WorkingTreeState? tree,
        WorkingTreeSection section,
        PaneRect rect,
        int top,
        bool hasCursor,
        int padding,
        GraphGlyphSet glyphs,
        FilePathStyle pathStyle = FilePathStyle.FullPath)
    {
        var width = PanePadding.TextWidth(rect.Width, padding);
        var cursor = hasCursor && tree?.Selection is { } selection && selection.Section == section ? selection.Index : -1;
        var lines = new List<StyledLine>(rect.Height);
        for (var offset = 0; offset < rect.Height; offset++)
        {
            var item = top + offset;
            var row = RenderRow(tree, section, item, width, item == cursor, glyphs, null, pathStyle);
            lines.Add(PanePadding.Wrap(row, padding));
        }

        return lines;
    }

    /// <summary>
    /// The title of the diff box: the side the file is on, the hunk the cursor is in, the markers of the diff options and the file
    /// (last, since a long title is cut from the right).
    /// </summary>
    /// <param name="tree">The working tree, whose selection says which side is shown; null while the status is not loaded.</param>
    /// <param name="diff">The diff box, null when there is none.</param>
    public static string DiffTitle(WorkingTreeState? tree, DiffPaneModel? diff)
    {
        if (tree?.Selection is not { } selection || diff is null || diff.Document.FileRows.Count == 0)
        {
            return DiffTitleName;
        }

        var side = selection.Section == WorkingTreeSection.Staged ? "staged" : "unstaged";
        var parts = new List<string> { DiffTitleName, side };
        var hunks = diff.Document.HunkRows.Count;
        if (hunks > 0)
        {
            var current = DiffPaneRenderer.HunkPosition(diff).ToString(CultureInfo.InvariantCulture);
            parts.Add($"change {current}/{hunks.ToString(CultureInfo.InvariantCulture)}");
        }

        if (diff.IsWrapped)
        {
            parts.Add("[wrap]");
        }

        if (diff.OptionsLabel.Length > 0)
        {
            parts.Add(diff.OptionsLabel);
        }

        parts.Add(selection.IsFolder
            ? $"{WorkingTreeRowRenderer.Printable(selection.Path)}/"
            : diff.Document.FilePaths[0][0]);
        return string.Join("  ", parts);
    }

    /// <summary>The staging buttons of the diff's title row: those of the side the selected file is on, none without a selection.</summary>
    public static WorkingTreeButtons StagingButtons(WorkingTreeState? tree) => tree?.Selection?.Section switch
    {
        WorkingTreeSection.Staged => WorkingTreeButtons.Staged,
        WorkingTreeSection.Unstaged => WorkingTreeButtons.Unstaged,
        _ => WorkingTreeButtons.None,
    };

    /// <summary>The rows of the diff box, starting at the model's top row; blank rows when the tab has no diff.</summary>
    public static IReadOnlyList<StyledLine> DiffLines(DiffPaneModel? diff, PaneRect rect, int padding, GraphGlyphSet glyphs) =>
        diff is null
            ? PlaceholderLines(string.Empty, rect, padding, glyphs)
            : DiffPaneRenderer.RenderRows(diff, rect.Width, rect.Height, glyphs, padding);

    /// <summary>The rows of the message box until the editor exists.</summary>
    public static IReadOnlyList<StyledLine> MessagePlaceholderLines(PaneRect rect, int padding, GraphGlyphSet glyphs) =>
        PlaceholderLines(MessagePlaceholderText, rect, padding, glyphs);

    /// <summary>The rows a list scrolled so that <paramref name="cursor"/> stays visible starts at.</summary>
    public static int TopFor(int cursor, int top, int height)
    {
        if (height <= 0 || cursor < 0)
        {
            return Math.Max(0, top);
        }

        if (cursor < top)
        {
            return cursor;
        }

        return cursor >= top + height ? cursor - height + 1 : top;
    }

    // Greedy word wrap, so a long empty-state sentence stays whole in a narrow list.
    private static List<string> WrapWords(string text, int width)
    {
        var rows = new List<string>();
        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && TextWidth.Of(candidate) > width)
            {
                rows.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            rows.Add(current);
        }

        return rows;
    }

    private static List<StyledLine> PlaceholderLines(string text, PaneRect rect, int padding, GraphGlyphSet glyphs)
    {
        var width = PanePadding.TextWidth(rect.Width, padding);
        var lines = new List<StyledLine>(rect.Height);
        for (var row = 0; row < rect.Height; row++)
        {
            var builder = new LineBuilder(width, glyphs.Ellipsis);
            if (row == 0)
            {
                builder.Append(text, SegmentRole.Faint);
            }

            lines.Add(PanePadding.Wrap(builder.Build(), padding));
        }

        return lines;
    }
}
