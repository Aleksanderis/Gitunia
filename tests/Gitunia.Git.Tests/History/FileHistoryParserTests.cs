using Gitunia.Git.Commits.Models;
using Gitunia.Git.History;
using Xunit;

namespace Gitunia.Git.Tests.History;

public class FileHistoryParserTests
{
    private const string Unit = "\u001f";
    private const string Record = "\u001e";
    private const string Nul = "\0";

    [Fact]
    public void GivenAModifiedAndARenamedCommit_WhenParsing_ThenTheOlderCommitKeepsTheOldName()
    {
        var output =
            $"{Record}aaa{Unit}Ann{Unit}2026-11-02{Unit}edit new{Nul}M{Nul}new name.txt{Nul}"
            + $"{Record}bbb{Unit}Bob{Unit}2026-11-01{Unit}rename{Nul}R100{Nul}old ü.txt{Nul}new name.txt{Nul}"
            + $"{Record}ccc{Unit}Cy{Unit}2026-10-31{Unit}add{Nul}A{Nul}old ü.txt{Nul}";

        var result = FileHistoryParser.Parse(output, "new name.txt");

        Assert.Empty(result.Warnings);
        Assert.Equal(["aaa", "bbb", "ccc"], result.Items.Select(entry => entry.Hash));
        Assert.Equal(["new name.txt", "new name.txt", "old ü.txt"], result.Items.Select(entry => entry.Path));
        Assert.Equal([null, "old ü.txt", null], result.Items.Select(entry => entry.OldPath));
        Assert.Equal(
            [FileChangeKind.Modified, FileChangeKind.Renamed, FileChangeKind.Added],
            result.Items.Select(entry => entry.Kind));
        Assert.Equal("rename", result.Items[1].Subject);
        Assert.Equal("Bob", result.Items[1].AuthorName);
        Assert.Equal("2026-11-01", result.Items[1].Date);
    }

    [Fact]
    public void GivenACommitWithoutAStatusLine_WhenParsing_ThenItTakesTheFollowedPath()
    {
        var output = $"{Record}aaa{Unit}Ann{Unit}2026-11-02{Unit}merge{Nul}";

        var result = FileHistoryParser.Parse(output, "kept.txt");

        var entry = Assert.Single(result.Items);
        Assert.Equal("kept.txt", entry.Path);
        Assert.Equal(FileChangeKind.Modified, entry.Kind);
    }

    [Fact]
    public void GivenARecordWithTooFewFields_WhenParsing_ThenItIsSkippedWithAWarning()
    {
        var output = $"{Record}aaa{Unit}Ann{Nul}M{Nul}x.txt{Nul}";

        var result = FileHistoryParser.Parse(output, "x.txt");

        Assert.Empty(result.Items);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenThereAreNoEntries()
    {
        var result = FileHistoryParser.Parse(string.Empty, "x.txt");

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }
}
