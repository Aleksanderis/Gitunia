using System.Text;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Staging;

public class PatchStagerTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly StatusReader Status = new(Runner);
    private static readonly DiffReader Diff = new(Runner);
    private static readonly IndexWriter Writer = new(Runner);
    private static readonly PatchStager Stager = new(Diff, Writer);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Lines(int count, params (int Line, string Text)[] replaced)
    {
        var builder = new StringBuilder();
        for (var line = 1; line <= count; line++)
        {
            var text = replaced.Any(change => change.Line == line) ? replaced.First(change => change.Line == line).Text : $"l{line}";
            builder.Append(text).Append('\n');
        }

        return builder.ToString();
    }

    [Fact]
    public async Task GivenTwoHunks_WhenStagingTheFirst_ThenOnlyItIsInTheIndex()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", Lines(30)));
        repository.WriteFile("f.txt", Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")));
        var (location, entry) = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, hunk: 0);
        var result = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal(Lines(30, (2, "TWO")), repository.Git("show", ":f.txt"));
        Assert.Equal(Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")), File.ReadAllText(Path.Combine(repository.WorkingTreePath, "f.txt")));
        Assert.Contains("TWENTY-EIGHT", repository.Git("diff"), StringComparison.Ordinal);
        Assert.DoesNotContain("TWO", repository.Git("diff"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenOneHunkOfSeveralChanges_WhenStagingOneAddedLine_ThenOnlyThatLineChanges()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB1\nB2\nc\n");
        var (location, entry) = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, texts: ["B1"]);
        var result = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal("a\nb\nB1\nc\n", repository.Git("show", ":f.txt"));
        Assert.Equal("a\nB1\nB2\nc\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "f.txt")));
    }

    [Fact]
    public async Task GivenARemovedLineChosen_WhenStaging_ThenOnlyItsRemovalIsStaged()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\nd\n"));
        repository.WriteFile("f.txt", "a\nc\nD\n");
        var (location, entry) = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, texts: ["b"]);
        await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, chosen, false, Token);

        Assert.Equal("a\nc\nd\n", repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenLinesChosenInADiffWithOtherContext_WhenStaging_ThenTheyStillMatch()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", Lines(30)));
        repository.WriteFile("f.txt", Lines(30, (2, "TWO"), (4, "FOUR"), (28, "TWENTY-EIGHT")));
        var (location, entry) = await LocateAsync(repository, "f.txt");
        var wholeFile = DiffOptions.Default with { WholeFile = true };
        var none = DiffOptions.Default with { ContextLines = 0 };

        var fromWholeFile = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, wholeFile, texts: ["TWO", "l2"]);
        var result = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, fromWholeFile, false, Token);
        var fromNoContext = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, none, texts: ["l28", "TWENTY-EIGHT"]);
        var second = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, fromNoContext, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal(PatchStagingOutcome.Applied, second.Outcome);
        Assert.Equal(Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")), repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenStagedChanges_WhenUnstagingOneHunk_ThenTheOtherStaysStaged()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", Lines(30)));
        repository.WriteFile("f.txt", Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")));
        repository.Git("add", "f.txt");
        var (location, entry) = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(location, WorkingDiffSide.Staged, entry, DiffOptions.Default, hunk: 1);
        var result = await Stager.StageAsync(location, WorkingDiffSide.Staged, entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal(Lines(30, (2, "TWO")), repository.Git("show", ":f.txt"));
        Assert.Equal(Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")), File.ReadAllText(Path.Combine(repository.WorkingTreePath, "f.txt")));
    }

    [Fact]
    public async Task GivenStagedLines_WhenUnstagingOneOfThem_ThenTheRestStaysStaged()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB1\nB2\nc\n");
        repository.Git("add", "f.txt");
        var (location, entry) = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(location, WorkingDiffSide.Staged, entry, DiffOptions.Default, texts: ["B2"]);
        await Stager.StageAsync(location, WorkingDiffSide.Staged, entry, chosen, false, Token);

        Assert.Equal("a\nB1\nc\n", repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenFilesWithoutNewlineAtTheEnd_WhenStagingLinesAndHunks_ThenTheIndexMatchesWhatWasChosen()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("f.txt", $"{Lines(30)}end"), new TestFile("g.txt", "x\nend"));
        repository.WriteFile("f.txt", $"{Lines(30, (2, "TWO"))}end changed");
        repository.WriteFile("g.txt", "x\nend\nmore");
        var (location, entry) = await LocateAsync(repository, "f.txt");
        var (_, other) = await LocateAsync(repository, "g.txt");

        var first = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, hunk: 0);
        await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, first, false, Token);
        var second = await ChangesAsync(location, WorkingDiffSide.Unstaged, other, DiffOptions.Default, hunk: 0);
        await Stager.StageAsync(location, WorkingDiffSide.Unstaged, other, second, false, Token);

        Assert.Equal($"{Lines(30, (2, "TWO"))}end", repository.Git("show", ":f.txt"));
        Assert.Equal("x\nend\nmore", repository.Git("show", ":g.txt"));
        Assert.Equal(string.Empty, repository.Git("diff", "--", "g.txt"));
    }

    [Fact]
    public async Task GivenAStagedRenameWithTwoEdits_WhenUnstagingOneHunk_ThenTheRenameAndTheOtherEditStay()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("old.txt", Lines(30)));
        repository.Git("mv", "old.txt", "new.txt");
        repository.WriteFile("new.txt", Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")));
        repository.Git("add", "new.txt");
        var (location, entry) = await LocateAsync(repository, "new.txt");
        Assert.Equal(StatusEntryKind.Renamed, entry.Kind);

        var chosen = await ChangesAsync(location, WorkingDiffSide.Staged, entry, DiffOptions.Default, hunk: 1);
        var result = await Stager.StageAsync(location, WorkingDiffSide.Staged, entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal(Lines(30, (2, "TWO")), repository.Git("show", ":new.txt"));
        Assert.Contains("rename from old.txt", repository.Git("diff", "--cached", "-M"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("ls-files", "old.txt"));
    }

    [Fact]
    public async Task GivenARenameToAPathWithASpaceAndAnEditInTheWorkingTree_WhenStagingAndUnstagingHunks_ThenTheIndexFollows()
    {
        const string renamed = "new name/ü file.txt";
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("old.txt", Lines(30)));
        repository.WriteFile(renamed, Lines(30, (2, "TWO")));
        repository.Delete("old.txt");
        repository.Git("add", "-A");
        repository.WriteFile(renamed, Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")));
        var (location, entry) = await LocateAsync(repository, renamed);
        Assert.Equal(StatusEntryKind.Renamed, entry.Kind);

        var edit = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, hunk: 0);
        var staged = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, edit, false, Token);
        var (_, afterStaging) = await LocateAsync(repository, renamed);
        var chosen = await ChangesAsync(location, WorkingDiffSide.Staged, afterStaging, DiffOptions.Default, hunk: 0);
        var unstaged = await Stager.StageAsync(location, WorkingDiffSide.Staged, afterStaging, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, staged.Outcome);
        Assert.Equal(PatchStagingOutcome.Applied, unstaged.Outcome);
        Assert.Equal(Lines(30, (28, "TWENTY-EIGHT")), repository.Git("show", $":{renamed}"));
        Assert.Equal(string.Empty, repository.Git("ls-files", "old.txt"));
    }

    [Fact]
    public async Task GivenARenameWithoutEdits_WhenUnstaging_ThenBothPathsGoWhole()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("old.txt", "some content\nmore\n"));
        repository.Git("mv", "old.txt", "new.txt");
        var (location, entry) = await LocateAsync(repository, "new.txt");

        var result = await Stager.StageAsync(location, WorkingDiffSide.Staged, entry, [new ChangedLine(DiffLineKind.Added, 1, "x")], false, Token);

        Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        Assert.Equal("old.txt\n", repository.Git("ls-files"));
    }

    [Fact]
    public async Task GivenUntrackedDeletedAndBinaryFiles_WhenStagingLines_ThenEachGoesWhole()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("gone.txt", "1\n2\n"), new TestFile("pic.bin", "ab\n"));
        repository.WriteFile("fresh.txt", "1\n2\n3\n").Delete("gone.txt");
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "pic.bin"), [0, 1, 2, 0, 255]);
        var chosen = new[] { new ChangedLine(DiffLineKind.Added, 1, "1") };

        foreach (var path in new[] { "fresh.txt", "gone.txt", "pic.bin" })
        {
            var (location, entry) = await LocateAsync(repository, path);
            var result = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, chosen, false, Token);
            Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        }

        Assert.Equal(string.Empty, repository.Git("diff"));
        Assert.Equal("fresh.txt\ngone.txt\npic.bin\n", repository.Git("diff", "--cached", "--no-renames", "--name-only"));
    }

    [Fact]
    public async Task GivenAStagedNewFile_WhenUnstagingLines_ThenTheFileIsUnstagedWhole()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("fresh.txt", "1\n2\n3\n");
        repository.Git("add", "fresh.txt");
        var (location, entry) = await LocateAsync(repository, "fresh.txt");

        var result = await Stager.StageAsync(location, WorkingDiffSide.Staged, entry, [new ChangedLine(DiffLineKind.Added, 2, "2")], false, Token);

        Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        Assert.Equal(string.Empty, repository.Git("diff", "--cached", "--no-renames", "--name-only"));
    }

    [Fact]
    public async Task GivenALineThatChangedSinceTheDiffWasRead_WhenStaging_ThenNothingIsChanged()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB\nc\n");
        var (location, entry) = await LocateAsync(repository, "f.txt");
        var chosen = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, hunk: 0);
        repository.WriteFile("f.txt", "a\nsomething else\nc\n");

        var result = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.FileChanged, result.Outcome);
        Assert.Equal("a\nb\nc\n", repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenNoLinesChosen_WhenStaging_ThenTheWholeFileIsStaged()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB\nc\n");
        var (location, entry) = await LocateAsync(repository, "f.txt");

        var result = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, [], false, Token);

        Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        Assert.Equal("a\nB\nc\n", repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenLinesWithCarriageReturnsAndUnicode_WhenStagingAHunk_ThenTheBytesSurvive()
    {
        var original = "ä1\r\nb\r\nc\r\n" + string.Concat(Enumerable.Range(4, 30).Select(line => $"l{line}\r\n"));
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", original));
        var changed = original.Replace("b\r\n", "日本\r\n", StringComparison.Ordinal).Replace("l30\r\n", "L30\r\n", StringComparison.Ordinal);
        repository.WriteFile("f.txt", changed);
        var (location, entry) = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, hunk: 0);
        var result = await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal(original.Replace("b\r\n", "日本\r\n", StringComparison.Ordinal), repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenAFileWithOddCharactersInItsName_WhenStagingAHunk_ThenItIsStaged()
    {
        const string path = "with space/日本 ü.txt";
        using var repository = TestRepository.Create().Commit("Initial", new TestFile(path, Lines(30)));
        repository.WriteFile(path, Lines(30, (2, "TWO"), (28, "TWENTY-EIGHT")));
        var (location, entry) = await LocateAsync(repository, path);

        var chosen = await ChangesAsync(location, WorkingDiffSide.Unstaged, entry, DiffOptions.Default, hunk: 1);
        await Stager.StageAsync(location, WorkingDiffSide.Unstaged, entry, chosen, false, Token);

        Assert.Equal(Lines(30, (28, "TWENTY-EIGHT")), repository.Git("show", $":{path}"));
    }

    private static async Task<(RepositoryLocation Location, StatusEntry Entry)> LocateAsync(TestRepository repository, string path)
    {
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var snapshot = await Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);
        return (location, snapshot.Entries.Single(entry => entry.Path == path));
    }

    // The changed lines of a hunk (or those with these texts) as read from a diff with the given options, as a user would pick them.
    private static async Task<IReadOnlyList<ChangedLine>> ChangesAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        DiffOptions options,
        int? hunk = null,
        string[]? texts = null)
    {
        DiffFile? file = null;
        await foreach (var candidate in Diff.ReadWorkingFileAsync(location, side, entry, options: options, cancellationToken: Token))
        {
            file = candidate;
        }

        Assert.NotNull(file);
        var changes = new List<ChangedLine>();
        for (var hunkIndex = 0; hunkIndex < file.Hunks.Count; hunkIndex++)
        {
            foreach (var line in file.Hunks[hunkIndex].Lines.Where(line => line.Kind != DiffLineKind.Context))
            {
                var isPicked = hunk is { } wanted ? wanted == hunkIndex : texts!.Contains(line.Text);
                if (isPicked)
                {
                    changes.Add(new ChangedLine(line.Kind, (line.Kind == DiffLineKind.Removed ? line.OldLineNumber : line.NewLineNumber)!.Value, line.Text));
                }
            }
        }

        return changes;
    }
}
