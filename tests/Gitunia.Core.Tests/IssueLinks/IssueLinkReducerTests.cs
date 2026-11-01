using System.Collections.Immutable;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.IssueLinks;
using Gitunia.Core.IssueTracker.Models;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.IssueLinks;

[Trait("Os", "Independent")]
public class IssueLinkReducerTests
{
    private static readonly ImmutableArray<IssueReference> Two =
    [
        new IssueReference(4, 2, "https://example.org/1"),
        new IssueReference(11, 2, "https://example.org/2"),
    ];

    [Fact]
    public void GivenALineWithTwoLinks_WhenOpeningWithoutANumber_ThenTheFirstOpens()
    {
        var effects = Reduce(new IssueLinkOpenRequested(Two, null, true));

        Assert.Equal("Opening https://example.org/1", Assert.IsType<PostStatusEffect>(effects[0]).Text);
        Assert.Equal("https://example.org/1", Assert.IsType<OpenUrlEffect>(effects[1]).Url);
    }

    [Fact]
    public void GivenALineWithTwoLinks_WhenOpeningWithANumber_ThenThatLinkOpens()
    {
        var effects = Reduce(new IssueLinkOpenRequested(Two, 2, true));

        Assert.Equal("https://example.org/2", Assert.IsType<OpenUrlEffect>(effects[1]).Url);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void GivenANumberOutsideTheLinks_WhenOpening_ThenNothingOpensAndTheWarningSaysSo(int number)
    {
        var effects = Reduce(new IssueLinkOpenRequested(Two, number, true));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects));
        Assert.Equal($"This line has 2 issue links: there is no link number {number}", post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenOneLink_WhenAskingForNumberTwo_ThenTheNounIsSingular()
    {
        var effects = Reduce(new IssueLinkOpenRequested([Two[0]], 2, true));

        Assert.Equal(
            "This line has 1 issue link: there is no link number 2",
            Assert.IsType<PostStatusEffect>(Assert.Single(effects)).Text);
    }

    [Fact]
    public void GivenALineWithoutLinks_WhenUsingTheOpenKey_ThenTheWarningSaysSo()
    {
        var effects = Reduce(new IssueLinkOpenRequested([], null, true));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects));
        Assert.Equal(IssueLinkReducer.NoLinkNotice, post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenALineWithoutLinks_WhenPressingEnter_ThenNothingHappens()
    {
        Assert.Empty(Reduce(new IssueLinkOpenRequested([], null, false)));
    }

    [Fact]
    public void GivenTheOpenerSucceeded_WhenFinished_ThenTheStatusSaysOpened()
    {
        var effects = Reduce(new IssueLinkOpenFinished("https://example.org/1", true, null));

        Assert.Equal("Opened https://example.org/1", Assert.IsType<PostStatusEffect>(Assert.Single(effects)).Text);
    }

    [Fact]
    public void GivenTheOpenerFailed_WhenFinished_ThenTheAddressIsCopiedAndTheWarningSaysSo()
    {
        var effects = Reduce(new IssueLinkOpenFinished("https://example.org/1", false, "could not start xdg-open"));

        var copy = Assert.IsType<CopyToClipboardEffect>(effects[0]);
        Assert.Equal("https://example.org/1", copy.Text);
        Assert.Equal("issue link", copy.Description);
        var post = Assert.IsType<PostStatusEffect>(effects[1]);
        Assert.Equal(
            "Could not open the link (could not start xdg-open); tried copying https://example.org/1 instead",
            post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    private static ImmutableArray<AppEffect> Reduce(IssueLinkCommand command) =>
        AppReducer.Reduce(AppState.Empty, command).Effects;
}
