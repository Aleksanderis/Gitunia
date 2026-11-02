using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Files;

public class PathRowLayoutTests
{
    private const string Ellipsis = "…";

    [Fact]
    public void GivenAWideRow_WhenLayingOutNameFirst_ThenTheNameIsBoldAndTheDirectoryFaintAfterIt()
    {
        var segments = PathRowLayout.Segments(FilePathStyle.NameFirst, "src/Core/Git/parser.cs", null, 60, Ellipsis);

        Assert.Equal(
            [
                new StyledSegment("parser.cs", SegmentRole.FileName),
                new StyledSegment("  src/Core/Git", SegmentRole.Faint),
            ],
            segments);
    }

    [Fact]
    public void GivenAFileInTheRoot_WhenLayingOutNameFirst_ThenOnlyTheNameShows()
    {
        var segments = PathRowLayout.Segments(FilePathStyle.NameFirst, "README.md", null, 30, Ellipsis);

        Assert.Equal([new StyledSegment("README.md", SegmentRole.FileName)], segments);
    }

    [Fact]
    public void GivenTheFullStyle_WhenLayingOut_ThenItIsThePathAsBeforeWhateverTheWidth()
    {
        Assert.Equal("src/a/b.cs", PathRowLayout.Text(FilePathStyle.FullPath, "src/a/b.cs", null, 3, Ellipsis));
        Assert.Equal("old.cs -> new.cs", PathRowLayout.Text(FilePathStyle.FullPath, "new.cs", "old.cs", 3, Ellipsis));
    }

    [Theory]
    [InlineData("src/very/long/directory/path/parser.cs", null, 40, "parser.cs  src/very/long/directory/path")]
    [InlineData("src/very/long/directory/path/parser.cs", null, 30, "parser.cs  …ong/directory/path")]
    [InlineData("src/very/long/directory/path/parser.cs", null, 20, "parser.cs  …ory/path")]
    [InlineData("src/very/long/directory/path/parser.cs", null, 14, "parser.cs  …th")]
    [InlineData("src/very/long/directory/path/parser.cs", null, 13, "parser.cs  …h")]
    [InlineData("src/very/long/directory/path/parser.cs", null, 12, "parser.cs")]
    [InlineData("src/very/long/directory/path/parser.cs", null, 9, "parser.cs")]
    [InlineData("src/a-very-long-file-name.cs", null, 8, "a-very-long-file-name.cs")] // The line builder cuts the name.
    [InlineData("修正修正修正/a.cs", null, 9, "a.cs  …正")] // A wide character in the directory is never split.
    [InlineData("src/new-folder/", null, 40, "new-folder/  src")] // The trailing separator stays with the name.
    [InlineData("src/new.cs", "src/old.cs", 40, "new.cs  src <- old.cs")] // A rename in one directory.
    [InlineData("lib/new.cs", "src/old.cs", 40, "new.cs  lib <- src/old.cs")] // A rename into another directory.
    [InlineData("new.cs", "old.cs", 40, "new.cs <- old.cs")] // A rename in the root.
    [InlineData("src/Core/new.cs", "src/Core/old.cs", 20, "new.cs  src/Core")] // The old path goes before the cut.
    [InlineData("a/very.cs", null, 10, "very.cs  a")] // A one-column directory that fits whole has no ellipsis.
    [InlineData("修修/a.cs", null, 8, "a.cs")] // A wide character leaves only the ellipsis: the directory is dropped.
    public void GivenANarrowingRow_WhenLayingOutNameFirst_ThenTheDirectoryIsCutFromTheLeftAndDroppedBeforeTheName(
        string path,
        string? oldPath,
        int columns,
        string expected)
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, path, oldPath, columns, Ellipsis);

        Assert.Equal(expected, text);
    }
}
