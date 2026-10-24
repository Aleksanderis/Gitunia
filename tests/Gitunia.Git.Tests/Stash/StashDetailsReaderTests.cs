using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Stash;

public class StashDetailsReaderTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly StashReader Reader = new(Runner);
    private static readonly StashWriter Writer = new(Runner);
    private static readonly StashDetailsReader Details = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenTrackedEditsOnly_WhenReadingFiles_ThenOnlyTrackedFilesAndNamesMatchGitStashShow()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("file.txt", "edited\n");
        repository.Git("stash", "push", "-q");
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var files = await Details.ReadFilesAsync(location, entry, null, Token);

        Assert.NotNull(files);
        Assert.Equal(entry.CommitHash, files.CommitHash);
        var file = Assert.Single(files.Files);
        Assert.Equal(StashFileOrigin.Tracked, file.Origin);
        Assert.Equal(["M\tfile.txt"], Describe(files));
        Assert.Equal(NameStatus(repository), Describe(files));
    }

    [Fact]
    public async Task GivenUntrackedFiles_WhenReadingFiles_ThenTheyFollowTheTrackedOnesMarkedUntracked()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var files = await Details.ReadFilesAsync(location, entry, null, Token);

        Assert.NotNull(files);
        Assert.Equal(2, files.Files.Count);
        Assert.Equal(StashFileOrigin.Tracked, files.Files[0].Origin);
        Assert.Equal("file.txt", files.Files[0].Change.Path);
        Assert.Equal(StashFileOrigin.Untracked, files.Files[1].Origin);
        Assert.Equal("untracked.txt", files.Files[1].Change.Path);
        Assert.Equal(FileChangeKind.Added, files.Files[1].Change.Kind);
    }

    [Fact]
    public async Task GivenOnlyUntrackedFiles_WhenReadingFiles_ThenAllAreUntracked()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("only.txt", "x\n");
        repository.Git("stash", "push", "-q", "--include-untracked");
        var location = await LocateAsync(repository);

        var files = await Details.ReadFilesAsync(location, await TopAsync(location), null, Token);

        Assert.NotNull(files);
        var file = Assert.Single(files.Files);
        Assert.Equal(StashFileOrigin.Untracked, file.Origin);
        Assert.Equal("only.txt", file.Change.Path);
    }

    [Fact]
    public async Task GivenRenameAndDelete_WhenReadingFiles_ThenStatusesMatchGitStashShow()
    {
        using var repository = TestRepositories.Linear(2);
        repository.Commit(
            "Add files",
            new TestFile("old-name.txt", "line one\nline two\nline three\nline four\nline five\n"),
            new TestFile("gone.txt", "bye\n"));
        repository.Git("mv", "old-name.txt", "new-name.txt");
        repository.Delete("gone.txt");
        repository.Git("stash", "push", "-q");
        var location = await LocateAsync(repository);

        var files = await Details.ReadFilesAsync(location, await TopAsync(location), null, Token);

        Assert.NotNull(files);
        Assert.Equal(NameStatus(repository), Describe(files));
        Assert.Contains(
            files.Files,
            file => file.Change.Kind == FileChangeKind.Renamed && file.Change.OldPath == "old-name.txt");
        Assert.Contains(
            files.Files,
            file => file.Change.Kind == FileChangeKind.Deleted && file.Change.Path == "gone.txt");
    }

    [Fact]
    public async Task GivenStagedCopyOfModifiedFile_WhenReadingFilesAndDiff_ThenItIsAnAdditionLikeGitStashShow()
    {
        using var repository = TestRepositories.Linear(2);
        repository.Commit("Add", new TestFile("source.txt", Rows(changed: false)));
        repository.WriteFile("source.txt", Rows(changed: true));
        repository.WriteFile("copy.txt", Rows(changed: false));
        repository.Git("add", "copy.txt");
        repository.Git("stash", "push", "-q");
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var files = await Details.ReadFilesAsync(location, entry, null, Token);
        var diff = await ReadDiffAsync(location, entry, StashFileOrigin.Tracked);

        Assert.NotNull(files);
        Assert.Equal(NameStatus(repository), Describe(files));
        Assert.DoesNotContain(files.Files, file => file.Change.Kind == FileChangeKind.Copied);
        Assert.DoesNotContain(diff, file => file.Status == DiffFileStatus.Copied);
        Assert.Equal(PatchBody(repository.Git("stash", "show", "-p", "--no-color")), PatchBody(diff));
    }

    [Fact]
    public async Task GivenDroppedStashWhoseCommitExists_WhenReadingDiff_ThenItStillStreams()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);
        await Writer.DropAsync(location, entry, Token);

        var diff = await ReadDiffAsync(location, entry, StashFileOrigin.Tracked);

        Assert.Equal("file.txt", Assert.Single(diff).Path);
    }

    [Fact]
    public async Task GivenStashDroppedMeanwhile_WhenReadingFiles_ThenResultIsNull()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);
        await Writer.DropAsync(location, entry, Token);

        var files = await Details.ReadFilesAsync(location, entry, null, Token);

        Assert.Null(files);
    }

    [Fact]
    public async Task GivenNewerStashPushed_WhenReadingOlderEntry_ThenItsOwnFilesAreReturned()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("file.txt", "first\n");
        repository.Git("stash", "push", "-q");
        var location = await LocateAsync(repository);
        var older = await TopAsync(location);
        repository.WriteFile("file.txt", "second\n");
        repository.WriteFile("extra.txt", "x\n");
        repository.Git("add", "extra.txt");
        repository.Git("stash", "push", "-q");

        var files = await Details.ReadFilesAsync(location, older, null, Token);

        Assert.NotNull(files);
        Assert.Equal(["M\tfile.txt"], Describe(files));
    }

    [Fact]
    public async Task GivenTrackedEdits_WhenReadingDiff_ThenTextMatchesGitStashShowPatch()
    {
        using var repository = TestRepositories.Linear(2);
        repository.Commit("Add", new TestFile("multi.txt", Rows(changed: false)));
        repository.WriteFile("file.txt", "edited\n");
        repository.WriteFile("multi.txt", Rows(changed: true));
        repository.Git("stash", "push", "-q");
        var location = await LocateAsync(repository);

        var diff = await ReadDiffAsync(location, await TopAsync(location), StashFileOrigin.Tracked);

        Assert.Equal(2, diff.Count);
        Assert.Equal(PatchBody(repository.Git("stash", "show", "-p", "--no-color")), PatchBody(diff));
    }

    [Fact]
    public async Task GivenUntrackedFiles_WhenReadingUntrackedDiff_ThenTheyAreAdditionsAndTrackedDiffExcludesThem()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var untracked = await ReadDiffAsync(location, entry, StashFileOrigin.Untracked);
        var tracked = await ReadDiffAsync(location, entry, StashFileOrigin.Tracked);

        var added = Assert.Single(untracked);
        Assert.Equal("untracked.txt", added.Path);
        Assert.Equal(DiffFileStatus.Added, added.Status);
        Assert.Equal("untracked", Assert.Single(Assert.Single(added.Hunks).Lines).Text);
        Assert.Equal("file.txt", Assert.Single(tracked).Path);
    }

    [Fact]
    public async Task GivenStashWithoutUntracked_WhenReadingUntrackedDiff_ThenNothingIsStreamed()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("file.txt", "edited\n");
        repository.Git("stash", "push", "-q");
        var location = await LocateAsync(repository);

        var diff = await ReadDiffAsync(location, await TopAsync(location), StashFileOrigin.Untracked);

        Assert.Empty(diff);
    }

    [Fact]
    public async Task GivenBinaryFile_WhenReadingFilesAndDiff_ThenItIsListedAndMarkedBinary()
    {
        using var repository = TestRepositories.Linear(2);
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "image.bin"), [0, 1, 2, 3, 0, 255]);
        repository.Git("add", "image.bin");
        repository.Git("commit", "-q", "-m", "Add binary");
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "image.bin"), [0, 9, 8, 7, 0, 255, 0]);
        repository.Git("stash", "push", "-q");
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var files = await Details.ReadFilesAsync(location, entry, null, Token);
        var diff = await ReadDiffAsync(location, entry, StashFileOrigin.Tracked);

        Assert.NotNull(files);
        Assert.Equal(["M\timage.bin"], Describe(files));
        var binary = Assert.Single(diff);
        Assert.True(binary.IsBinary);
        Assert.Empty(binary.Hunks);
    }

    [Fact]
    public async Task GivenCancelledToken_WhenReadingFiles_ThenItThrowsCancellation()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Details.ReadFilesAsync(location, entry, null, source.Token));
    }

    private static string Rows(bool changed)
    {
        var rows = Enumerable.Range(1, 40)
            .Select(number => changed && number is 3 or 38 ? $"changed {number}" : $"row {number}");

        return $"{string.Join('\n', rows)}\n";
    }

    private static async Task<List<DiffFile>> ReadDiffAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileOrigin origin)
    {
        var files = new List<DiffFile>();
        await foreach (var file in Details.ReadDiffAsync(location, entry, origin, cancellationToken: Token))
        {
            files.Add(file);
        }

        return files;
    }

    private static string[] Describe(StashFiles files) =>
        [.. files.Files.Select(file => file.Change.Kind switch
        {
            FileChangeKind.Renamed => $"R{file.Change.Similarity}\t{file.Change.OldPath}\t{file.Change.Path}",
            FileChangeKind.Deleted => $"D\t{file.Change.Path}",
            FileChangeKind.Added => $"A\t{file.Change.Path}",
            _ => $"M\t{file.Change.Path}",
        })];

    private static string[] NameStatus(TestRepository repository) =>
        repository.Git("stash", "show", "--name-status")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string[] PatchBody(string patch) =>
        [.. patch.Split('\n')
            .Where(line => line.StartsWith("@@", StringComparison.Ordinal)
                || (line.Length > 0 && line[0] is ' ' or '+' or '-'
                    && !line.StartsWith("+++", StringComparison.Ordinal)
                    && !line.StartsWith("---", StringComparison.Ordinal)))];

    private static string[] PatchBody(IEnumerable<DiffFile> files) =>
        [.. files
            .SelectMany(file => file.Hunks)
            .SelectMany(hunk => new[] { hunk.HeaderText }.Concat(hunk.Lines.Select(Prefixed)))];

    private static string Prefixed(DiffLine line) => line.Kind switch
    {
        DiffLineKind.Added => $"+{line.Text}",
        DiffLineKind.Removed => $"-{line.Text}",
        _ => $" {line.Text}",
    };

    private static Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

    private static async Task<StashEntry> TopAsync(RepositoryLocation location) =>
        (await Reader.ReadStashesAsync(location, Token)).Items[0];
}
