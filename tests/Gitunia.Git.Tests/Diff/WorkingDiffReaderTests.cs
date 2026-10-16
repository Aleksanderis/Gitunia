using System.Diagnostics;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Diff;

public class WorkingDiffReaderTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly DiffReader Diff = new(Runner);
    private static readonly StatusReader Status = new(Runner);

    private static readonly string[] FixedOptions =
    [
        "--no-color",
        "--no-ext-diff",
        "--no-textconv",
        "--src-prefix=a/",
        "--dst-prefix=b/",
        "--find-renames",
        "--find-copies",
    ];

    private const int DifferencesExitCode = 1;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenModifiedAndDeletedFiles_WhenReadingUnstaged_ThenTheDiffMatchesGitDiff()
    {
        using var testRepository = TestRepository.Create()
            .Commit("base", new TestFile("keep.txt", "1\n2\n3\n"), new TestFile("gone.txt", "x\ny\n"), new TestFile("other.txt", "o\n"));
        testRepository.WriteFile("keep.txt", "1\ntwo\n3\n").Delete("gone.txt").WriteFile("other.txt", "changed\n");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var modified = await ReadAsync(location, WorkingDiffSide.Unstaged, "keep.txt");
        var deleted = await ReadAsync(location, WorkingDiffSide.Unstaged, "gone.txt");

        Assert.Equal(testRepository.Git(["diff", .. FixedOptions, "--", "keep.txt"], null), Written(modified));
        Assert.Equal(testRepository.Git(["diff", .. FixedOptions, "--", "gone.txt"], null), Written(deleted));
        Assert.Equal(DiffFileStatus.Deleted, Assert.Single(deleted).Status);
        Assert.Equal("keep.txt", Assert.Single(modified).Path);
    }

    [Fact]
    public async Task GivenStagedModifiedAddedAndRenamedFiles_WhenReadingStaged_ThenTheDiffMatchesGitDiffCached()
    {
        using var testRepository = TestRepository.Create()
            .Commit("base", new TestFile("keep.txt", "1\n2\n3\n"), new TestFile("old name.txt", "a\nb\nc\nd\ne\nf\ng\nh\n"));
        testRepository.WriteFile("keep.txt", "1\ntwo\n3\n").WriteFile("fresh.txt", "new\n");
        testRepository.Git("mv", "old name.txt", "new name.txt");
        testRepository.Git("add", "-A");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var modified = await ReadAsync(location, WorkingDiffSide.Staged, "keep.txt");
        var added = await ReadAsync(location, WorkingDiffSide.Staged, "fresh.txt");
        var renamed = await ReadAsync(location, WorkingDiffSide.Staged, "new name.txt");

        Assert.Equal(testRepository.Git(["diff", "--cached", .. FixedOptions, "--", "keep.txt"], null), Written(modified));
        Assert.Equal(testRepository.Git(["diff", "--cached", .. FixedOptions, "--", "fresh.txt"], null), Written(added));
        var rename = Assert.Single(renamed);
        Assert.Equal(DiffFileStatus.Renamed, rename.Status);
        Assert.Equal("old name.txt", rename.OldPath);
        Assert.Equal("new name.txt", rename.NewPath);
    }

    [Fact]
    public async Task GivenUntrackedTextFile_WhenReadingUnstaged_ThenItIsShownAsAdditionsLikeGitDiffNoIndex()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        testRepository.WriteFile("dir/new file.txt", "one\ntwo\n");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var files = await ReadAsync(location, WorkingDiffSide.Unstaged, "dir/new file.txt");

        var expected = testRepository.Git(["diff", "--no-index", .. FixedOptions, "--", "/dev/null", "dir/new file.txt"], [DifferencesExitCode]);
        Assert.Equal(expected, Written(files));
        var file = Assert.Single(files);
        Assert.Equal(DiffFileStatus.Added, file.Status);
        Assert.Equal(["one", "two"], file.Hunks.Single().Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task GivenUntrackedBinaryFile_WhenReadingUnstaged_ThenItIsABinaryFileWithoutHunks()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        File.WriteAllBytes(Path.Combine(testRepository.WorkingTreePath, "logo.bin"), [0, 1, 2, 3, 0, 255]);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var files = await ReadAsync(location, WorkingDiffSide.Unstaged, "logo.bin");

        var file = Assert.Single(files);
        Assert.True(file.IsBinary);
        Assert.Empty(file.Hunks);
    }

    [Fact]
    public async Task GivenTrackedBinaryFileChanged_WhenReadingUnstaged_ThenItIsABinaryFileWithoutHunks()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        var path = Path.Combine(testRepository.WorkingTreePath, "logo.bin");
        File.WriteAllBytes(path, [0, 1, 2, 3, 0, 255]);
        testRepository.Git("add", "logo.bin");
        testRepository.Git("commit", "-q", "-m", "binary");
        File.WriteAllBytes(path, [0, 1, 2, 3, 0, 255, 9]);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var files = await ReadAsync(location, WorkingDiffSide.Unstaged, "logo.bin");

        Assert.True(Assert.Single(files).IsBinary);
    }

    [Fact]
    public async Task GivenConflictedFile_WhenReadingUnstaged_ThenTheCombinedDiffIsShownAsOneFile()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb\nc\n"));
        testRepository.CheckoutNewBranch("topic").Commit("topic", new TestFile("f.txt", "a\ntheirs\nc\n"));
        testRepository.Checkout("main").Commit("main", new TestFile("f.txt", "a\nours\nc\n"));
        testRepository.MergeExpectingConflict("topic");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var warnings = new List<string>();

        var files = await ReadAsync(location, WorkingDiffSide.Unstaged, "f.txt", warnings);

        Assert.Empty(warnings);
        var file = Assert.Single(files);
        Assert.Equal("f.txt", file.Path);
        var hunk = Assert.Single(file.Hunks);
        Assert.StartsWith("@@@", hunk.HeaderText, StringComparison.Ordinal);
        var texts = hunk.Lines.Select(line => (line.Kind, line.Text)).ToList();
        Assert.Contains((DiffLineKind.Added, "<<<<<<< HEAD"), texts);
        Assert.Contains((DiffLineKind.Added, "ours"), texts);
        Assert.Contains((DiffLineKind.Added, "theirs"), texts);
        Assert.Contains((DiffLineKind.Context, "a"), texts);
        Assert.Equal(1, hunk.Lines[0].NewLineNumber);
    }

    [Fact]
    public async Task GivenPathWithGlobCharacters_WhenReadingUnstaged_ThenOnlyThatFileIsRead()
    {
        using var testRepository = TestRepository.Create()
            .Commit("base", new TestFile("a*.txt", "1\n"), new TestFile("ab.txt", "1\n"));
        testRepository.WriteFile("a*.txt", "star\n").WriteFile("ab.txt", "b\n");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var files = await ReadAsync(location, WorkingDiffSide.Unstaged, "a*.txt");

        Assert.Equal("a*.txt", Assert.Single(files).Path);
    }

    [Fact]
    public async Task GivenModifiedFileWithNonUtf8Name_WhenReadingUnstaged_ThenItsChangesAreShown()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Only Linux allows file names that are not UTF-8.");
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("other.txt", "o\n"));
        CreateOddFile(testRepository.WorkingTreePath, "one\ntwo\n");
        testRepository.Git("add", "-A");
        testRepository.Git("commit", "-m", "odd");
        CreateOddFile(testRepository.WorkingTreePath, "one\n2\n");
        testRepository.WriteFile("other.txt", "changed\n");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var files = await ReadOddAsync(location, WorkingDiffSide.Unstaged);

        var file = Assert.Single(files);
        Assert.True(file.HasInvalidText);
        var lines = file.Hunks.Single().Lines;
        Assert.Contains(lines, line => line.Kind == DiffLineKind.Removed && line.Text == "two");
        Assert.Contains(lines, line => line.Kind == DiffLineKind.Added && line.Text == "2");
    }

    [Fact]
    public async Task GivenStagedFileWithNonUtf8Name_WhenReadingStaged_ThenItsChangesAreShown()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Only Linux allows file names that are not UTF-8.");
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("other.txt", "o\n"));
        CreateOddFile(testRepository.WorkingTreePath, "one\n");
        testRepository.Git("add", "-A");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var files = await ReadOddAsync(location, WorkingDiffSide.Staged);

        var file = Assert.Single(files);
        Assert.Equal(DiffFileStatus.Added, file.Status);
        Assert.Equal(["one"], file.Hunks.Single().Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task GivenUntrackedFileWithNonUtf8Name_WhenReadingUnstaged_ThenItIsShownAsAdditions()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Only Linux allows file names that are not UTF-8.");
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("other.txt", "o\n"));
        CreateOddFile(testRepository.WorkingTreePath, "one\ntwo");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var files = await ReadOddAsync(location, WorkingDiffSide.Unstaged);

        var file = Assert.Single(files);
        Assert.Equal(DiffFileStatus.Added, file.Status);
        Assert.Equal(["one", "two"], file.Hunks.Single().Lines.Select(line => line.Text));
    }

    private static async Task<IReadOnlyList<DiffFile>> ReadOddAsync(RepositoryLocation location, WorkingDiffSide side)
    {
        var snapshot = await Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);
        var entry = snapshot.Entries.Single(candidate => GitPathText.HasEscapedBytes(candidate.Path));
        var files = new List<DiffFile>();
        await foreach (var file in Diff.ReadWorkingFileAsync(location, side, entry, cancellationToken: Token))
        {
            files.Add(file);
        }

        return files;
    }

    private static void CreateOddFile(string directory, string content)
    {
        // The runtime cannot spell a non-UTF-8 name, so the shell creates the file from an octal escape.
        var startInfo = new ProcessStartInfo("sh") { WorkingDirectory = directory };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add($"printf '{content.Replace("\n", "\\n")}' > \"$(printf 'odd\\377 name.txt')\"");
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static async Task<IReadOnlyList<DiffFile>> ReadAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        string path,
        ICollection<string>? warnings = null)
    {
        var snapshot = await Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);
        var entry = snapshot.Entries.Single(candidate => candidate.Path == path);
        var files = new List<DiffFile>();
        await foreach (var file in Diff.ReadWorkingFileAsync(location, side, entry, warnings, cancellationToken: Token))
        {
            files.Add(file);
        }

        return files;
    }

    private static string Written(IEnumerable<DiffFile> files) => string.Concat(files.Select(DiffPatchWriter.WriteFile));
}
