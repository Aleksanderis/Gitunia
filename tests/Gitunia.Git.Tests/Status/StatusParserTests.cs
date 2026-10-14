using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Git.Tests.Status;

public class StatusParserTests
{
    private const string Hash = "1111111111111111111111111111111111111111";
    private const string OtherHash = "2222222222222222222222222222222222222222";

    private static string Output(params string[] segments) => string.Concat(segments.Select(segment => segment + "\0"));

    private static string Ordinary(string codes, string path, string submodule = "N...") =>
        $"1 {codes} {submodule} 100644 100644 100644 {Hash} {OtherHash} {path}";

    [Fact]
    public void GivenBranchWithUpstream_WhenParsing_ThenHeaderIsRead()
    {
        var output = Output(
            "# branch.oid " + Hash,
            "# branch.head main",
            "# branch.upstream origin/main",
            "# branch.ab +3 -12");

        var branch = StatusParser.Parse(output).Branch;

        Assert.Equal(new HeadState(HeadKind.Branch, "main", Hash), branch.Head);
        Assert.Equal("origin/main", branch.UpstreamName);
        Assert.Equal(3, branch.Ahead);
        Assert.Equal(12, branch.Behind);
    }

    [Fact]
    public void GivenBranchWithoutUpstream_WhenParsing_ThenNoCounts()
    {
        var output = Output("# branch.oid " + Hash, "# branch.head main");

        var branch = StatusParser.Parse(output).Branch;

        Assert.Null(branch.UpstreamName);
        Assert.Null(branch.Ahead);
        Assert.Null(branch.Behind);
    }

    [Fact]
    public void GivenDetachedHead_WhenParsing_ThenNoBranchName()
    {
        var output = Output("# branch.oid " + Hash, "# branch.head (detached)");

        var head = StatusParser.Parse(output).Branch.Head;

        Assert.Equal(new HeadState(HeadKind.Detached, null, Hash), head);
    }

    [Fact]
    public void GivenUnbornBranch_WhenParsing_ThenNoCommit()
    {
        var output = Output("# branch.oid (initial)", "# branch.head main");

        var head = StatusParser.Parse(output).Branch.Head;

        Assert.Equal(new HeadState(HeadKind.Unborn, "main", null), head);
    }

    [Fact]
    public void GivenUpstreamThatIsGone_WhenParsing_ThenUpstreamKeptWithoutCounts()
    {
        var output = Output("# branch.oid " + Hash, "# branch.head main", "# branch.upstream origin/main");

        var branch = StatusParser.Parse(output).Branch;

        Assert.Equal("origin/main", branch.UpstreamName);
        Assert.Null(branch.Ahead);
    }

    [Fact]
    public void GivenStagedAndUnstagedChanges_WhenParsing_ThenBothSidesAreRead()
    {
        var output = Output(Ordinary("MM", "both.txt"), Ordinary("A.", "added.txt"), Ordinary(".D", "deleted.txt"), Ordinary("T.", "typed.txt"));

        var entries = StatusParser.Parse(output).Entries;

        Assert.Equal(4, entries.Count);
        Assert.All(entries, entry => Assert.Equal(StatusEntryKind.Ordinary, entry.Kind));
        Assert.Equal((FileChangeKind.Modified, FileChangeKind.Modified), (entries[0].IndexChange, entries[0].WorktreeChange));
        Assert.True(entries[0].IsStaged);
        Assert.True(entries[0].IsUnstaged);
        Assert.Equal((FileChangeKind.Added, FileChangeKind.Unmodified), (entries[1].IndexChange, entries[1].WorktreeChange));
        Assert.False(entries[1].IsUnstaged);
        Assert.Equal((FileChangeKind.Unmodified, FileChangeKind.Deleted), (entries[2].IndexChange, entries[2].WorktreeChange));
        Assert.False(entries[2].IsStaged);
        Assert.Equal(FileChangeKind.TypeChanged, entries[3].IndexChange);
    }

    [Fact]
    public void GivenPathWithSpacesAndUnicode_WhenParsing_ThenPathIsExact()
    {
        var output = Output(Ordinary(".M", "dir with space/ünï cødé  file.txt"), "? new  file ✓.txt");

        var entries = StatusParser.Parse(output).Entries;

        Assert.Equal("dir with space/ünï cødé  file.txt", entries[0].Path);
        Assert.Equal("new  file ✓.txt", entries[1].Path);
    }

    [Fact]
    public void GivenPathWithNewlineAndQuote_WhenParsing_ThenPathIsExact()
    {
        var output = Output(Ordinary(".M", "line\nbreak \"quoted\".txt"));

        var entry = Assert.Single(StatusParser.Parse(output).Entries);

        Assert.Equal("line\nbreak \"quoted\".txt", entry.Path);
    }

    [Fact]
    public void GivenRename_WhenParsing_ThenSourceFollowsAsSeparateField()
    {
        var output = Output($"2 R. N... 100644 100644 100644 {Hash} {OtherHash} R87 new name.txt", "old name.txt", Ordinary(".M", "after.txt"));

        var result = StatusParser.Parse(output);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Entries.Count);
        var rename = result.Entries[0];
        Assert.Equal(StatusEntryKind.Renamed, rename.Kind);
        Assert.Equal("new name.txt", rename.Path);
        Assert.Equal("old name.txt", rename.OriginalPath);
        Assert.Equal(87, rename.Similarity);
        Assert.Equal(FileChangeKind.Renamed, rename.IndexChange);
        Assert.Equal("after.txt", result.Entries[1].Path);
    }

    [Fact]
    public void GivenCopy_WhenParsing_ThenKindIsCopied()
    {
        var output = Output($"2 C. N... 100644 100644 100644 {Hash} {OtherHash} C75 copy.txt", "orig.txt");

        var entry = Assert.Single(StatusParser.Parse(output).Entries);

        Assert.Equal(StatusEntryKind.Copied, entry.Kind);
        Assert.Equal("orig.txt", entry.OriginalPath);
        Assert.Equal(75, entry.Similarity);
    }

    [Fact]
    public void GivenRenameWithMissingSource_WhenParsing_ThenSkippedWithWarning()
    {
        var output = $"2 R. N... 100644 100644 100644 {Hash} {OtherHash} R100 new.txt\0";

        var result = StatusParser.Parse(output);

        Assert.Empty(result.Entries);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("DD", ConflictKind.BothDeleted)]
    [InlineData("AU", ConflictKind.AddedByUs)]
    [InlineData("UD", ConflictKind.DeletedByThem)]
    [InlineData("UA", ConflictKind.AddedByThem)]
    [InlineData("DU", ConflictKind.DeletedByUs)]
    [InlineData("AA", ConflictKind.BothAdded)]
    [InlineData("UU", ConflictKind.BothModified)]
    public void GivenUnmergedRecord_WhenParsing_ThenConflictKindMatchesCode(string codes, ConflictKind expected)
    {
        var output = Output($"u {codes} N... 100644 100644 100644 100644 {Hash} {OtherHash} {Hash} conflict file.txt");

        var result = StatusParser.Parse(output);

        var entry = Assert.Single(result.Entries);
        Assert.Empty(result.Warnings);
        Assert.Equal(StatusEntryKind.Unmerged, entry.Kind);
        Assert.Equal(expected, entry.Conflict);
        Assert.Equal("conflict file.txt", entry.Path);
        Assert.False(entry.IsStaged);
    }

    [Fact]
    public void GivenUntrackedAndIgnored_WhenParsing_ThenKindsAreSet()
    {
        var output = Output("? untracked.txt", "! build/output.o");

        var entries = StatusParser.Parse(output).Entries;

        Assert.Equal(StatusEntryKind.Untracked, entries[0].Kind);
        Assert.Equal(StatusEntryKind.Ignored, entries[1].Kind);
        Assert.Equal("build/output.o", entries[1].Path);
        Assert.False(entries[0].IsStaged);
        Assert.False(entries[0].IsUnstaged);
    }

    [Fact]
    public void GivenSubmoduleWithChanges_WhenParsing_ThenStateIsRead()
    {
        var output = Output(Ordinary(".M", "lib", "SCMU"), Ordinary(".M", "other", "S.M."), Ordinary(".M", "plain"));

        var entries = StatusParser.Parse(output).Entries;

        Assert.Equal(new SubmoduleState(true, true, true), entries[0].Submodule);
        Assert.Equal(new SubmoduleState(false, true, false), entries[1].Submodule);
        Assert.Null(entries[2].Submodule);
    }

    [Fact]
    public void GivenUnknownRecordsAndHeaders_WhenParsing_ThenWarnedAndRestParsed()
    {
        var output = Output("# branch.oid " + Hash, "# branch.head main", "# future.header 1", "# stash 2", "x something", Ordinary(".M", "ok.txt"));

        var result = StatusParser.Parse(output);

        Assert.Equal(2, result.Warnings.Count);
        Assert.Equal("ok.txt", Assert.Single(result.Entries).Path);
    }

    [Fact]
    public void GivenMalformedRecords_WhenParsing_ThenNothingThrowsAndWarningsAreReported()
    {
        var output = Output("1 MM N...", "1 XX N... 100644 100644 100644 a b path.txt", "u UU", "?", "# branch.ab nonsense", Ordinary(".M", "ok.txt"));

        var result = StatusParser.Parse(output);

        Assert.Equal("ok.txt", Assert.Single(result.Entries).Path);
        Assert.True(result.Warnings.Count >= 5);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenNoEntries()
    {
        var result = StatusParser.Parse(string.Empty);

        Assert.Empty(result.Entries);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void GivenMissingFinalTerminator_WhenParsing_ThenLastEntryIsKept()
    {
        var result = StatusParser.Parse("? last.txt");

        Assert.Equal("last.txt", Assert.Single(result.Entries).Path);
    }
}
