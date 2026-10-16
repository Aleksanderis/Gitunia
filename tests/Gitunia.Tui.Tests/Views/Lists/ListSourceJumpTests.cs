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
    public void GivenTheSideBySideDiff_WhenLabelling_ThenThereAreNoLabels()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        source.Update(new DiffDocument([new DiffRow(DiffRowKind.Added, "needle", null, 1, 0)], [0], [0], [["f"]], 1));
        var list = Sized(source, 200, 5);
        source.Width = 200;
        source.SetSideBySide(true);

        Assert.True(source.ShowsSideBySide);
        Assert.Empty(source.JumpLabels(list, "needle"));
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
