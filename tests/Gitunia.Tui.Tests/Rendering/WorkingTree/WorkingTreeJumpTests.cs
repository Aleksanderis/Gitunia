using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

public class WorkingTreeJumpTests
{
    [Fact]
    public void GivenAJumpLabel_WhenRenderingAFile_ThenTheLabelSitsOverTheCharacterAfterTheMatchInThePath()
    {
        var line = WorkingTreeRowRenderer.Render(Modified("src/parser.cs"), WorkingTreeSection.Unstaged, 30, false, GraphGlyphSet.Unicode, new JumpLabel(0, 4, 6, 'k'));

        Assert.StartsWith("M src/parserk", line.Text, StringComparison.Ordinal);
        Assert.Equal(30, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenTheListsOfATree_WhenAskingForJumpRows_ThenThePathsAreTheTextAndTheStatusLetterIsLeftOut()
    {
        var tree = Tree(Modified("a/one.cs"), StagedRename("old.cs", "new.cs"), StagedModified("two.cs"));

        var unstaged = new WorkingTreeJumpRows(tree, WorkingTreeSection.Unstaged, 20, "…");
        var staged = new WorkingTreeJumpRows(tree, WorkingTreeSection.Staged, 20, "…");

        Assert.Equal(1, unstaged.Count);
        Assert.Equal("a/one.cs", unstaged.TextOf(0));
        Assert.Equal(2, staged.Count);
        Assert.Equal("old.cs -> new.cs", staged.TextOf(0));
        Assert.Equal(18, staged.ColumnsOf(0));
    }

    [Fact]
    public void GivenALongPath_WhenLabelling_ThenAMatchPastTheListWidthTakesNoLetter()
    {
        var tree = Tree(Modified("a/very/deep/directory/structure/target.cs"), Modified("a/target.cs"));
        var rows = new WorkingTreeJumpRows(tree, WorkingTreeSection.Unstaged, 16, "…");

        var labels = QuickJumpLabeler.Compute("target", rows, new JumpWindow(0, 2, 0));

        Assert.False(labels.Single(label => label.Row == 0).HasLetter);
        Assert.True(labels.Single(label => label.Row == 1).HasLetter);
    }

    [Theory]
    [InlineData(26)]
    [InlineData(28)]
    [InlineData(34)]
    [InlineData(40)]
    public void GivenAConflictedRowInTheNameFirstStyle_WhenJumping_ThenTheJumpTextIsTheDrawnPathBeforeTheConflictText(
        int width)
    {
        var entry = Conflict("src/some/deep/directory/file.cs");
        var tree = Tree(entry);
        var rows = new WorkingTreeJumpRows(tree, WorkingTreeSection.Unstaged, width, "…", FilePathStyle.NameFirst);

        var drawn = WorkingTreeRowRenderer.Render(
            entry,
            WorkingTreeSection.Unstaged,
            width,
            false,
            GraphGlyphSet.Unicode,
            null,
            FilePathStyle.NameFirst);

        Assert.StartsWith(rows.TextOf(0) + "  both modified", drawn.Text[2..], StringComparison.Ordinal);
        Assert.Equal(width - 2 - WorkingTreeRowRenderer.ConflictColumns(entry), rows.ColumnsOf(0));
    }

    [Fact]
    public void GivenNoTree_WhenAskingForJumpRows_ThenThereAreNone()
    {
        var rows = new WorkingTreeJumpRows(null, WorkingTreeSection.Unstaged, 20, "…");

        Assert.Equal(0, rows.Count);
        Assert.Empty(QuickJumpLabeler.Compute("x", rows, new JumpWindow(0, 5, 0)));
    }
}
