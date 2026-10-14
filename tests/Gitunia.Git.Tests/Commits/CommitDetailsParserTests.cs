using System.Text;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitDetailsParserTests
{
    private static readonly string Hash = new('a', 40);
    private static readonly string Parent = new('b', 40);

    private static string Record(string parents, string message) =>
        $"{string.Join('\x1f', Hash, parents, "Ann", "ann@x.org", "2026-01-02T03:04:05+02:00", "Bob", "bob@x.org", "2026-01-03T00:00:00Z", message)}\0";

    private static CommitDetails? Parse(string text, List<string> warnings) => CommitDetailsParser.Parse(Encoding.UTF8.GetBytes(text), warnings);

    [Fact]
    public void GivenFullRecord_WhenParsing_ThenFieldsAndOffsetsSurvive()
    {
        var warnings = new List<string>();

        var details = Parse(Record(Parent, "Subject line\n\nBody line 1\nBody ünï 日本\n"), warnings);

        Assert.NotNull(details);
        Assert.Equal(Hash, details.Id.ToString());
        Assert.Equal(Parent, Assert.Single(details.Parents).ToString());
        Assert.Equal("Ann", details.AuthorName);
        Assert.Equal(TimeSpan.FromHours(2), details.AuthorTime.Offset);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)), details.AuthorTime);
        Assert.True(details.HasDistinctCommitter);
        Assert.Equal("Subject line\n\nBody line 1\nBody ünï 日本", details.Message);
        Assert.Equal("Subject line", details.Subject);
        Assert.Empty(warnings);
    }

    [Fact]
    public void GivenRootCommitWithoutParents_WhenParsing_ThenParentsEmpty()
    {
        var details = Parse(Record(string.Empty, "root"), []);

        Assert.Empty(details!.Parents);
    }

    [Fact]
    public void GivenMessageWithSeparatorLikeText_WhenParsing_ThenMessageIsKeptWhole()
    {
        var details = Parse(Record(Parent, "a\x1f b"), []);

        Assert.Equal("a\x1f b", details!.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("only\x1fthree\x1ffields\0")]
    public void GivenTruncatedOrEmptyOutput_WhenParsing_ThenNullWithWarning(string text)
    {
        var warnings = new List<string>();

        Assert.Null(Parse(text, warnings));
        Assert.Single(warnings);
    }

    [Fact]
    public void GivenInvalidTime_WhenParsing_ThenWarnsAndFallsBackToEpoch()
    {
        var warnings = new List<string>();
        var text = $"{string.Join('\x1f', Hash, string.Empty, "A", "a@b", "nonsense", "A", "a@b", "nonsense", "m")}\0";

        var details = Parse(text, warnings);

        Assert.Equal(DateTimeOffset.UnixEpoch, details!.AuthorTime);
        Assert.Equal(2, warnings.Count);
    }
}
