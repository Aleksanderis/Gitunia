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
        Assert.Equal("R100 old.txt -> new.txt", header.Text);
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
