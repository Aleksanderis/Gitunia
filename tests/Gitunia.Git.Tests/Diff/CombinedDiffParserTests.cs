using System.Text;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Xunit;

namespace Gitunia.Git.Tests.Diff;

public class CombinedDiffParserTests
{
    private const string Conflict = """
        diff --cc "we ird.txt"
        index 1111111,2222222..0000000
        --- a/we ird.txt
        +++ b/we ird.txt
        @@@ -1,3 -1,3 +1,7 @@@ void Run()
          a
        ++<<<<<<< HEAD
         +ours
        ++=======
        + theirs
        ++>>>>>>> topic
         -old
          c
        \ No newline at end of file
        diff --git a/other.txt b/other.txt
        index 3333333..4444444 100644
        --- a/other.txt
        +++ b/other.txt
        @@ -1 +1 @@
        -x
        +y

        """;

    [Fact]
    public void GivenACombinedDiff_WhenParsingWithCombinedSupport_ThenLinesCollapseToOneOldNewPairAndTheNextFileFollows()
    {
        var files = Parse(Conflict, readsCombined: true);

        Assert.Equal(["we ird.txt", "other.txt"], files.Select(file => file.Path));
        var hunk = files[0].Hunks.Single();
        Assert.Equal("void Run()", hunk.Section);
        Assert.Equal(
            [
                (DiffLineKind.Context, "a", 1),
                (DiffLineKind.Added, "<<<<<<< HEAD", 2),
                (DiffLineKind.Added, "ours", 3),
                (DiffLineKind.Added, "=======", 4),
                (DiffLineKind.Added, "theirs", 5),
                (DiffLineKind.Added, ">>>>>>> topic", 6),
                (DiffLineKind.Removed, "old", null),
                (DiffLineKind.Context, "c", 7),
            ],
            hunk.Lines.Select(line => (line.Kind, line.Text, line.NewLineNumber)));
        Assert.True(hunk.Lines[^1].NoNewlineAtEnd);
        Assert.Equal(DiffLineKind.Removed, files[1].Hunks.Single().Lines[0].Kind);
    }

    [Fact]
    public void GivenACombinedDiff_WhenParsingWithoutCombinedSupport_ThenItIsSkippedWithAWarning()
    {
        var parser = new DiffParser();
        var files = new List<DiffFile>();

        parser.Feed(Encoding.UTF8.GetBytes(Conflict), files);
        parser.Complete(files);

        Assert.Equal(["other.txt"], files.Select(file => file.Path));
        Assert.Contains(parser.Warnings, warning => warning.StartsWith("Skipped combined diff", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenACombinedDiffFedInTinyChunks_WhenParsing_ThenTheResultIsTheSame()
    {
        var parser = new DiffParser(readsCombined: true);
        var files = new List<DiffFile>();
        var bytes = Encoding.UTF8.GetBytes(Conflict);

        for (var offset = 0; offset < bytes.Length; offset += 7)
        {
            parser.Feed(bytes.AsSpan(offset, Math.Min(7, bytes.Length - offset)), files);
        }

        parser.Complete(files);

        Assert.Equal(Parse(Conflict, readsCombined: true).Select(file => file.Hunks.Sum(hunk => hunk.Lines.Count)), files.Select(file => file.Hunks.Sum(hunk => hunk.Lines.Count)));
    }

    [Fact]
    public void GivenAHunkHeaderThatCannotBeRead_WhenParsingACombinedDiff_ThenTheBodyStaysInTheHunkAndAWarningIsRaised()
    {
        const string patch = "diff --cc f.txt\nindex 1,2..3\n--- a/f.txt\n+++ b/f.txt\n@@@ garbage @@@\n  one\n++two\n";
        var parser = new DiffParser(readsCombined: true);
        var files = new List<DiffFile>();

        parser.Feed(Encoding.UTF8.GetBytes(patch), files);
        parser.Complete(files);

        var file = Assert.Single(files);
        Assert.Equal(["one", "two"], file.Hunks.Single().Lines.Select(line => line.Text));
        Assert.Contains(parser.Warnings, warning => warning.StartsWith("Could not read the hunk header", StringComparison.Ordinal));
    }

    private static List<DiffFile> Parse(string patch, bool readsCombined)
    {
        var parser = new DiffParser(readsCombined: readsCombined);
        var files = new List<DiffFile>();
        parser.Feed(Encoding.UTF8.GetBytes(patch), files);
        parser.Complete(files);
        return files;
    }
}
