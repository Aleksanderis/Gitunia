using Gitunia.Core.Graph;
using Gitunia.Core.Jump;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Tests.Rendering.Graph;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Search;

public class QuickJumpControllerTests
{
    private static CommitGraph Graph() => CommitFactory.Graph(
        CommitFactory.Commit(6, "Fix parser crash", [5]),
        CommitFactory.Commit(5, "Add search box", [4]),
        CommitFactory.Commit(4, "Fix typo in readme", [3]),
        CommitFactory.Commit(3, "Refactor loader", [2]),
        CommitFactory.Commit(2, "fix lint", [1]),
        CommitFactory.Commit(1, "Initial commit", []));

    [Fact]
    public void GivenGraph_WhenOpeningTheJump_ThenInsertModeIsOnAndNothingIsLabelledYet()
    {
        using var harness = new PromptHarness(Graph());

        harness.Press(new Key(' '));
        harness.Press(new Key('j'));

        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.Equal(string.Empty, harness.Session.Source.JumpText);
        Assert.StartsWith("jump _", harness.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTypedText_WhenPressingALabel_ThenThatCommitIsSelectedAndTheJumpCloses()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 3 };
        harness.Press(new Key(' '));
        harness.Press(new Key('j'));
        harness.Type("fix");
        var labels = QuickJumpLabeler.Compute("fix", harness.Session.Graph, 0, 50, 3);

        harness.Type(labels.Single(label => label.Row == 0).Letter.ToString());

        Assert.Equal(0, harness.SelectedRow);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Null(harness.Session.Source.JumpText);
        Assert.Equal(string.Empty, harness.StatusLine);
    }

    [Fact]
    public void GivenTypedText_WhenPressingEnter_ThenTheNearestMatchIsSelected()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 3 };
        harness.Press(new Key(' '));
        harness.Press(new Key('j'));
        harness.Type("fix");

        harness.Press(Key.Enter);

        Assert.Equal(2, harness.SelectedRow);
        Assert.Equal(KeyMode.Normal, harness.Mode);
    }

    [Fact]
    public void GivenTypedText_WhenPressingEscape_ThenTheSelectionStaysAndTheLabelsGo()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 3 };
        harness.Press(new Key(' '));
        harness.Press(new Key('j'));
        harness.Type("fix");

        harness.Press(Key.Esc);

        Assert.Equal(3, harness.SelectedRow);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Null(harness.Session.Source.JumpText);
    }

    [Fact]
    public void GivenARowBelowTheView_WhenTypingItsText_ThenNoLabelAppearsForIt()
    {
        using var harness = new PromptHarness(Graph()) { VisibleRowCount = 2 };
        harness.Press(new Key(' '));
        harness.Press(new Key('j'));

        harness.Type("fix");

        Assert.Equal("jump fix_  1 in view", harness.StatusLine);
    }

    [Fact]
    public void GivenAScrolledView_WhenTypingText_ThenOnlyRowsFromTheTopOfTheViewAreLabelled()
    {
        using var harness = new PromptHarness(Graph()) { FirstVisibleRow = 3 };
        harness.Press(new Key(' '));
        harness.Press(new Key('j'));
        harness.Type("fix");

        harness.Press(Key.Enter);

        Assert.Equal(4, harness.SelectedRow);
    }

    [Fact]
    public void GivenNoMatchInView_WhenPressingEnter_ThenTheNoticeSaysSoAndTheSelectionStays()
    {
        using var harness = new PromptHarness(Graph()) { SelectedRow = 1 };
        harness.Press(new Key(' '));
        harness.Press(new Key('j'));
        harness.Type("zzz");

        harness.Press(Key.Enter);

        Assert.Equal(1, harness.SelectedRow);
        Assert.Equal("No match in view", harness.Notice);
    }
}
