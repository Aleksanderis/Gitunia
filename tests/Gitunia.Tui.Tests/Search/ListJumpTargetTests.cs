using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.Search;

public class ListJumpTargetTests
{
    [Fact]
    public void GivenLeadingRows_WhenJumping_ThenTheListSelectsTheRowPastThem()
    {
        var source = new DiffListSource(GraphGlyphSet.Unicode);
        var rows = new DiffRow[]
        {
            new(DiffRowKind.Added, "one", null, 1, 0),
            new(DiffRowKind.Added, "two", null, 2, 0),
            new(DiffRowKind.Added, "three", null, 3, 0),
        };
        source.Update(new DiffDocument(rows, [0], [0], [["f"]], 3));
        var list = new ListView { Width = 30, Height = 5, Source = source };
        var target = new ListJumpTarget(PaneKind.Diff, source, list, 1);

        target.JumpTo(1);
        target.ShowLabels("on");

        Assert.Equal(2, list.SelectedItem);
        Assert.Equal("on", source.JumpText);
        Assert.Equal(PaneKind.Diff, target.Pane);
    }
}
