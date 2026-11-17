using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.History.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Reflog.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

/// <summary>
/// The window half of the list contract (see <c>ListViewportContractTests</c> in Core): every renderer that windows a
/// list keeps the selected row inside the rows it shows at the top, in the middle, at the end, in a smaller window and
/// for an empty list. A new windowing renderer adds a row to <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class ListWindowContractTests
{
    private const int Items = 40;

    private const int Width = 100;

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> Surfaces => ["grouped-rows-plain", "grouped-rows-grouped"];

    /// <summary>
    /// The frame builders that window their own list, by name. The graph, diff, sidebar and detail panes are Terminal.Gui
    /// <c>ListView</c>s and use the framework's viewport, so they are not part of this contract.
    /// </summary>
    public static TheoryData<string> FrameSurfaces => ["reflog", "file-history", "rebase"];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAnySelection_WhenWindowing_ThenTheSelectedLineIsShown(string surface)
    {
        foreach (var rows in new[] { 12, 5, 1 })
        {
            foreach (var selected in new[] { 0, 1, Items / 2, Items - 2, Items - 1 })
            {
                var window = WindowOf(surface, Items, selected, rows);

                Assert.InRange(window.Selected, window.Top, window.Top + window.RowCount - 1);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAnEmptyList_WhenWindowing_ThenOneRowAtZeroShows(string surface)
    {
        var window = WindowOf(surface, 0, 0, 8);

        Assert.Equal(1, window.RowCount);
        Assert.Equal(0, window.Top);
    }

    [Theory]
    [MemberData(nameof(FrameSurfaces))]
    public void GivenAFrameBuilder_WhenTheSelectionMoves_ThenTheHighlightedRowIsTheSelectedEntry(string surface)
    {
        foreach (var height in new[] { 24, 16, 12 })
        {
            foreach (var selected in new[] { 0, 1, Items / 2, Items - 2, Items - 1 })
            {
                Assert.Equal(selected, SelectedEntryOf(surface, Items, selected, height));
            }
        }
    }

    [Theory]
    [MemberData(nameof(FrameSurfaces))]
    public void GivenAFrameBuilderAndAnEmptyList_WhenBuilding_ThenNoRowIsHighlighted(string surface)
    {
        Assert.Null(SelectedEntryOf(surface, 0, 0, 24));
    }

    private static int? SelectedEntryOf(string surface, int count, int selected, int height)
    {
        var tab = new TabId(1);
        var reflog = new ReflogState(tab, "HEAD", "HEAD", Reflog(count)).MoveTo(selected);
        var history = new FileHistoryState(tab, "a.txt", "HEAD", FileHistory(count)).MoveTo(selected);
        var rebase = Rebase(count).MoveTo(selected);
        var frame = surface switch
        {
            "reflog" => ReflogFrameBuilder.Build(reflog, Width, height, GraphGlyphSet.Unicode, Now),
            "file-history" => FileHistoryFrameBuilder.Build(history, Width, height, GraphGlyphSet.Unicode),
            _ => RebaseEditorFrameBuilder.Build(rebase, Width, height, GraphGlyphSet.Unicode),
        };
        var lines = frame!.Lines.ToList();
        var line = lines.FindIndex(IsHighlighted);
        if (line < 0)
        {
            return null;
        }

        Assert.Single(lines, IsHighlighted);

        return surface switch
        {
            "reflog" => ReflogFrameBuilder.EntryIndexAt(reflog, line, height),
            "file-history" => FileHistoryFrameBuilder.EntryIndexAt(history, line, height),
            _ => RebaseEditorFrameBuilder.EntryIndexAt(rebase, line, Width, height),
        };
    }

    private static bool IsHighlighted(StyledLine line) =>
        line.IsSelected || line.Segments.Any(segment => segment.Role == SegmentRole.Cursor);

    private static List<ReflogEntry> Reflog(int count) =>
        [.. Enumerable.Range(0, count).Select(index => new ReflogEntry(index, new string('a', 40), "commit", $"m{index}", Now))];

    private static List<FileHistoryEntry> FileHistory(int count) =>
        [
            .. Enumerable.Range(0, count)
                .Select(index => new FileHistoryEntry(
                    new string('b', 40),
                    "Ann",
                    "2026-11-02",
                    $"c{index}",
                    "a.txt",
                    null,
                    FileChangeKind.Modified)),
        ];

    private static RebaseEditorState Rebase(int count) =>
        RebaseEditorState.Create(
            new TabId(1),
            "topic",
            "main",
            RebaseBase.OntoCommit("x"),
            "tip",
            [.. Enumerable.Range(0, count).Select(index => new RebaseTodoCommit(new string('c', 40), $"s{index}"))]);

    private static ListWindow WindowOf(string surface, int count, int selected, int rows)
    {
        var grouped = surface == "grouped-rows-grouped";
        var entries = Enumerable.Range(0, count)
            .Select(index => new PaletteEntry(
                ActionIds.RepositoryRefresh,
                $"entry{index}",
                string.Empty,
                Group: grouped ? $"group{index / 5}" : null))
            .ToList();

        return GroupedRows.Of(entries, rows, scrolls: true).Window(selected, rows);
    }
}
