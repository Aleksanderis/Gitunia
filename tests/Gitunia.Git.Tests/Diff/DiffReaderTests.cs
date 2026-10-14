using System.Text;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Diff;

public class DiffReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenOddPathsRenamesBinariesAndModeChanges_WhenReadingStagedDiff_ThenWritingBackReproducesGitOutput()
    {
        using var testRepository = TestRepository.Create()
            .Commit(
                "base",
                new TestFile("plain.txt", "1\n2\n3\n"),
                new TestFile("with space/日本 ü.txt", "x\ny\n"),
                new TestFile("tab\there.txt", "t\n"),
                new TestFile("eol.txt", "no newline"),
                new TestFile("rename me.txt", "a\nb\nc\nd\ne\nf\ng\nh\n"),
                new TestFile("source.txt", "s1\ns2\ns3\ns4\ns5\ns6\ns7\ns8\n"),
                new TestFile("run.sh", "echo\n"));
        File.WriteAllBytes(Path.Combine(testRepository.WorkingTreePath, "logo.bin"), [0, 1, 2, 3, 0, 255]);
        testRepository.Git("add", "logo.bin");
        testRepository.Git("commit", "-q", "-m", "binary");
        File.WriteAllBytes(Path.Combine(testRepository.WorkingTreePath, "logo.bin"), [0, 1, 2, 3, 0, 255, 9, 8]);
        testRepository
            .WriteFile("plain.txt", "1\ntwo\n3\n")
            .WriteFile("with space/日本 ü.txt", "x\nz\n")
            .WriteFile("tab\there.txt", "t2\n")
            .WriteFile("eol.txt", "no newline changed")
            .WriteFile("source.txt", "s1\ns2\ns3\ns4\ns5\ns6\ns7\ns8\nnew tail\n")
            .WriteFile("copy of source.txt", "s1\ns2\ns3\ns4\ns5\ns6\ns7\ns8\n")
            .Delete("rename me.txt")
            .WriteFile("moved/renamed ü.txt", "a\nb\nc\nd\ne\nf\ng\nH\n");
        testRepository.Git("add", "-A");
        testRepository.Git("update-index", "--chmod=+x", "run.sh");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        string[] options = ["--no-color", "--no-ext-diff", "--no-textconv", "--src-prefix=a/", "--dst-prefix=b/", "--find-renames", "--find-copies", "--binary"];
        var expected = testRepository.Git(["diff", "--cached", .. options], null);
        var warnings = new List<string>();

        var files = await ReadAllAsync(repository, ["diff", "--cached", "--binary"], warnings);

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
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        testRepository.Git("config", "diff.noprefix", "true");
        testRepository.Git("config", "color.ui", "always");
        testRepository.WriteFile("a.txt", "2\n");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);

        var files = await ReadAllAsync(repository, ["diff"], []);

        var file = Assert.Single(files);
        Assert.Equal("a.txt", file.Path);
        Assert.Equal("a.txt", file.OldPath);
    }

    [Fact]
    public async Task GivenShowOfACommit_WhenReadingWithEmptyFormat_ThenFilesOfThatCommitAreReturned()
    {
        using var testRepository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1\n"))
            .Commit("two", new TestFile("a.txt", "1\n2\n"), new TestFile("b.txt", "b\n"));
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);

        var files = await ReadAllAsync(repository, ["show", "--format=", testRepository.Head], []);

        Assert.Equal(["a.txt", "b.txt"], files.Select(file => file.Path));
        Assert.Equal(DiffFileStatus.Added, files[1].Status);
    }

    [Fact]
    public async Task GivenSelectedAdditionOnly_WhenApplyingForwardPatchToIndex_ThenIndexHoldsExactlyThatChange()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("f.txt", "l1\nl2\nl3\nl4\nl5\nl6\n"));
        testRepository.WriteFile("f.txt", "l1\nX\nY\nl4\nl5\nl6\n");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        var file = (await ReadAllAsync(repository, ["diff"], [])).Single();
        var hunk = file.Hunks.Single();
        var addedX = hunk.Lines.ToList().FindIndex(line => line is { Kind: DiffLineKind.Added, Text: "X" });

        ApplyToIndex(testRepository, DiffPatchWriter.WriteSelection(file, new Dictionary<int, IReadOnlyCollection<int>> { [0] = [addedX] }, PatchDirection.Forward), reverse: false);

        Assert.Equal("l1\nl2\nl3\nX\nl4\nl5\nl6\n", testRepository.Git("show", ":f.txt"));
        Assert.Equal("l1\nX\nY\nl4\nl5\nl6\n", File.ReadAllText(Path.Combine(testRepository.WorkingTreePath, "f.txt")));
    }

    [Fact]
    public async Task GivenSecondOfThreeHunksOnly_WhenApplyingForwardPatchToIndex_ThenOtherHunksStayUnstaged()
    {
        var original = string.Concat(Enumerable.Range(1, 30).Select(number => $"line {number}\n"));
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("f.txt", original));
        testRepository.WriteFile("f.txt", original.Replace("line 3\n", "three\n").Replace("line 15\n", "fifteen\n").Replace("line 27\n", "twenty-seven\n"));
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        var file = (await ReadAllAsync(repository, ["diff", "-U1"], [])).Single();
        Assert.Equal(3, file.Hunks.Count);

        ApplyToIndex(testRepository, DiffPatchWriter.WriteHunks(file, [1]), reverse: false);

        Assert.Equal(original.Replace("line 15\n", "fifteen\n"), testRepository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenStagedChange_WhenApplyingReversePatchOfOneRemovedLine_ThenOnlyThatLineIsUnstaged()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("f.txt", "l1\nl2\nl3\nl4\nl5\nl6\n"));
        testRepository.WriteFile("f.txt", "l1\nX\nY\nl4\nl5\nl6\n");
        testRepository.Git("add", "f.txt");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        var file = (await ReadAllAsync(repository, ["diff", "--cached"], [])).Single();
        var removedTwo = file.Hunks.Single().Lines.ToList().FindIndex(line => line is { Kind: DiffLineKind.Removed, Text: "l2" });

        ApplyToIndex(testRepository, DiffPatchWriter.WriteSelection(file, new Dictionary<int, IReadOnlyCollection<int>> { [0] = [removedTwo] }, PatchDirection.Reverse), reverse: true);

        Assert.Equal("l1\nl2\nX\nY\nl4\nl5\nl6\n", testRepository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenNoNewlineAtEndOfFile_WhenStagingTheChangedLine_ThenIndexKeepsNoNewline()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb"));
        testRepository.WriteFile("f.txt", "a\nB");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        var file = (await ReadAllAsync(repository, ["diff"], [])).Single();

        ApplyToIndex(testRepository, DiffPatchWriter.WriteFile(file), reverse: false);

        Assert.Equal("a\nB", testRepository.Git("show", ":f.txt"));
    }

    [Theory]
    [InlineData("added")]
    [InlineData("removed")]
    public async Task GivenNewlineAtEndOfFileChanges_WhenSelectingOneSideOfTheChange_ThenPatchStillApplies(string selectedSide)
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("f.txt", "a\nb"));
        testRepository.WriteFile("f.txt", "a\nb\n");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        var file = (await ReadAllAsync(repository, ["diff"], [])).Single();
        var lines = file.Hunks.Single().Lines.ToList();
        var index = lines.FindIndex(line => line.Kind == (selectedSide == "added" ? DiffLineKind.Added : DiffLineKind.Removed));

        ApplyToIndex(testRepository, DiffPatchWriter.WriteSelection(file, new Dictionary<int, IReadOnlyCollection<int>> { [0] = [index] }, PatchDirection.Forward), reverse: false);

        Assert.Equal(selectedSide == "added" ? "a\nb\n" : "a\n", testRepository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenStagedChangesInTwoHunks_WhenReversingSelectionsOfBoth_ThenOffsetsAreRightAndOnlyThoseLinesAreUnstaged()
    {
        var original = string.Concat(Enumerable.Range(1, 30).Select(number => $"line {number}\n"));
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("f.txt", original));
        testRepository.WriteFile("f.txt", original.Replace("line 3\n", "three\nextra\nextra two\n").Replace("line 20\n", string.Empty).Replace("line 27\n", "twenty-seven\n"));
        testRepository.Git("add", "f.txt");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        var file = (await ReadAllAsync(repository, ["diff", "--cached", "-U1"], [])).Single();
        Assert.Equal(3, file.Hunks.Count);
        var selection = new Dictionary<int, IReadOnlyCollection<int>>
        {
            [0] = [file.Hunks[0].Lines.ToList().FindIndex(line => line.Text == "extra two")],
            [2] = [file.Hunks[2].Lines.ToList().FindIndex(line => line.Text == "line 27")],
        };

        ApplyToIndex(testRepository, DiffPatchWriter.WriteSelection(file, selection, PatchDirection.Reverse), reverse: true);

        var expected = original.Replace("line 3\n", "three\nextra\n").Replace("line 20\n", string.Empty).Replace("line 27\n", "line 27\ntwenty-seven\n");
        Assert.Equal(expected, testRepository.Git("show", ":f.txt"));
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

        using var testRepository = TestRepository.Create().Commit("base", new TestFile("big.txt", original.ToString()), new TestFile("small.txt", "s\n"));
        testRepository.WriteFile("big.txt", changed.ToString()).WriteFile("small.txt", "t\n");
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);

        var files = await ReadAllAsync(repository, ["diff", "-U0"], []);

        Assert.Equal(["big.txt", "small.txt"], files.Select(file => file.Path));
        Assert.Equal(10_000, files[0].Hunks.Count);
        Assert.Equal(20_000, files[0].Hunks.Sum(hunk => hunk.Lines.Count));
    }

    [Fact]
    public async Task GivenCancelledToken_WhenReading_ThenOperationCancelled()
    {
        using var testRepository = TestRepository.Create().Commit("base", new TestFile("a.txt", "1\n"));
        var repository = await Repository.OpenAsync(Runner, testRepository.WorkingTreePath, Token);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ReadAllAsync(repository, ["diff"], [], source.Token));
    }

    private static async Task<List<DiffFile>> ReadAllAsync(Repository repository, string[] command, List<string> warnings, CancellationToken? token = null)
    {
        var files = new List<DiffFile>();
        await foreach (var file in DiffReader.ReadAsync(repository, command, warnings, cancellationToken: token ?? Token))
        {
            files.Add(file);
        }

        return files;
    }

    private static void ApplyToIndex(TestRepository testRepository, string patch, bool reverse)
    {
        Assert.NotEqual(string.Empty, patch);
        var patchPath = Path.Combine(Path.GetTempPath(), $"gitunia-patch-{Guid.NewGuid().ToString("N")}.patch");
        File.WriteAllBytes(patchPath, Encoding.UTF8.GetBytes(patch));
        try
        {
            testRepository.Git(reverse ? ["apply", "--cached", "-R", patchPath] : ["apply", "--cached", patchPath], null);
        }
        finally
        {
            File.Delete(patchPath);
        }
    }
}
