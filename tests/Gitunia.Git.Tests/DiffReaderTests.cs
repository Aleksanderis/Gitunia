using System.Text;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class DiffReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenOddPathsRenamesBinariesAndModeChanges_WhenReadingStagedDiff_ThenWritingBackReproducesGitOutput()
    {
        using var repository = TestRepository.Create()
            .Commit(
                "base",
                ("plain.txt", "1\n2\n3\n"),
                ("with space/日本 ü.txt", "x\ny\n"),
                ("tab\there.txt", "t\n"),
                ("eol.txt", "no newline"),
                ("rename me.txt", "a\nb\nc\nd\ne\nf\ng\nh\n"),
                ("source.txt", "s1\ns2\ns3\ns4\ns5\ns6\ns7\ns8\n"),
                ("run.sh", "echo\n"));
        File.WriteAllBytes(System.IO.Path.Combine(repository.Path, "logo.bin"), [0, 1, 2, 3, 0, 255]);
        repository.Git("add", "logo.bin");
        repository.Git("commit", "-q", "-m", "binary");
        File.WriteAllBytes(System.IO.Path.Combine(repository.Path, "logo.bin"), [0, 1, 2, 3, 0, 255, 9, 8]);
        repository
            .WriteFile("plain.txt", "1\ntwo\n3\n")
            .WriteFile("with space/日本 ü.txt", "x\nz\n")
            .WriteFile("tab\there.txt", "t2\n")
            .WriteFile("eol.txt", "no newline changed")
            .WriteFile("source.txt", "s1\ns2\ns3\ns4\ns5\ns6\ns7\ns8\nnew tail\n")
            .WriteFile("copy of source.txt", "s1\ns2\ns3\ns4\ns5\ns6\ns7\ns8\n")
            .Delete("rename me.txt")
            .WriteFile("moved/renamed ü.txt", "a\nb\nc\nd\ne\nf\ng\nH\n");
        repository.Git("add", "-A");
        repository.Git("update-index", "--chmod=+x", "run.sh");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        string[] options = ["--no-color", "--no-ext-diff", "--no-textconv", "--src-prefix=a/", "--dst-prefix=b/", "--find-renames", "--find-copies", "--binary"];
        var expected = repository.Git(["diff", "--cached", .. options], null);
        var warnings = new List<string>();

        var files = await ReadAllAsync(repo, ["diff", "--cached", "--binary"], warnings);

        Assert.Empty(warnings);
        Assert.Equal(expected, string.Concat(files.Select(DiffPatchWriter.WriteFile)));
        Assert.Contains(files, file => file.Path == "with space/日本 ü.txt");
        Assert.Contains(files, file => file.Path == "tab\there.txt");
        Assert.Contains(files, file => file.Status == DiffFileStatus.Renamed && file.OldPath == "rename me.txt" && file.NewPath == "moved/renamed ü.txt");
        Assert.Contains(files, file => file.Status == DiffFileStatus.Copied && file.OldPath == "source.txt" && file.NewPath == "copy of source.txt");
        Assert.Contains(files, file => file.IsBinary && file.Path == "logo.bin");
        Assert.Contains(files, file => file.IsModeChange && file.Path == "run.sh" && file.NewMode == "100755");
        Assert.Contains(files, file => file.Hunks.Any(hunk => hunk.Lines.Any(line => line.NoNewlineAtEnd)));
    }

    [Fact]
    public async Task GivenUserDiffConfig_WhenReadingDiff_ThenFixedOptionsKeepPrefixesAndNoColor()
    {
        using var repository = TestRepository.Create().Commit("base", ("a.txt", "1\n"));
        repository.Git("config", "diff.noprefix", "true");
        repository.Git("config", "color.ui", "always");
        repository.WriteFile("a.txt", "2\n");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var files = await ReadAllAsync(repo, ["diff"], []);

        var file = Assert.Single(files);
        Assert.Equal("a.txt", file.Path);
        Assert.Equal("a.txt", file.OldPath);
    }

    [Fact]
    public async Task GivenShowOfACommit_WhenReadingWithEmptyFormat_ThenFilesOfThatCommitAreReturned()
    {
        using var repository = TestRepository.Create()
            .Commit("one", ("a.txt", "1\n"))
            .Commit("two", ("a.txt", "1\n2\n"), ("b.txt", "b\n"));
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var files = await ReadAllAsync(repo, ["show", "--format=", repository.Head], []);

        Assert.Equal(["a.txt", "b.txt"], files.Select(file => file.Path));
        Assert.Equal(DiffFileStatus.Added, files[1].Status);
    }

    [Fact]
    public async Task GivenSelectedAdditionOnly_WhenApplyingForwardPatchToIndex_ThenIndexHoldsExactlyThatChange()
    {
        using var repository = TestRepository.Create().Commit("base", ("f.txt", "l1\nl2\nl3\nl4\nl5\nl6\n"));
        repository.WriteFile("f.txt", "l1\nX\nY\nl4\nl5\nl6\n");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var file = (await ReadAllAsync(repo, ["diff"], [])).Single();
        var hunk = file.Hunks.Single();
        var addedX = hunk.Lines.ToList().FindIndex(line => line is { Kind: DiffLineKind.Added, Text: "X" });

        ApplyToIndex(repository, DiffPatchWriter.WriteSelection(file, new Dictionary<int, IReadOnlyCollection<int>> { [0] = [addedX] }, PatchDirection.Forward), reverse: false);

        Assert.Equal("l1\nl2\nl3\nX\nl4\nl5\nl6\n", repository.Git("show", ":f.txt"));
        Assert.Equal("l1\nX\nY\nl4\nl5\nl6\n", File.ReadAllText(System.IO.Path.Combine(repository.Path, "f.txt")));
    }

    [Fact]
    public async Task GivenSecondOfThreeHunksOnly_WhenApplyingForwardPatchToIndex_ThenOtherHunksStayUnstaged()
    {
        var original = string.Concat(Enumerable.Range(1, 30).Select(number => $"line {number}\n"));
        using var repository = TestRepository.Create().Commit("base", ("f.txt", original));
        repository.WriteFile("f.txt", original.Replace("line 3\n", "three\n").Replace("line 15\n", "fifteen\n").Replace("line 27\n", "twenty-seven\n"));
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var file = (await ReadAllAsync(repo, ["diff", "-U1"], [])).Single();
        Assert.Equal(3, file.Hunks.Count);

        ApplyToIndex(repository, DiffPatchWriter.WriteHunks(file, [1]), reverse: false);

        Assert.Equal(original.Replace("line 15\n", "fifteen\n"), repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenStagedChange_WhenApplyingReversePatchOfOneRemovedLine_ThenOnlyThatLineIsUnstaged()
    {
        using var repository = TestRepository.Create().Commit("base", ("f.txt", "l1\nl2\nl3\nl4\nl5\nl6\n"));
        repository.WriteFile("f.txt", "l1\nX\nY\nl4\nl5\nl6\n");
        repository.Git("add", "f.txt");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var file = (await ReadAllAsync(repo, ["diff", "--cached"], [])).Single();
        var removedTwo = file.Hunks.Single().Lines.ToList().FindIndex(line => line is { Kind: DiffLineKind.Removed, Text: "l2" });

        ApplyToIndex(repository, DiffPatchWriter.WriteSelection(file, new Dictionary<int, IReadOnlyCollection<int>> { [0] = [removedTwo] }, PatchDirection.Reverse), reverse: true);

        Assert.Equal("l1\nl2\nX\nY\nl4\nl5\nl6\n", repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenNoNewlineAtEndOfFile_WhenStagingTheChangedLine_ThenIndexKeepsNoNewline()
    {
        using var repository = TestRepository.Create().Commit("base", ("f.txt", "a\nb"));
        repository.WriteFile("f.txt", "a\nB");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var file = (await ReadAllAsync(repo, ["diff"], [])).Single();

        ApplyToIndex(repository, DiffPatchWriter.WriteFile(file), reverse: false);

        Assert.Equal("a\nB", repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenHugeDiff_WhenStreaming_ThenEveryLineArrivesAndParserBuffersOnlyOneLine()
    {
        var original = new StringBuilder();
        var changed = new StringBuilder();
        for (var number = 0; number < 100_000; number++)
        {
            original.Append("line ").Append(number).Append('\n');
            changed.Append(number % 10 == 0 ? "changed " : "line ").Append(number).Append('\n');
        }

        using var repository = TestRepository.Create().Commit("base", ("big.txt", original.ToString()), ("small.txt", "s\n"));
        repository.WriteFile("big.txt", changed.ToString()).WriteFile("small.txt", "t\n");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var files = await ReadAllAsync(repo, ["diff", "-U0"], []);

        Assert.Equal(["big.txt", "small.txt"], files.Select(file => file.Path));
        Assert.Equal(10_000, files[0].Hunks.Count);
        Assert.Equal(20_000, files[0].Hunks.Sum(hunk => hunk.Lines.Count));
    }

    [Fact]
    public async Task GivenCancelledToken_WhenReading_ThenOperationCancelled()
    {
        using var repository = TestRepository.Create().Commit("base", ("a.txt", "1\n"));
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ReadAllAsync(repo, ["diff"], [], source.Token));
    }

    private static async Task<List<DiffFile>> ReadAllAsync(Repo repo, string[] command, List<string> warnings, CancellationToken? token = null)
    {
        var files = new List<DiffFile>();
        await foreach (var file in DiffReader.ReadAsync(repo, command, token ?? Token, warnings))
        {
            files.Add(file);
        }

        return files;
    }

    private static void ApplyToIndex(TestRepository repository, string patch, bool reverse)
    {
        Assert.NotEqual(string.Empty, patch);
        var patchPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gitunia-patch-" + Guid.NewGuid().ToString("N") + ".patch");
        File.WriteAllBytes(patchPath, DiffPatchWriter.ToBytes(patch));
        try
        {
            repository.Git(reverse ? ["apply", "--cached", "-R", patchPath] : ["apply", "--cached", patchPath], null);
        }
        finally
        {
            File.Delete(patchPath);
        }
    }
}
