using Gitunia.Core.Keymap.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Input;
using Gitunia.Tui.Tests.Rendering.Graph;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Search;

/// <summary>
/// The text field contract (type, Left, Ctrl-w, Backspace on a grapheme, paste, Ctrl-u) against the graph prompts
/// whose keys go through their controllers: the graph search, the ref jump and the quick jump. The fields reduced in
/// Core are in <c>TextFieldContractTests</c>.
/// </summary>
[Trait("Os", "Independent")]
public class PromptFieldContractTests
{
    private const string Grapheme = "é";

    public static TheoryData<string> Prompts => ["graph search", "ref jump", "quick jump"];

    [Theory]
    [MemberData(nameof(Prompts))]
    public void GivenAPrompt_WhenTypingAndMovingTheCaret_ThenTextAndCaretFollow(string prompt)
    {
        using var harness = Opened(prompt);
        harness.Type("foo bar");

        harness.Press(Key.CursorLeft);

        Assert.Equal("foo bar", harness.StatusLine.Text);
        Assert.Equal(6, harness.StatusLine.Caret);
    }

    [Theory]
    [MemberData(nameof(Prompts))]
    public void GivenTheCaretInsideTheLastWord_WhenCtrlWIsPressed_ThenTheWordBeforeTheCaretGoes(string prompt)
    {
        using var harness = Opened(prompt);
        harness.Type("foo bar");
        harness.Press(Key.CursorLeft);

        harness.Press(Key.W.WithCtrl);

        Assert.Equal("foo r", harness.StatusLine.Text);
        Assert.Equal(4, harness.StatusLine.Caret);
    }

    [Theory]
    [MemberData(nameof(Prompts))]
    public void GivenAGraphemeOfTwoChars_WhenBackspaceIsPressed_ThenTheWholeGraphemeGoes(string prompt)
    {
        using var harness = Opened(prompt);
        harness.Type($"a{Grapheme}");

        harness.Press(Key.Backspace);

        Assert.Equal("a", harness.StatusLine.Text);
    }

    [Theory]
    [MemberData(nameof(Prompts))]
    public void GivenTheCaretInTheMiddle_WhenTextIsPasted_ThenItGoesInAtTheCaretAndTheCaretFollows(string prompt)
    {
        using var harness = Opened(prompt);
        harness.Type("ac");
        harness.Press(Key.CursorLeft);

        ((ITextKeyHandler)harness).HandleTextKey(new KeyEvent("XY"));

        Assert.Equal("aXYc", harness.StatusLine.Text);
        Assert.Equal(3, harness.StatusLine.Caret);
    }

    [Theory]
    [MemberData(nameof(Prompts))]
    public void GivenAPrompt_WhenCtrlUIsPressed_ThenTheTextBeforeTheCaretGoes(string prompt)
    {
        using var harness = Opened(prompt);
        harness.Type("foo bar");
        harness.Press(Key.CursorLeft);

        harness.Press(Key.U.WithCtrl);

        Assert.Equal("r", harness.StatusLine.Text);
        Assert.Equal(0, harness.StatusLine.Caret);
    }

    private static PromptHarness Opened(string prompt)
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(2, "two", [1]), CommitFactory.Commit(1, "one", []));
        var hash = CommitFactory.Id(2).ToString();
        var main = new GitRef("refs/heads/main", "main", RefKind.LocalBranch, hash, hash, null, null, null, false, false, null, null);
        var harness = new PromptHarness(graph, refs: [main]);
        switch (prompt)
        {
            case "graph search":
                harness.Press(new Key('/'));
                break;
            case "ref jump":
                harness.Press(new Key(' '));
                harness.Press(new Key('J'));
                break;
            case "quick jump":
                harness.Press(new Key(' '));
                harness.Press(new Key('j'));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(prompt), prompt, "Unknown prompt");
        }

        Assert.Equal(KeyMode.Insert, harness.Mode);

        return harness;
    }
}
