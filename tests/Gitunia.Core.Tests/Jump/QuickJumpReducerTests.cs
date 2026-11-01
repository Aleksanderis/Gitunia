using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.Core.Tests.Jump;

[Trait("Os", "Independent")]
public class QuickJumpReducerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly QuickJumpSession Open = new(Tab, PaneKind.Graph, string.Empty);

    [Fact]
    public void GivenAListWithATarget_WhenOpening_ThenThePromptOpensForThatTabAndPane()
    {
        var transition = QuickJumpReducer.Reduce(null, new QuickJumpOpenRequested(Tab, PaneKind.Diff, true, null));

        Assert.Equal(new QuickJumpSession(Tab, PaneKind.Diff, string.Empty), transition.Session);
        Assert.Empty(transition.Effects);
    }

    [Fact]
    public void GivenAPaneWithoutATarget_WhenOpening_ThenItSaysSoAndNothingOpens()
    {
        var transition = QuickJumpReducer.Reduce(null, new QuickJumpOpenRequested(Tab, PaneKind.Sidebar, false, null));

        Assert.Null(transition.Session);
        Assert.Equal(QuickJumpReducer.UnavailableNotice, Text(transition));
    }

    [Fact]
    public void GivenNoTab_WhenOpening_ThenItSaysSo()
    {
        var transition = QuickJumpReducer.Reduce(null, new QuickJumpOpenRequested(null, PaneKind.Graph, true, null));

        Assert.Null(transition.Session);
        Assert.Equal(QuickJumpReducer.UnavailableNotice, Text(transition));
    }

    [Fact]
    public void GivenAListThatCannotBeJumpedInRightNow_WhenOpening_ThenTheReasonIsTheNotice()
    {
        var transition = QuickJumpReducer.Reduce(null, new QuickJumpOpenRequested(Tab, PaneKind.Graph, true, "Not here"));

        Assert.Null(transition.Session);
        Assert.Equal("Not here", Text(transition));
    }

    [Fact]
    public void GivenAPrintableKeyThatIsNoLabel_WhenTyped_ThenTheTextGrows()
    {
        var labels = ImmutableArray.Create(new JumpLabel(4, 0, 1, 'q'));

        var transition = QuickJumpReducer.Reduce(Open, new QuickJumpKeyPressed(new KeyEvent("f"), labels));

        Assert.Equal("f", transition.Session!.Text);
        Assert.Empty(transition.Effects);
    }

    [Fact]
    public void GivenALabelKey_WhenTyped_ThenThePromptClosesAndTheRowIsJumpedTo()
    {
        var labels = ImmutableArray.Create(new JumpLabel(4, 0, 1, 'q'));
        var typing = Open with { Text = "f" };

        var transition = QuickJumpReducer.Reduce(typing, new QuickJumpKeyPressed(new KeyEvent("q"), labels));

        Assert.Null(transition.Session);
        Assert.Equal(new JumpToRowEffect(4), Assert.Single(transition.Effects));
    }

    [Fact]
    public void GivenMatches_WhenAccepting_ThenTheFirstLabelIsTheJump()
    {
        var labels = ImmutableArray.Create(new JumpLabel(2, 0, 1, 'q'), new JumpLabel(5, 0, 1, 'w'));

        var transition = QuickJumpReducer.Reduce(Open, new QuickJumpAccepted(labels));

        Assert.Null(transition.Session);
        Assert.Equal(new JumpToRowEffect(2), Assert.Single(transition.Effects));
    }

    [Fact]
    public void GivenNoMatch_WhenAccepting_ThenTheNoticeSaysSoAndThereIsNoJump()
    {
        var transition = QuickJumpReducer.Reduce(Open, new QuickJumpAccepted([]));

        Assert.Null(transition.Session);
        Assert.Equal(QuickJumpReducer.NoMatchNotice, Text(transition));
        Assert.DoesNotContain(transition.Effects, effect => effect is JumpToRowEffect);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenClosed_ThenItEndsWithoutEffects()
    {
        var transition = QuickJumpReducer.Reduce(Open, new QuickJumpClosed());

        Assert.Null(transition.Session);
        Assert.Empty(transition.Effects);
    }

    [Fact]
    public void GivenNoPrompt_WhenAKeyArrives_ThenNothingHappens()
    {
        var transition = QuickJumpReducer.Reduce(null, new QuickJumpKeyPressed(new KeyEvent("a"), []));

        Assert.Null(transition.Session);
        Assert.Empty(transition.Effects);
    }

    [Theory]
    [InlineData("", 0, "jump _  type text, then press a label")]
    [InlineData("fix", 0, "jump fix_  no match in view")]
    [InlineData("fix", 3, "jump fix_  3 in view")]
    public void GivenTheTypedText_WhenDrawingTheStatusLine_ThenItSaysWhatIsInView(string text, int matches, string line)
    {
        Assert.Equal(line, QuickJumpReducer.StatusLine(text, matches));
    }

    private static string Text(QuickJumpTransition transition) =>
        Assert.IsType<PostStatusEffect>(Assert.Single(transition.Effects)).Text;
}
