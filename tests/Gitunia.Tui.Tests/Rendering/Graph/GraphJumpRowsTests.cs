using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Graph;

[Trait("Os", "Independent")]
public class GraphJumpRowsTests
{
    [Fact]
    public void GivenAGraph_WhenAskingForJumpRows_ThenTheSubjectsAreTheTextAndTheColumnsStopAtTheMessageColumn()
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "Fix typo", []));
        var options = new GraphRowOptions(60, 1, GraphGlyphSet.Unicode);
        var rows = new GraphJumpRows(graph, options);

        Assert.Equal(1, rows.Count);
        Assert.Equal("Fix typo", rows.TextOf(0));
        Assert.True(rows.ColumnsOf(0) is > 0 and <= 60);
        Assert.Equal(GraphRowRenderer.MessageColumns(graph[0], options), rows.ColumnsOf(0));
    }

    [Fact]
    public void GivenANarrowGraph_WhenLabelling_ThenASubjectCutByTheColumnTakesNoLetterAndWhatIsDrawnMatchesTheLabels()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(2, "Short fix", [1]),
            CommitFactory.Commit(1, "A subject that is long enough to be cut at the message column and then fix", []));
        var options = new GraphRowOptions(40, 1, GraphGlyphSet.Unicode);
        var rows = new GraphJumpRows(graph, options);

        var labels = QuickJumpLabeler.Compute("fix", rows, new JumpWindow(0, 2, 0));

        Assert.True(labels.Single(label => label.Row == 0).HasLetter);
        Assert.False(labels.Single(label => label.Row == 1).HasLetter);
        foreach (var label in labels.Where(label => label.HasLetter))
        {
            var line = GraphRowRenderer.Render(graph[label.Row], options, false, label);
            Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.JumpLabel && segment.Text == label.Letter.ToString());
        }
    }

    [Fact]
    public void GivenARefOnACommit_WhenLabellingByItsName_ThenTheRefIsFoundAndTheLabelIsDrawnInsideIt()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(2, "Plain", [1]),
            CommitFactory.Commit(1, "Unrelated subject", [], new CommitDecoration("refs/tags/v1.2", "v1.2", RefKind.Tag, false)));
        var options = new GraphRowOptions(60, 1, GraphGlyphSet.Unicode);

        var labels = QuickJumpLabeler.Compute("v1.", new GraphJumpRows(graph, options), new JumpWindow(0, 2, 0));

        var label = Assert.Single(labels);
        Assert.Equal(1, label.Row);
        Assert.True(label.HasLetter);
        var line = GraphRowRenderer.Render(graph[1], options, false, label);
        Assert.Contains("tag: v1.", line.Text, StringComparison.Ordinal);
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.SearchMatch && segment.Text == "v1.");
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.JumpLabel && segment.Text == label.Letter.ToString());
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.Plain && segment.Text == "Unrelated subject");
    }
}
