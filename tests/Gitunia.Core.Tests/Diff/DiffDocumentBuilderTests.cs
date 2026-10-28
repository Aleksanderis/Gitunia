using System.Text;
using Gitunia.Core.Diff;
using Gitunia.Git.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

public class DiffDocumentBuilderTests
{
    private const string TwoFiles =
        "diff --git a/one.txt b/one.txt\n"
        + "index 111..222 100644\n"
        + "--- a/one.txt\n"
        + "+++ b/one.txt\n"
        + "@@ -1,2 +1,2 @@\n"
        + " keep\n"
        + "-old\n"
        + "+new\n"
        + "\\ No newline at end of file\n"
        + "@@ -10 +10 @@\n"
        + "-x\n"
        + "+y\n"
        + "diff --git a/old.txt b/new.txt\n"
        + "similarity index 100%\n"
        + "rename from old.txt\n"
        + "rename to new.txt\n"
        + "diff --git a/img.png b/img.png\n"
        + "index 111..222 100644\n"
        + "Binary files a/img.png and b/img.png differ\n";

    [Fact]
    public void GivenTwoHunksAndMoreFiles_WhenBuilding_ThenAnchorsPointAtHeaders()
    {
        var document = Build(TwoFiles);

        Assert.Equal(3, document.FileRows.Count);
        Assert.Equal(2, document.HunkRows.Count);
        Assert.All(document.FileRows, row => Assert.Equal(DiffRowKind.FileHeader, document.Rows[row].Kind));
        Assert.All(document.HunkRows, row => Assert.Equal(DiffRowKind.HunkHeader, document.Rows[row].Kind));
        Assert.Equal("M one.txt", document.Rows[0].Text);
    }

    [Fact]
    public void GivenTwoHunksInAFile_WhenBuilding_ThenABlankRowSeparatesThemAndNoneComesBeforeTheFirst()
    {
        var document = Build(TwoFiles);

        var second = document.HunkRows[1];
        Assert.Equal(DiffRowKind.Note, document.Rows[second - 1].Kind);
        Assert.Equal(string.Empty, document.Rows[second - 1].Text);
        Assert.Equal(DiffRowKind.HunkHeader, document.Rows[document.HunkRows[0]].Kind);
        Assert.Equal(DiffRowKind.FileHeader, document.Rows[document.HunkRows[0] - 1].Kind);
        Assert.Single(document.Rows, row => row.Kind == DiffRowKind.Note && row.Text.Length == 0);
    }

    [Fact]
    public void GivenTwoHunksAndMoreFiles_WhenTakingTheFirstFile_ThenOnlyItsRowsAndHunksRemain()
    {
        var document = Build(TwoFiles).ForFile(0);

        Assert.Equal([0], document.FileRows);
        Assert.Equal([["one.txt"]], document.FilePaths);
        Assert.Equal(2, document.HunkRows.Count);
        Assert.All(document.Rows, row => Assert.Equal(0, row.FileIndex));
        Assert.DoesNotContain(document.Rows, row => row.Text.Contains("new.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenARenamedFile_WhenTakingIt_ThenItStartsAtItsHeaderAndKeepsBothPaths()
    {
        var document = Build(TwoFiles).ForFile(1);

        Assert.Equal(DiffRowKind.FileHeader, document.Rows[0].Kind);
        Assert.Equal([["new.txt", "old.txt"]], document.FilePaths);
        Assert.Empty(document.HunkRows);
        Assert.Equal(2, document.Rows.Count);
    }

    [Fact]
    public void GivenAWarningAboveTheFiles_WhenTakingTheFirstFileOnly_ThenTheWarningStaysAbove()
    {
        var whole = Build(TwoFiles, "careful");

        Assert.Equal(DiffRowKind.Warning, whole.ForFile(0).Rows[0].Kind);
        Assert.Equal(1, whole.ForFile(0).FileRows[0]);
        Assert.Equal(DiffRowKind.FileHeader, whole.ForFile(2).Rows[0].Kind);
    }

    [Fact]
    public void GivenAChangedLine_WhenBuilding_ThenKindAndLineNumbersFollowTheHunk()
    {
        var document = Build(TwoFiles);

        var removed = document.Rows.First(row => row.Kind == DiffRowKind.Removed);
        var added = document.Rows.First(row => row.Kind == DiffRowKind.Added);
        Assert.Equal(2, removed.OldLineNumber);
        Assert.Null(removed.NewLineNumber);
        Assert.Null(added.OldLineNumber);
        Assert.Equal(2, added.NewLineNumber);
        Assert.Equal(10, document.MaximumLineNumber);
    }

    [Fact]
    public void GivenNoNewlineMarker_WhenBuilding_ThenItGetsItsOwnRow()
    {
        var document = Build(TwoFiles);

        Assert.Contains(document.Rows, row => row.Kind == DiffRowKind.NoNewline);
    }

    [Fact]
    public void GivenARenameWithoutEdits_WhenBuilding_ThenHeaderShowsBothPathsAndANoteExplains()
    {
        var document = Build(TwoFiles);

        var header = document.Rows[document.FileRows[1]];
        Assert.Equal("R old.txt -> new.txt", header.Text);
        Assert.Equal(DiffRowKind.Note, document.Rows[document.FileRows[1] + 1].Kind);
        Assert.Equal(1, document.FileIndexOf("old.txt"));
        Assert.Equal(1, document.FileIndexOf("new.txt"));
    }

    [Fact]
    public void GivenABinaryFile_WhenBuilding_ThenOnlyANoteFollowsItsHeader()
    {
        var document = Build(TwoFiles);

        var start = document.FileRows[2];
        Assert.Equal("Binary file, content not shown", document.Rows[start + 1].Text);
        Assert.Equal(start + 2, document.Rows.Count);
    }

    [Fact]
    public void GivenWarnings_WhenBuilding_ThenTheyComeFirstAndAnchorsShift()
    {
        var plain = Build(TwoFiles);

        var warned = Build(TwoFiles, "invalid UTF-8 was replaced");

        Assert.Equal(DiffRowKind.Warning, warned.Rows[0].Kind);
        Assert.Equal(plain.FileRows.Select(row => row + 1), warned.FileRows);
        Assert.Equal(plain.HunkRows.Select(row => row + 1), warned.HunkRows);
    }

    [Fact]
    public void GivenAnUnknownPath_WhenLookingForTheFile_ThenTheIndexIsMinusOne()
    {
        Assert.Equal(-1, Build(TwoFiles).FileIndexOf("missing.txt"));
    }

    [Fact]
    public void GivenAWholeFileWithTwoDistantChanges_WhenBuilding_ThenEachChangeGroupIsAnAnchorWhereItsContextStarts()
    {
        var document = BuildWholeFile(WholeFilePatch(changedLines: [5, 25], lineCount: 30), 3);

        Assert.Equal(2, document.HunkRows.Count);
        var first = document.HunkRows[0];
        var second = document.HunkRows[1];
        Assert.Equal("line 2", document.Rows[first].Text);
        Assert.Equal("line 22", document.Rows[second].Text);
        Assert.DoesNotContain(document.Rows.Index().Where(item => item.Item.Kind == DiffRowKind.HunkHeader), item => document.HunkRows.Contains(item.Index));
    }

    [Fact]
    public void GivenAWholeFileWithChangesWithinTwiceTheContext_WhenBuilding_ThenTheyAreOneGroup()
    {
        var near = BuildWholeFile(WholeFilePatch(changedLines: [5, 12], lineCount: 30), 3);
        var apart = BuildWholeFile(WholeFilePatch(changedLines: [5, 13], lineCount: 30), 3);

        Assert.Single(near.HunkRows);
        Assert.Equal(2, apart.HunkRows.Count);
    }

    [Fact]
    public void GivenAWholeFileAndNoContext_WhenBuilding_ThenAnchorsAreTheChangedRowsThemselves()
    {
        var document = BuildWholeFile(WholeFilePatch(changedLines: [1, 2, 9], lineCount: 12), 0);

        Assert.Equal(2, document.HunkRows.Count);
        Assert.All(document.HunkRows, row => Assert.Equal(DiffRowKind.Removed, document.Rows[row].Kind));
    }

    [Fact]
    public void GivenOneHunkWithSeveralChangeGroups_WhenGroupingChanges_ThenEachGroupStartsAtItsFirstChangedRow()
    {
        var document = BuildGrouped(WholeFilePatch(changedLines: [2, 4, 6], lineCount: 8));

        Assert.Equal(3, document.HunkRows.Count);
        Assert.All(document.HunkRows, row => Assert.Equal(DiffRowKind.Removed, document.Rows[row].Kind));
        Assert.Equal(["old 2", "old 4", "old 6"], document.HunkRows.Select(row => document.Rows[row].Text));
    }

    [Fact]
    public void GivenARemovedAndAnAddedLineTogether_WhenGroupingChanges_ThenTheyAreOneGroup()
    {
        var document = BuildGrouped(WholeFilePatch(changedLines: [3], lineCount: 6));

        Assert.Single(document.HunkRows);
    }

    private static DiffDocument BuildGrouped(string patch)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var file in DiffParser.Parse(patch).Items)
        {
            builder.Add(file, groupChanges: true);
        }

        return builder.Build([]);
    }

    private static string WholeFilePatch(int[] changedLines, int lineCount)
    {
        var builder = new StringBuilder();
        builder.Append("diff --git a/f.txt b/f.txt\nindex 111..222 100644\n--- a/f.txt\n+++ b/f.txt\n");
        builder.Append($"@@ -1,{lineCount} +1,{lineCount} @@\n");
        for (var number = 1; number <= lineCount; number++)
        {
            if (changedLines.Contains(number))
            {
                builder.Append($"-old {number}\n+line {number}\n");
            }
            else
            {
                builder.Append($" line {number}\n");
            }
        }

        return builder.ToString();
    }

    private static DiffDocument BuildWholeFile(string patch, int context)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var file in DiffParser.Parse(patch).Items)
        {
            builder.Add(file, context);
        }

        return builder.Build([]);
    }

    private static DiffDocument Build(string patch, params string[] warnings)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var file in DiffParser.Parse(patch).Items)
        {
            builder.Add(file);
        }

        return builder.Build(warnings);
    }
}
