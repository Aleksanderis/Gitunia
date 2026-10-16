using Gitunia.Core.Diff;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

public class DiffSelectionTests
{
    private const string TwoHunks =
        "diff --git a/one.txt b/one.txt\n"
        + "--- a/one.txt\n"
        + "+++ b/one.txt\n"
        + "@@ -1,3 +1,3 @@\n"
        + " keep\n"
        + "-old\n"
        + "+new\n"
        + " tail\n"
        + "@@ -20,2 +20,3 @@\n"
        + " ctx\n"
        + "+added one\n"
        + "+added two\n";

    [Fact]
    public void GivenTheCursorInTheSecondHunk_WhenSelectingTheHunk_ThenItsChangedLinesAreReturnedWithTheirNumbers()
    {
        var document = Build(TwoHunks);
        var cursor = document.HunkRows[1] + 2;

        var lines = DiffSelection.OfHunk(document, cursor);

        Assert.Equal(
            [
                new ChangedLine(DiffLineKind.Added, 21, "added one"),
                new ChangedLine(DiffLineKind.Added, 22, "added two"),
            ],
            lines);
    }

    [Fact]
    public void GivenTheCursorOnTheFileHeader_WhenSelectingTheHunk_ThenTheFirstHunkIsUsed()
    {
        var document = Build(TwoHunks);

        var lines = DiffSelection.OfHunk(document, 0);

        Assert.Equal(
            [
                new ChangedLine(DiffLineKind.Removed, 2, "old"),
                new ChangedLine(DiffLineKind.Added, 2, "new"),
            ],
            lines);
    }

    [Fact]
    public void GivenARangeAcrossContextAndHeaders_WhenSelectingRows_ThenOnlyChangedLinesAreReturned()
    {
        var document = Build(TwoHunks);

        var lines = DiffSelection.OfRows(document, 0, document.Rows.Count - 1);

        Assert.Equal(4, lines.Count);
        Assert.DoesNotContain(lines, line => line.Text is "keep" or "tail" or "ctx");
    }

    [Fact]
    public void GivenADiffWithoutHunks_WhenSelectingTheHunk_ThenNothingIsReturned()
    {
        var document = Build("diff --git a/img.png b/img.png\nindex 111..222 100644\nBinary files a/img.png and b/img.png differ\n");

        Assert.Empty(DiffSelection.OfHunk(document, 1));
        Assert.Equal(-1, DiffSelection.HunkIndexAt(document, 1));
    }

    [Fact]
    public void GivenRowsBetweenAndAfterHunks_WhenAskingForTheHunkIndex_ThenItIsTheHunkTheRowIsIn()
    {
        var document = Build(TwoHunks);

        Assert.Equal(0, DiffSelection.HunkIndexAt(document, document.HunkRows[0]));
        Assert.Equal(0, DiffSelection.HunkIndexAt(document, document.HunkRows[1] - 1));
        Assert.Equal(1, DiffSelection.HunkIndexAt(document, document.Rows.Count - 1));
    }

    private static DiffDocument Build(string patch)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var file in DiffParser.Parse(patch).Items)
        {
            builder.Add(file);
        }

        return builder.Build([]);
    }
}
