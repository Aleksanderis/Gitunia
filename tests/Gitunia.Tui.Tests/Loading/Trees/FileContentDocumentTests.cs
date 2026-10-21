using Gitunia.Core.Diff;
using Gitunia.Git.Blobs.Models;
using Gitunia.Tui.Loading.Trees;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Trees;

public class FileContentDocumentTests
{
    private static DiffDocument Text(string text) =>
        FileContentDocument.From("src/a.txt", new BlobContent(BlobKind.Text, text, text.Length));

    [Fact]
    public void GivenTextWithAFinalNewline_WhenBuilding_ThenEachLineIsARowAndTheLastNewlineAddsNone()
    {
        var document = Text("one\ntwo\n");

        Assert.Equal(["one", "two"], document.Rows.Select(row => row.Text));
        Assert.Equal([1, 2], document.Rows.Select(row => row.NewLineNumber!.Value));
        Assert.All(document.Rows, row => Assert.Equal(DiffRowKind.Context, row.Kind));
        Assert.Equal(2, document.MaximumLineNumber);
        Assert.Equal("src/a.txt", document.ContentPath);
        Assert.True(document.IsFileContent);
    }

    [Fact]
    public void GivenTextWithoutAFinalNewline_WhenBuilding_ThenTheLastLineIsStillARow()
    {
        Assert.Equal(["one", "two"], Text("one\ntwo").Rows.Select(row => row.Text));
    }

    [Fact]
    public void GivenCrlfAndBlankLines_WhenBuilding_ThenCarriageReturnsAreDroppedAndBlankLinesKept()
    {
        var document = Text("a\r\n\r\nb\r\n");

        Assert.Equal(["a", string.Empty, "b"], document.Rows.Select(row => row.Text));
    }

    [Fact]
    public void GivenOneBlankLine_WhenBuilding_ThenItIsOneRowNotAnEmptyFile()
    {
        var document = Text("\n");

        Assert.Single(document.Rows);
        Assert.Equal(DiffRowKind.Context, document.Rows[0].Kind);
    }

    [Fact]
    public void GivenEmptyText_WhenBuilding_ThenASingleNoteSaysSo()
    {
        var document = Text(string.Empty);

        Assert.Equal(DiffRowKind.Note, Assert.Single(document.Rows).Kind);
        Assert.Equal("Empty file", document.Rows[0].Text);
        Assert.Equal("src/a.txt", document.ContentPath);
    }

    [Fact]
    public void GivenABinaryBlob_WhenBuilding_ThenTheNoteNamesTheSizeAndShowsNoContent()
    {
        var document = FileContentDocument.From("x.bin", new BlobContent(BlobKind.Binary, string.Empty, 2048));

        Assert.Equal(DiffRowKind.Note, Assert.Single(document.Rows).Kind);
        Assert.Equal("Binary file (2.0 KB): its content is not shown", document.Rows[0].Text);
    }

    [Fact]
    public void GivenATooLargeBlob_WhenBuilding_ThenTheNoteNamesTheSizeAndTheLimit()
    {
        var document = FileContentDocument.From("x.txt", new BlobContent(BlobKind.TooLarge, string.Empty, 5 * 1024 * 1024));

        Assert.Equal("File too large to show (5.0 MB, the limit is 1.0 MB)", document.Rows[0].Text);
    }

    [Fact]
    public void GivenALoadedDocument_WhenCopyingItsText_ThenTheLinesComeBackWithoutDiffMarkers()
    {
        Assert.Equal("one\ntwo\n", FileContentDocument.Text(Text("one\r\ntwo")));
    }
}
