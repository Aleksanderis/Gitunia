using Gitunia.Core.IssueTracker;
using Gitunia.Git.IssueTracker.Models;
using Xunit;

namespace Gitunia.Core.Tests.IssueTracker;

public class IssueTrackerLinksTests
{
    private static readonly IssueTrackerRule GitHub = new("gh", "#(\\d+)", "https://example.org/issues/$1");

    [Fact]
    public void GivenARule_WhenFinding_ThenEveryMatchGetsItsUrl()
    {
        var links = new IssueTrackerLinks([GitHub]);

        var found = links.Find("Fix #12, closes #7");

        Assert.Equal(
            ["4,3,https://example.org/issues/12", "16,2,https://example.org/issues/7"],
            found.Select(reference => $"{reference.Start},{reference.Length},{reference.Url}"));
    }

    [Fact]
    public void GivenSeveralRules_WhenFinding_ThenEachSectionMakesLinks()
    {
        var jira = new IssueTrackerRule("jira", "([A-Z]+-\\d+)", "https://jira.example.org/browse/$1");
        var links = new IssueTrackerLinks([GitHub, jira]);

        var found = links.Find("ABC-5 and #3");

        Assert.Equal(
            ["https://jira.example.org/browse/ABC-5", "https://example.org/issues/3"],
            found.Select(reference => reference.Url));
    }

    [Fact]
    public void GivenOverlappingRules_WhenFinding_ThenTheEarlierRuleWins()
    {
        var first = new IssueTrackerRule("a", "(#\\d+)", "https://a.example.org/$1");
        var second = new IssueTrackerRule("b", "(\\d+)", "https://b.example.org/$1");
        var links = new IssueTrackerLinks([first, second]);

        var found = links.Find("#12");

        Assert.Equal(["https://a.example.org/#12"], found.Select(reference => reference.Url));
    }

    [Fact]
    public void GivenARegexWithoutAGroup_WhenFinding_ThenTheWholeMatchIsTheReference()
    {
        var links = new IssueTrackerLinks([new IssueTrackerRule("x", "BUG\\d+", "https://example.org/$1")]);

        Assert.Equal(["https://example.org/BUG42"], links.Find("see BUG42").Select(reference => reference.Url));
    }

    [Fact]
    public void GivenACaptureWithSpecialCharacters_WhenFinding_ThenTheUrlIsEncoded()
    {
        var links = new IssueTrackerLinks([new IssueTrackerRule("x", "<([^>]+)>", "https://example.org/?q=$1")]);

        var found = links.Find("<a b\"é>");

        Assert.Equal(["https://example.org/?q=a%20b%22%C3%A9"], found.Select(reference => reference.Url));
    }

    [Theory]
    [InlineData("javascript:alert($1)")]
    [InlineData("file:///etc/$1")]
    [InlineData("ftp://example.org/$1")]
    public void GivenAUrlThatIsNotHttp_WhenFinding_ThenNoLinkIsMade(string template)
    {
        var links = new IssueTrackerLinks([new IssueTrackerRule("x", "#(\\d+)", template)]);

        Assert.Empty(links.Find("#1"));
    }

    [Fact]
    public void GivenAnInvalidRegex_WhenCreating_ThenThatRuleIsSkippedAndOthersWork()
    {
        var links = new IssueTrackerLinks([new IssueTrackerRule("bad", "([", "https://x.org/$1"), GitHub]);

        Assert.Single(links.Find("#1"));
    }

    [Fact]
    public void GivenARunawayRegex_WhenFinding_ThenItMakesNoLinksInsteadOfHanging()
    {
        var links = new IssueTrackerLinks([new IssueTrackerRule("slow", "^(a+)+$", "https://x.org/$1")]);

        Assert.Empty(links.Find(new string('a', 40) + "!"));
    }

    [Fact]
    public void GivenNoRulesOrNoText_WhenFinding_ThenThereAreNoReferences()
    {
        Assert.True(IssueTrackerLinks.None.IsEmpty);
        Assert.Empty(IssueTrackerLinks.None.Find("#1"));
        Assert.Empty(new IssueTrackerLinks([GitHub]).Find(string.Empty));
    }
}
