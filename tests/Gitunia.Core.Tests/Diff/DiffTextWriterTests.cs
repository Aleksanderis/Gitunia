using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

public class DiffTextWriterTests
{
    [Theory]
    [InlineData(DiffRowKind.Added, "+x")]
    [InlineData(DiffRowKind.Removed, "-x")]
    [InlineData(DiffRowKind.Context, " x")]
    [InlineData(DiffRowKind.HunkHeader, "x")]
    public void GivenARow_WhenWritingALine_ThenThePrefixMatchesItsKind(DiffRowKind kind, string expected)
    {
        Assert.Equal(expected, DiffTextWriter.Line(new DiffRow(kind, "x", null, null, 0)));
    }

    [Fact]
    public void GivenTwoFiles_WhenWritingOne_ThenOnlyItsRowsAreIncluded()
    {
        var rows = new List<DiffRow>
        {
            new(DiffRowKind.FileHeader, "M a.txt", null, null, 0),
            new(DiffRowKind.Added, "one", null, 1, 0),
            new(DiffRowKind.FileHeader, "M b.txt", null, null, 1),
            new(DiffRowKind.Removed, "two", 1, null, 1),
        };
        var document = new DiffDocument(rows, [], [0, 2], [["a.txt"], ["b.txt"]], 1);

        Assert.Equal("M b.txt\n-two\n", DiffTextWriter.File(document, 1));
        Assert.Equal(string.Empty, DiffTextWriter.File(document, 5));
    }
}
