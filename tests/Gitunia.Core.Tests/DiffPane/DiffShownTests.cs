using Gitunia.Core.Diff;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Keymap;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Xunit;

namespace Gitunia.Core.Tests.DiffPane;

[Trait("Os", "Independent")]
public class DiffShownTests
{
    [Fact]
    public void GivenAShownFile_WhenCutting_ThenThePaneHoldsOnlyThatFile()
    {
        var full = Document("a.txt", "b.txt");
        var state = new DiffPaneState(ShownPath: "b.txt");

        var cut = DiffShown.Cut(state, full, true);

        Assert.Equal(1, cut.File);
        Assert.Null(cut.BuiltFolder);
        Assert.Equal([["b.txt"]], DiffShown.DocumentOf(full, cut).FilePaths);
    }

    [Fact]
    public void GivenAShownFolder_WhenCutting_ThenThePaneHoldsTheFilesBelowIt()
    {
        var full = Document("top.txt", "src/a.txt", "src/sub/b.txt");
        var state = new DiffPaneState(ShownFolder: "src", ShownPath: "src/a.txt");

        var cut = DiffShown.Cut(state, full, true);

        Assert.Equal("src", cut.BuiltFolder);
        Assert.Equal([["src/a.txt"], ["src/sub/b.txt"]], DiffShown.DocumentOf(full, cut).FilePaths);
    }

    [Fact]
    public void GivenAFolderWhileTheDiffLoads_WhenCutting_ThenTheWholeDocumentStaysAndNoFolderIsBuilt()
    {
        var state = new DiffPaneState(ShownFolder: "src");

        var cut = DiffShown.Cut(state, DiffDocument.Empty, false);

        Assert.Equal(-1, cut.File);
        Assert.Null(cut.BuiltFolder);
        Assert.Same(DiffDocument.Empty, DiffShown.DocumentOf(DiffDocument.Empty, cut));
    }

    [Fact]
    public void GivenNoWantedFile_WhenCutting_ThenTheFirstFileShows()
    {
        var cut = DiffShown.Cut(DiffPaneState.Empty, Document("a.txt", "b.txt"), true);

        Assert.Equal(0, cut.File);
    }

    [Fact]
    public void GivenAWantedFileTheCommitDoesNotHave_WhenCutting_ThenTheFirstFileShows()
    {
        var cut = DiffShown.Cut(new DiffPaneState(ShownPath: "gone.txt"), Document("a.txt", "b.txt"), true);

        Assert.Equal(0, cut.File);
    }

    [Theory]
    [InlineData(1, 3, true, 1, 2)]
    [InlineData(0, 3, true, 5, 2)]
    [InlineData(2, 3, false, 5, 0)]
    [InlineData(1, 3, false, 1, 0)]
    public void GivenAFileAndACount_WhenStepping_ThenTheTargetIsClampedToTheFiles(
        int shown,
        int fileCount,
        bool forward,
        int steps,
        int expected)
    {
        Assert.Equal(expected, DiffFileStep.Target(shown, fileCount, forward, steps));
    }

    [Theory]
    [InlineData(2, 3, true)]
    [InlineData(0, 3, false)]
    [InlineData(-1, 3, true)]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, true)]
    public void GivenTheLastOrFirstFileOrNone_WhenStepping_ThenNothingMoves(int shown, int fileCount, bool forward)
    {
        Assert.Equal(-1, DiffFileStep.Target(shown, fileCount, forward, 1));
    }

    [Fact]
    public void GivenTheOptionKeys_WhenResolving_ThenEachChangesItsOptionAndOtherKeysAreRefused()
    {
        var options = DiffOptions.Default;

        Assert.True(DiffOptionKeys.TryResolve(options, ActionIds.DiffMoreContext, out var more));
        Assert.True(more.ContextLines > options.ContextLines);
        Assert.True(DiffOptionKeys.TryResolve(options, ActionIds.DiffToggleWholeFile, out var whole));
        Assert.True(whole.WholeFile);
        Assert.True(DiffOptionKeys.TryResolve(options, ActionIds.DiffToggleIgnoreWhitespace, out var ignore));
        Assert.True(ignore.IgnoreWhitespace);
        Assert.False(DiffOptionKeys.TryResolve(options, ActionIds.DiffNextHunk, out var same));
        Assert.Equal(options, same);
    }

    [Fact]
    public void GivenOptions_WhenDescribing_ThenTheSentenceNamesContextAndWhitespace()
    {
        Assert.Equal("Diff: whole files", DiffOptionKeys.Describe(DiffOptions.Default with { WholeFile = true }));
        Assert.Equal(
            "Diff: 3 context lines, whitespace ignored",
            DiffOptionKeys.Describe(DiffOptions.Default with { ContextLines = 3, IgnoreWhitespace = true }));
    }

    private static DiffDocument Document(params string[] paths)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var path in paths)
        {
            builder.Add(DiffParser.Parse(Patch(path)).Items[0]);
        }

        return builder.Build([]);
    }

    private static string Patch(string path) =>
        $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n-old\n+new\n";
}
