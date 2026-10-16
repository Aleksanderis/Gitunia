using Gitunia.Core.Jump;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Tests.Search;
using Xunit;

namespace Gitunia.Core.Tests.Jump;

public class QuickJumpLabelerTests
{
    private static readonly SearchTestRow[] Rows =
    [
        new("Fix parser crash", "Bob"),
        new("Add search box", "Bob"),
        new("Fix typo", "Bob"),
        new("Refactor loader", "Bob"),
        new("fix lint", "Bob"),
    ];

    [Fact]
    public void GivenEmptyText_WhenComputing_ThenThereAreNoTargets()
    {
        var graph = SearchTestGraph.Build(Rows);

        Assert.Empty(QuickJumpLabeler.Compute(string.Empty, graph, 0, 5, 0));
    }

    [Fact]
    public void GivenText_WhenComputing_ThenMatchingRowsGetLabelsNearestTheCursorFirst()
    {
        var graph = SearchTestGraph.Build(Rows);

        var labels = QuickJumpLabeler.Compute("FIX", graph, 0, 5, 3);

        Assert.Equal([2, 4, 0], labels.Select(label => label.Row));
        Assert.All(labels, label => Assert.True(label.HasLetter));
        Assert.Equal(3, labels.Select(label => label.Letter).Distinct().Count());
    }

    [Fact]
    public void GivenMatchesFollowedByCharacters_WhenComputing_ThenNoLabelIsOneOfThem()
    {
        var graph = SearchTestGraph.Build(Rows);

        // After "f" come 'i' (fix, fix, fix), 'a' (Refactor: "f" then 'a') and 'l' ("lint" has no f; "loader" has none).
        var labels = QuickJumpLabeler.Compute("f", graph, 0, 5, 0);

        Assert.DoesNotContain(labels, label => label.Letter is 'i' or 'a');
    }

    [Fact]
    public void GivenRowsOutsideTheView_WhenComputing_ThenTheyAreIgnored()
    {
        var graph = SearchTestGraph.Build(Rows);

        var labels = QuickJumpLabeler.Compute("fix", graph, 1, 2, 1);

        Assert.Equal([2], labels.Select(label => label.Row));
    }

    [Fact]
    public void GivenMoreMatchesThanLetters_WhenComputing_ThenTheFarthestGoUnlabelled()
    {
        var rows = Enumerable.Range(0, 40).Select(index => new SearchTestRow($"item {index}", "Bob")).ToArray();
        var graph = SearchTestGraph.Build(rows);

        var labels = QuickJumpLabeler.Compute("item", graph, 0, 40, 0);

        Assert.Equal(40, labels.Count);
        Assert.True(labels[0].HasLetter);
        Assert.False(labels[^1].HasLetter);
        Assert.Equal(QuickJumpLabeler.Alphabet.Length, labels.Count(label => label.HasLetter));
    }

    [Fact]
    public void GivenALabelKey_WhenApplying_ThenItJumpsWithoutChangingTheText()
    {
        var graph = SearchTestGraph.Build(Rows);
        var labels = QuickJumpLabeler.Compute("fix", graph, 0, 5, 0);
        var letter = labels[1].Letter.ToString().ToUpperInvariant();

        var update = QuickJumpLabeler.Apply("fix", new KeyEvent(letter), labels);

        Assert.Equal(labels[1].Row, update.JumpRow);
        Assert.Equal("fix", update.Text);
    }

    [Fact]
    public void GivenAKeyThatIsNoLabel_WhenApplying_ThenTheTextGrows()
    {
        var graph = SearchTestGraph.Build(Rows);
        var labels = QuickJumpLabeler.Compute("fix", graph, 0, 5, 0);

        var update = QuickJumpLabeler.Apply("fix", new KeyEvent(" "), labels);

        Assert.Null(update.JumpRow);
        Assert.Equal("fix ", update.Text);
    }

    [Fact]
    public void GivenEmptyText_WhenApplyingAnyLetter_ThenItIsTextNeverALabel()
    {
        var update = QuickJumpLabeler.Apply(string.Empty, new KeyEvent("a"), []);

        Assert.Null(update.JumpRow);
        Assert.Equal("a", update.Text);
    }
}
