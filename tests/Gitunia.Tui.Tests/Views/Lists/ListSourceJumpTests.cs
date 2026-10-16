using System.Drawing;
using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Views.Lists;

public class ListSourceJumpTests
{
    [Fact]
    public void GivenAWrappedRowScrolledPartlyOffTheTop_WhenLabelling_ThenItIsLeftOutBecauseItsFirstLineIsNotShown()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        var rows = new DiffRow[]
        {
            new(DiffRowKind.Added, "needle one needle one needle on", null, 1, 0),
            new(DiffRowKind.Added, "needle two", null, 2, 0),
        };
        source.Update(new DiffDocument(rows, [0], [0], [["f"]], 2));
        var list = Sized(source, 30, 3);
        source.Width = 20;
        source.SetWrapped(true);

        Assert.Equal(4, source.Count);
        list.SetContentSize(new Size(30, source.Count));
        // The first row wraps over several lines; scroll to its second line.
        list.Viewport = list.Viewport with { Y = 1 };
        Assert.Equal(1, list.Viewport.Y);
        var labels = source.JumpLabels(list, "needle");

        Assert.DoesNotContain(labels, label => label.Row == 0);
        Assert.Contains(labels, label => label.Row == 1);
    }

    [Fact]
    public void GivenTheSideBySideDiff_WhenLabelling_ThenBothSidesOfAPairAreLabelledAndRenderedOverTheirText()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument(
            [new DiffRow(DiffRowKind.Removed, "old needle", 1, null, 0), new DiffRow(DiffRowKind.Added, "new needle", null, 1, 0)],
            [0],
            [0],
            [["f"]],
            1));
        var list = Sized(source, 200, 5);
        source.Width = 200;
        source.SetSideBySide(true);

        var labels = source.JumpLabels(list, "needle");

        Assert.True(source.ShowsSideBySide);
        Assert.Equal([0, 1], labels.Select(label => label.Row).Order());
        Assert.All(labels, label => Assert.True(label.HasLetter));
        Assert.Equal(0, source.LineOf(1));
    }

    [Fact]
    public void GivenTheSideBySideDiff_WhenLabelling_ThenRowsOfLinesOutOfViewGetNoLabel()
    {
        var rows = new List<DiffRow>();
        for (var index = 0; index < 4; index++)
        {
            rows.Add(new DiffRow(DiffRowKind.Removed, "needle old " + index, index + 1, null, 0));
        }

        for (var index = 0; index < 4; index++)
        {
            rows.Add(new DiffRow(DiffRowKind.Added, "needle new " + index, null, index + 1, 0));
        }

        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument(rows, [0], [0], [["f"]], 4));
        var list = Sized(source, 200, 2);
        source.Width = 200;
        source.SetSideBySide(true);
        list.Viewport = list.Viewport with { Y = 2 };
        list.SetContentSize(new Size(200, source.Count));
        list.Viewport = list.Viewport with { Y = 2 };

        var labels = source.JumpLabels(list, "needle");

        // Lines 2 and 3 hold old rows 2, 3 and new rows 6, 7 (rows 4, 5 sit on lines 0 and 1).
        Assert.Equal([2, 3, 6, 7], labels.Select(label => label.Row).Order());
    }

    [Fact]
    public void GivenAFileList_WhenTheTreeChanges_ThenTheLabelsFollowTheNewFiles()
    {
        var source = new WorkingTreeListSource(WorkingTreeSection.Unstaged, GraphGlyphSet.Unicode);
        source.Update(Tree(Modified("a/target.cs"), Modified("b/other.cs")));
        var list = Sized(source, 40, 5);

        var first = source.JumpLabels(list, "target");
        source.Update(Tree(Modified("b/other.cs"), Modified("c/target.cs"), Modified("d/target.cs")));
        var second = source.JumpLabels(list, "target");

        Assert.Equal([0], first.Select(label => label.Row));
        Assert.Equal([1, 2], second.Select(label => label.Row).Order());
    }

    private static ListView Sized(IListDataSource source, int width, int height)
    {
        var list = new ListView { X = 0, Y = 0, Width = width, Height = height, Source = source };
        list.SetRelativeLayout(new Size(width + 10, height + 10));

        return list;
    }
}
