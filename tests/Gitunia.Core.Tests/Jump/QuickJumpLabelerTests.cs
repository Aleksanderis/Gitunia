using Gitunia.Core.Jump;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Jump.Models;
using Xunit;

namespace Gitunia.Core.Tests.Jump;

[Trait("Os", "Independent")]
public class QuickJumpLabelerTests
{
    private static readonly string[] Subjects =
    [
        "Fix parser crash",
        "Add search box",
        "Fix typo",
        "Refactor loader",
        "fix lint",
    ];

    [Fact]
    public void GivenEmptyText_WhenComputing_ThenThereAreNoTargets()
    {
        var rows = new TextJumpRows(Subjects);

        Assert.Empty(QuickJumpLabeler.Compute(string.Empty, rows, new JumpWindow(0, 5, 0)));
    }

    [Fact]
    public void GivenText_WhenComputing_ThenMatchingRowsGetLabelsNearestTheCursorFirst()
    {
        var rows = new TextJumpRows(Subjects);

        var labels = QuickJumpLabeler.Compute("FIX", rows, new JumpWindow(0, 5, 3));

        Assert.Equal([2, 4, 0], labels.Select(label => label.Row));
        Assert.All(labels, label => Assert.True(label.HasLetter));
        Assert.Equal(3, labels.Select(label => label.Letter).Distinct().Count());
    }

    [Fact]
    public void GivenMatchesFollowedByCharacters_WhenComputing_ThenNoLabelIsOneOfThem()
    {
        var rows = new TextJumpRows(Subjects);

        // After "f" come 'i' (fix, fix, fix), 'a' (Refactor: "f" then 'a') and 'l' ("lint" has no f; "loader" has none).
        var labels = QuickJumpLabeler.Compute("f", rows, new JumpWindow(0, 5, 0));

        Assert.DoesNotContain(labels, label => label.Letter is 'i' or 'a');
    }

    [Fact]
    public void GivenRowsOutsideTheView_WhenComputing_ThenTheyAreIgnored()
    {
        var rows = new TextJumpRows(Subjects);

        var labels = QuickJumpLabeler.Compute("fix", rows, new JumpWindow(1, 2, 1));

        Assert.Equal([2], labels.Select(label => label.Row));
    }

    [Fact]
    public void GivenMoreMatchesThanLetters_WhenComputing_ThenTheFarthestGoUnlabelled()
    {
        var rows = new TextJumpRows([.. Enumerable.Range(0, 40).Select(index => $"item {index}")]);

        var labels = QuickJumpLabeler.Compute("item", rows, new JumpWindow(0, 40, 0));

        Assert.Equal(40, labels.Count);
        Assert.True(labels[0].HasLetter);
        Assert.False(labels[^1].HasLetter);
        Assert.Equal(QuickJumpLabeler.Alphabet.Length, labels.Count(label => label.HasLetter));
    }

    [Fact]
    public void GivenTextThatRepeatsInOneSubject_WhenComputing_ThenEveryContinuationIsBlocked()
    {
        var rows = new TextJumpRows(["aab aac"]);

        var labels = QuickJumpLabeler.Compute("aa", rows, new JumpWindow(0, 1, 0));

        Assert.Single(labels);
        Assert.NotEqual('b', labels[0].Letter);
        Assert.NotEqual('c', labels[0].Letter);
    }

    [Fact]
    public void GivenALabelKey_WhenApplying_ThenItJumpsWithoutChangingTheText()
    {
        var rows = new TextJumpRows(Subjects);
        var labels = QuickJumpLabeler.Compute("fix", rows, new JumpWindow(0, 5, 0));
        var letter = labels[1].Letter.ToString().ToUpperInvariant();

        var update = QuickJumpLabeler.Apply("fix", new KeyEvent(letter), labels);

        Assert.Equal(labels[1].Row, update.JumpRow);
        Assert.Equal("fix", update.Text);
    }

    [Fact]
    public void GivenAKeyThatIsNoLabel_WhenApplying_ThenTheTextGrows()
    {
        var rows = new TextJumpRows(Subjects);
        var labels = QuickJumpLabeler.Compute("fix", rows, new JumpWindow(0, 5, 0));

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

    [Fact]
    public void GivenAMatchWhoseLabelFallsPastTheColumns_WhenComputing_ThenItIsATargetWithoutALetter()
    {
        var rows = new TextJumpRows(["short fix here", "a very long line that goes on and on and then fix appears"], columns: 20);

        var labels = QuickJumpLabeler.Compute("fix", rows, new JumpWindow(0, 2, 0));

        Assert.True(labels.Single(label => label.Row == 0).HasLetter);
        Assert.False(labels.Single(label => label.Row == 1).HasLetter);
    }

    [Fact]
    public void GivenALongRowCutWithAnEllipsis_WhenTheLabelWouldSitOnTheEllipsis_ThenItTakesNoLetter()
    {
        // 12 columns, the text is wider so the last column shows the ellipsis: the label over column 11 would be hidden by it.
        var rows = new TextJumpRows(["0123456fix8 and more"], columns: 12);

        var labels = QuickJumpLabeler.Compute("fix", rows, new JumpWindow(0, 1, 0));

        Assert.True(labels[0].HasLetter);
        var tight = QuickJumpLabeler.Compute("fix", new TextJumpRows(["01234567fix8 and more"], columns: 12), new JumpWindow(0, 1, 0));
        Assert.False(tight[0].HasLetter);
    }

    [Fact]
    public void GivenAMatchThatEndsTheRow_WhenTheRowIsFull_ThenTheAppendedLabelTakesNoLetter()
    {
        var full = QuickJumpLabeler.Compute("ab", new TextJumpRows(["xxab"], columns: 4), new JumpWindow(0, 1, 0));
        var roomy = QuickJumpLabeler.Compute("ab", new TextJumpRows(["xxab"], columns: 5), new JumpWindow(0, 1, 0));

        Assert.False(full[0].HasLetter);
        Assert.True(roomy[0].HasLetter);
    }

    [Fact]
    public void GivenAWideCharacterUnderTheLabel_WhenItDoesNotFitTheColumns_ThenTheLabelTakesNoLetter()
    {
        // The wide character after "ab" needs two columns: columns 2 and 3, but the row has only 3.
        var narrow = QuickJumpLabeler.Compute("ab", new TextJumpRows(["ab\u4e2dcd"], columns: 3), new JumpWindow(0, 1, 0));
        var fits = QuickJumpLabeler.Compute("ab", new TextJumpRows(["ab\u4e2dcd"], columns: 5), new JumpWindow(0, 1, 0));

        Assert.False(narrow[0].HasLetter);
        Assert.True(fits[0].HasLetter);
    }

    [Fact]
    public void GivenMatchesPastTheColumns_WhenLabelling_ThenTheyUseUpNoLetters()
    {
        var texts = new List<string>();
        for (var index = 0; index < QuickJumpLabeler.Alphabet.Length; index++)
        {
            texts.Add("x item");
        }

        texts.Insert(0, "very far away item");
        var rows = new TextJumpRows(texts, columns: 10);

        var labels = QuickJumpLabeler.Compute("item", rows, new JumpWindow(0, texts.Count, 0));

        Assert.Equal(QuickJumpLabeler.Alphabet.Length, labels.Count(label => label.HasLetter));
        Assert.False(labels.Single(label => label.Row == 0).HasLetter);
    }
}
