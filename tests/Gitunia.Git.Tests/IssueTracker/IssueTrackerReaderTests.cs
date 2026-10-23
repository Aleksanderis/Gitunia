using Gitunia.Git.IssueTracker;
using Gitunia.Git.IssueTracker.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.IssueTracker;

public class IssueTrackerReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAFileWithTwoSections_WhenReading_ThenBothRulesComeInFileOrder()
    {
        using var testRepository = TestRepositories.Linear(1);
        WriteFile(
            testRepository,
            "[issuetracker \"gh.main\"]\n\tregex = \"#(\\\\d+)\"\n\turl = https://example.org/$1\n"
            + "[issuetracker \"jira\"]\n\turl = https://jira.example.org/$1\n\tregex = ([A-Z]+-\\\\d+)\n");

        var rules = await ReadAsync(testRepository);

        Assert.Equal(["gh.main", "jira"], rules.Select(rule => rule.Name));
        Assert.Equal("#(\\d+)", rules[0].Pattern);
        Assert.Equal("https://jira.example.org/$1", rules[1].UrlTemplate);
    }

    [Fact]
    public async Task GivenNoFile_WhenReading_ThenThereAreNoRules()
    {
        using var testRepository = TestRepositories.Linear(1);

        Assert.Empty(await ReadAsync(testRepository));
    }

    [Fact]
    public async Task GivenAnEmptyFile_WhenReading_ThenThereAreNoRules()
    {
        using var testRepository = TestRepositories.Linear(1);
        WriteFile(testRepository, string.Empty);

        Assert.Empty(await ReadAsync(testRepository));
    }

    [Fact]
    public async Task GivenAMalformedFile_WhenReading_ThenThereAreNoRulesAndNoError()
    {
        using var testRepository = TestRepositories.Linear(1);
        WriteFile(testRepository, "[issuetracker \"x\"\nthis is = = not [ config\n");

        Assert.Empty(await ReadAsync(testRepository));
    }

    [Fact]
    public async Task GivenASectionWithoutAUrl_WhenReading_ThenItIsLeftOut()
    {
        using var testRepository = TestRepositories.Linear(1);
        WriteFile(testRepository, "[issuetracker \"a\"]\n\tregex = x\n[core]\n\tfoo = bar\n");

        Assert.Empty(await ReadAsync(testRepository));
    }

    [Fact]
    public async Task GivenASectionWithoutAName_WhenReading_ThenItIsIgnoredWithoutAnError()
    {
        using var testRepository = TestRepositories.Linear(1);
        WriteFile(testRepository, "[issuetracker]\n\tregex = x\n\turl = https://a/$1\n");

        Assert.Empty(await ReadAsync(testRepository));
    }

    [Fact]
    public void GivenKeysWithoutASubsectionName_WhenParsing_ThenTheyAreIgnored()
    {
        Assert.Empty(IssueTrackerParser.Parse("issuetracker.regex\nx\0issuetracker.url\nhttps://a/$1\0"));
    }

    [Fact]
    public void GivenARepeatedKey_WhenParsing_ThenTheLastValueWins()
    {
        var rules = IssueTrackerParser.Parse(
            "issuetracker.a.regex\none\0issuetracker.a.url\nhttps://x/\0issuetracker.a.regex\ntwo\0");

        Assert.Equal("two", Assert.Single(rules).Pattern);
    }

    private static void WriteFile(TestRepository testRepository, string text) =>
        File.WriteAllText(Path.Combine(testRepository.WorkingTreePath, IssueTrackerReader.FileName), text);

    private static async Task<IReadOnlyList<IssueTrackerRule>> ReadAsync(
        TestRepository testRepository)
    {
        var runner = testRepository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(testRepository.WorkingTreePath, Token);

        return await new IssueTrackerReader(runner).ReadAsync(location, Token);
    }
}
