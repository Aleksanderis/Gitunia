using Gitunia.Core.Keymap;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Tests.Rendering;
using Gitunia.Tui.Tests.Rendering.Graph;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Search;

public class RefJumpControllerTests
{
    private static GitRef Ref(string name, RefKind kind, int commit)
    {
        var hash = CommitFactory.Id(commit).ToString();
        var prefix = kind == RefKind.Tag ? "refs/tags/" : "refs/heads/";
        return new GitRef(prefix + name, name, kind, hash, hash, null, null, null, false, false, null, null);
    }

    private static Gitunia.Core.Graph.CommitGraph Graph() => CommitFactory.Graph(
        CommitFactory.Commit(4, "four", [3]),
        CommitFactory.Commit(3, "three", [2]),
        CommitFactory.Commit(2, "two", [1]),
        CommitFactory.Commit(1, "one", []));

    private static GitRef[] Refs() =>
    [
        Ref("main", RefKind.LocalBranch, 4),
        Ref("release/1.0", RefKind.LocalBranch, 2),
        Ref("v0.1", RefKind.Tag, 1),
    ];

    [Fact]
    public void GivenGr_WhenTypingAFuzzyNameAndAccepting_ThenTheRefsCommitIsSelected()
    {
        using var harness = new PromptHarness(Graph(), refs: Refs());

        harness.Press(new Key('g'));
        harness.Press(new Key('r'));
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("rel");
        Assert.StartsWith("gr rel_  release/1.0 (1/1)", harness.StatusLine, StringComparison.Ordinal);
        harness.Press(Key.Enter);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(2, harness.SelectedRow);
    }

    [Fact]
    public void GivenSeveralCandidates_WhenMovingDownAndAccepting_ThenTheHighlightedOneIsUsed()
    {
        using var harness = new PromptHarness(Graph(), refs: Refs());
        harness.Press(new Key('g'));
        harness.Press(new Key('r'));

        harness.Press(Key.CursorDown);
        harness.Press(Key.Enter);

        Assert.Equal(2, harness.SelectedRow);
    }

    [Fact]
    public void GivenHashPrefix_WhenAccepting_ThenTheCommitIsSelected()
    {
        using var harness = new PromptHarness(Graph(), refs: Refs());
        harness.Press(new Key('g'));
        harness.Press(new Key('r'));

        harness.Type("0000003");
        harness.Press(Key.Enter);

        Assert.Equal(1, harness.SelectedRow);
    }

    [Fact]
    public void GivenNoCandidate_WhenAccepting_ThenANoticeIsShownAndSelectionStays()
    {
        using var harness = new PromptHarness(Graph(), refs: Refs()) { SelectedRow = 2 };
        harness.Press(new Key('g'));
        harness.Press(new Key('r'));

        harness.Type("zzz");
        Assert.Contains("no matching ref or commit", harness.StatusLine, StringComparison.Ordinal);
        harness.Press(Key.Enter);

        Assert.Equal(2, harness.SelectedRow);
        Assert.Equal("No matching ref or commit", harness.Notice);
    }

    [Fact]
    public void GivenOpenPrompt_WhenPressingEscape_ThenItClosesWithoutMoving()
    {
        using var harness = new PromptHarness(Graph(), refs: Refs()) { SelectedRow = 3 };
        harness.Press(new Key('g'));
        harness.Press(new Key('r'));

        harness.Press(Key.Esc);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(3, harness.SelectedRow);
        Assert.Equal(string.Empty, harness.StatusLine);
    }

    [Fact]
    public void GivenRefOutsideTheLoadedRows_WhenAcceptingWhileLoading_ThenItJumpsOnceTheCommitArrives()
    {
        var loaded = CommitFactory.Graph(CommitFactory.Commit(4, "four", [3]), CommitFactory.Commit(3, "three", [2]));
        using var harness = new PromptHarness(loaded, isLoading: true, refs: Refs());
        harness.Press(new Key('g'));
        harness.Press(new Key('r'));
        harness.Type("v0");
        harness.Press(Key.Enter);
        Assert.Contains("Loading", harness.Notice, StringComparison.Ordinal);

        var rest = CommitFactory.Graph(CommitFactory.Commit(2, "two", [1]), CommitFactory.Commit(1, "one", []));
        harness.Session.Append([rest[0], rest[1]]);

        Assert.Equal(3, harness.SelectedRow);
    }
}
