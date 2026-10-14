using Gitunia.Core.Diff;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Clipboard;

public class RangeTextBuilderTests
{
    [Fact]
    public void GivenARangeOverCommits_WhenBuildingGraphText_ThenItHasOneHashPerLineInGraphOrder()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(3, "c", [2]),
            CommitFactory.Commit(2, "b", [1]),
            CommitFactory.Commit(1, "a", []));

        var text = RangeTextBuilder.Graph(graph, new RowRange(2, 1));

        Assert.Equal(CommitFactory.Id(2) + "\n" + CommitFactory.Id(1) + "\n", text);
    }

    [Fact]
    public void GivenARangePastTheLastCommit_WhenBuildingGraphText_ThenOnlyExistingCommitsAreCopied()
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "a", []));

        Assert.Equal(CommitFactory.Id(1) + "\n", RangeTextBuilder.Graph(graph, new RowRange(0, 9)));
    }

    [Fact]
    public void GivenARangeOverDiffRows_WhenBuildingDiffText_ThenRowsKeepTheirPrefixAndNotesAreSkipped()
    {
        using var source = DiffSource(wrapped: false);

        var text = RangeTextBuilder.Diff(source, new RowRange(0, 4));

        Assert.Equal("@@ -1 +1 @@\n-old\n+new\n context\n", text);
    }

    [Fact]
    public void GivenAWrappedRow_WhenBuildingDiffText_ThenTheRowIsCopiedOnceAndWhole()
    {
        using var source = DiffSource(wrapped: true);
        var firstLine = source.LineOf(3);

        var text = RangeTextBuilder.Diff(source, new RowRange(firstLine, firstLine + 1));

        Assert.True(source.LineOf(4) - firstLine > 1);
        Assert.Equal("+" + new string('n', 60) + "\n", text);
    }

    [Fact]
    public void GivenARangeOverDetailRows_WhenBuildingDetailText_ThenItHasTheRowTextWithoutTrailingSpaces()
    {
        using var source = new DetailListSource(GraphGlyphSet.Unicode);
        source.Update(new DetailContent("abc1234", [Row("Author: Ann  "), Row("Date: today"), Row("last")], 0, [], BottomTab.Details));

        var text = RangeTextBuilder.Detail(source, new RowRange(0, 1));

        Assert.Equal("Author: Ann\nDate: today\n", text);
    }

    private static DetailRow Row(string text) => new([new StyledSegment(text, SegmentRole.Plain)]);

    private static DiffListSource DiffSource(bool wrapped)
    {
        var rows = new List<DiffRow>
        {
            new(DiffRowKind.Note, "note", null, null, 0),
            new(DiffRowKind.HunkHeader, "@@ -1 +1 @@", null, null, 0),
            new(DiffRowKind.Removed, "old", 1, null, 0),
            new(DiffRowKind.Added, wrapped ? new string('n', 60) : "new", null, 1, 0),
            new(DiffRowKind.Context, "context", 2, 2, 0),
        };
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument(rows, [1], [0], [["f.txt"]], 2));
        if (wrapped)
        {
            source.Width = 30;
            source.SetWrapped(true);
        }

        return source;
    }
}
