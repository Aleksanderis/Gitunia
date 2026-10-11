using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class DiffParserTests
{
    [Fact]
    public void GivenTwoHunks_WhenParsing_ThenRangesNumbersAndSectionAreRead()
    {
        var patch = DiffFixtures.TwoHunks;

        var result = DiffParser.Parse(patch);

        var file = Assert.Single(result.Items);
        Assert.Empty(result.Warnings);
        Assert.Equal("src/app.txt", file.Path);
        Assert.Equal(DiffFileStatus.Modified, file.Status);
        Assert.Equal("100644", file.OldMode);
        Assert.Equal("100644", file.NewMode);
        Assert.False(file.IsModeChange);
        Assert.Equal(2, file.Hunks.Count);
        var first = file.Hunks[0];
        Assert.Equal((3, 4, 3, 5), (first.OldStart, first.OldCount, first.NewStart, first.NewCount));
        Assert.Equal("void Run()", first.Section);
        Assert.Equal(
            [DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added, DiffLineKind.Added, DiffLineKind.Context, DiffLineKind.Context],
            first.Lines.Select(line => line.Kind));
        Assert.Equal(new DiffLine(DiffLineKind.Removed, "old", 4, null, false), first.Lines[1]);
        Assert.Equal(new DiffLine(DiffLineKind.Added, "new one", null, 4, false), first.Lines[2]);
        var second = file.Hunks[1];
        Assert.Equal((20, 1, 21, 1), (second.OldStart, second.OldCount, second.NewStart, second.NewCount));
        Assert.Equal(string.Empty, second.Section);
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void GivenCapturedDiff_WhenWrittenBack_ThenTextIsIdentical(string name, string patch)
    {
        var result = DiffParser.Parse(patch);

        Assert.True(name.Length > 0);
        Assert.Equal(patch, string.Concat(result.Items.Select(DiffPatchWriter.WriteFile)));
    }

    public static TheoryData<string, string> RoundTripCases() => new()
    {
        { "two hunks", DiffFixtures.TwoHunks },
        { "rename with edit", DiffFixtures.RenameWithEdit },
        { "pure rename", DiffFixtures.PureRename },
        { "copy", DiffFixtures.Copy },
        { "binary", DiffFixtures.BinaryDiffers },
        { "binary patch", DiffFixtures.BinaryPatch },
        { "mode change", DiffFixtures.ModeChange },
        { "no newline", DiffFixtures.NoNewlineBothSides },
        { "added and deleted", DiffFixtures.AddedAndDeleted },
        { "odd paths", DiffFixtures.OddPaths },
        { "header lookalikes", DiffFixtures.HeaderLookalikes },
    };

    [Fact]
    public void GivenRenameWithEdit_WhenParsing_ThenPathsAndSimilarityComeFromRenameLines()
    {
        var file = Assert.Single(DiffParser.Parse(DiffFixtures.RenameWithEdit).Items);

        Assert.Equal(DiffFileStatus.Renamed, file.Status);
        Assert.Equal("old name.txt", file.OldPath);
        Assert.Equal("new dir/new name.txt", file.NewPath);
        Assert.Equal(88, file.Similarity);
        Assert.Single(file.Hunks);
    }

    [Fact]
    public void GivenPureRenameAndCopy_WhenParsing_ThenStatusAndNoHunks()
    {
        var rename = Assert.Single(DiffParser.Parse(DiffFixtures.PureRename).Items);
        var copy = Assert.Single(DiffParser.Parse(DiffFixtures.Copy).Items);

        Assert.Equal((DiffFileStatus.Renamed, "a.txt", "b.txt", 100), (rename.Status, rename.OldPath, rename.NewPath, rename.Similarity));
        Assert.Empty(rename.Hunks);
        Assert.Equal((DiffFileStatus.Copied, "a.txt", "a copy.txt"), (copy.Status, copy.OldPath, copy.NewPath));
        Assert.Empty(copy.Hunks);
    }

    [Fact]
    public void GivenBinaryFiles_WhenParsing_ThenFlaggedWithoutHunks()
    {
        var plain = Assert.Single(DiffParser.Parse(DiffFixtures.BinaryDiffers).Items);
        var withPatch = Assert.Single(DiffParser.Parse(DiffFixtures.BinaryPatch).Items);

        Assert.True(plain.IsBinary);
        Assert.Empty(plain.Hunks);
        Assert.Equal("img/logo.png", plain.Path);
        Assert.True(withPatch.IsBinary);
        Assert.Contains("GIT binary patch", withPatch.HeaderLines);
        Assert.Contains("literal 5", withPatch.HeaderLines);
    }

    [Fact]
    public void GivenModeChangeOnly_WhenParsing_ThenModesDifferAndThereAreNoHunks()
    {
        var file = Assert.Single(DiffParser.Parse(DiffFixtures.ModeChange).Items);

        Assert.True(file.IsModeChange);
        Assert.Equal(("100644", "100755"), (file.OldMode, file.NewMode));
        Assert.Equal(DiffFileStatus.Modified, file.Status);
        Assert.Empty(file.Hunks);
        Assert.Equal("run it.sh", file.Path);
    }

    [Fact]
    public void GivenNoNewlineMarkers_WhenParsing_ThenFlagSitsOnTheRightLines()
    {
        var file = Assert.Single(DiffParser.Parse(DiffFixtures.NoNewlineBothSides).Items);

        var lines = file.Hunks.Single().Lines;
        Assert.Equal([DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added], lines.Select(line => line.Kind));
        Assert.Equal([false, true, true], lines.Select(line => line.NoNewlineAtEnd));
    }

    [Fact]
    public void GivenAddedAndDeletedFiles_WhenParsing_ThenOneSideIsNull()
    {
        var files = DiffParser.Parse(DiffFixtures.AddedAndDeleted).Items;

        Assert.Equal((DiffFileStatus.Added, null, "new.txt"), (files[0].Status, files[0].OldPath, files[0].NewPath));
        Assert.Equal((0, 0, 1, 2), (files[0].Hunks[0].OldStart, files[0].Hunks[0].OldCount, files[0].Hunks[0].NewStart, files[0].Hunks[0].NewCount));
        Assert.Equal((DiffFileStatus.Deleted, "gone.txt", null), (files[1].Status, files[1].OldPath, files[1].NewPath));
        Assert.Equal("gone.txt", files[1].Path);
    }

    [Fact]
    public void GivenQuotedAndSpacedPaths_WhenParsing_ThenPathsAreDecoded()
    {
        var files = DiffParser.Parse(DiffFixtures.OddPaths).Items;

        Assert.Equal(["tab\there.txt", "with space/日本 ü.txt", "quote\"d.txt"], files.Select(file => file.Path));
    }

    [Fact]
    public void GivenLinesThatLookLikeHeaders_WhenParsing_ThenTheyStayHunkContent()
    {
        var file = Assert.Single(DiffParser.Parse(DiffFixtures.HeaderLookalikes).Items);

        var hunk = Assert.Single(file.Hunks);
        Assert.Equal(["--- a/fake", "+++ b/fake", "diff --git a/x b/x", "@@ -1 +1 @@"], hunk.Lines.Select(line => line.Text).Take(4));
        Assert.Equal("notes.md", file.Path);
    }

    [Fact]
    public void GivenShowOutputWithMessageAndCombinedDiff_WhenParsing_ThenMessageIsIgnoredAndCombinedSkippedWithWarning()
    {
        var result = DiffParser.Parse(DiffFixtures.ShowWithCombined);

        var file = Assert.Single(result.Items);
        Assert.Equal("kept.txt", file.Path);
        Assert.Contains(result.Warnings, warning => warning.Contains("combined", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GivenTruncatedHunk_WhenParsing_ThenWarnsAndKeepsWhatWasRead()
    {
        var result = DiffParser.Parse(DiffFixtures.TruncatedHunk);

        var file = Assert.Single(result.Items);
        Assert.Equal(2, file.Hunks.Single().Lines.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("line counts", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenInvalidUtf8_WhenParsing_ThenWarnsOnce()
    {
        var parser = new DiffParser();
        var files = new List<DiffFile>();
        var bytes = System.Text.Encoding.UTF8.GetBytes(DiffFixtures.NoNewlineBothSides);
        bytes[^40] = 0xFF;
        bytes[^41] = 0xFE;

        parser.Feed(bytes, files);
        parser.Complete(files);

        Assert.Single(parser.Warnings, warning => warning.Contains("UTF-8", StringComparison.Ordinal));
        Assert.Single(files);
    }

    [Fact]
    public void GivenOutputFedOneByteAtATime_WhenParsing_ThenResultEqualsWholeParseAndBufferStaysOneLine()
    {
        var patch = DiffFixtures.TwoHunks + DiffFixtures.RenameWithEdit + DiffFixtures.AddedAndDeleted;
        var parser = new DiffParser();
        var files = new List<DiffFile>();
        var longestLine = patch.Split('\n').Max(line => line.Length);

        foreach (var value in System.Text.Encoding.UTF8.GetBytes(patch))
        {
            parser.Feed([value], files);
            Assert.True(parser.BufferedByteCount <= longestLine);
        }

        parser.Complete(files);

        Assert.Equal(string.Concat(DiffParser.Parse(patch).Items.Select(DiffPatchWriter.WriteFile)), string.Concat(files.Select(DiffPatchWriter.WriteFile)));
        Assert.Equal(4, files.Count);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenNothingAndNoWarnings()
    {
        var result = DiffParser.Parse(string.Empty);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }
}
