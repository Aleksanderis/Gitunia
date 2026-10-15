using System.Text;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Diff;

public class DiffPatchWriterTests
{
    private const string Header = "diff --git a/src/app.txt b/src/app.txt\nindex 1111111..2222222 100644\n--- a/src/app.txt\n+++ b/src/app.txt\n";

    private static DiffFile TwoHunkFile => DiffParser.Parse(DiffFixtures.TwoHunks).Items.Single();

    [Fact]
    public void GivenOnlyOneAddedLineSelected_WhenWritingForward_ThenRemovalBecomesContextAndOtherAdditionIsDropped()
    {
        var selection = Selection(new HunkLineSelection(0, [2]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Forward);

        Assert.Equal(
            $"{Header}@@ -3,4 +3,5 @@ void Run()\n context before\n old\n+new one\n context after\n tail\n",
            patch);
    }

    [Fact]
    public void GivenOnlyTheRemovalSelected_WhenWritingForward_ThenAdditionsAreDroppedAndCountsShrink()
    {
        var selection = Selection(new HunkLineSelection(0, [1]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Forward);

        Assert.Equal($"{Header}@@ -3,4 +3,3 @@ void Run()\n context before\n-old\n context after\n tail\n", patch);
    }

    [Fact]
    public void GivenTwoHunksSelected_WhenWritingForward_ThenSecondHunkStartIsShiftedByTheFirst()
    {
        var selection = Selection(new HunkLineSelection(0, [1, 2, 3]), new HunkLineSelection(1, [0, 1]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Forward);

        Assert.Contains("@@ -3,4 +3,5 @@ void Run()\n", patch);
        Assert.Contains("@@ -20,1 +21,1 @@\n-single\n+single changed\n", patch);
    }

    [Fact]
    public void GivenOnlyOneAddedLineSelected_WhenWritingReverse_ThenOtherAdditionStaysAsContext()
    {
        var selection = Selection(new HunkLineSelection(0, [2]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Reverse);

        Assert.Equal(
            $"{Header}@@ -3,4 +3,5 @@ void Run()\n context before\n+new one\n new two\n context after\n tail\n",
            patch);
    }

    [Fact]
    public void GivenOnlyContextOrNothingSelected_WhenWriting_ThenEmpty()
    {
        Assert.Equal(string.Empty, DiffPatchWriter.WriteSelection(TwoHunkFile, Selection(new HunkLineSelection(0, [0, 4])), PatchDirection.Forward));
        Assert.Equal(string.Empty, DiffPatchWriter.WriteSelection(TwoHunkFile, Selection(), PatchDirection.Forward));
    }

    [Fact]
    public void GivenOneSideOfNoNewlineChangeSelected_WhenWriting_ThenOtherSideIsCoupledAndMarkersStay()
    {
        var file = DiffParser.Parse(DiffFixtures.NoNewlineBothSides).Items.Single();

        var patch = DiffPatchWriter.WriteSelection(file, Selection(new HunkLineSelection(0, [2])), PatchDirection.Forward);

        Assert.EndsWith(" first\n-last\n\\ No newline at end of file\n+last changed\n\\ No newline at end of file\n", patch, StringComparison.Ordinal);
        Assert.Contains("@@ -1,2 +1,2 @@", patch, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenHunkIndexes_WhenWritingHunks_ThenOnlyThoseHunksFollowTheHeader()
    {
        var patch = DiffPatchWriter.WriteHunks(TwoHunkFile, [1]);

        Assert.Equal($"{Header}@@ -20 +21 @@\n-single\n+single changed\n", patch);
        Assert.Equal("@@ -20 +21 @@\n-single\n+single changed\n", DiffPatchWriter.WriteHunk(TwoHunkFile, 1));
    }

    [Fact]
    public void GivenFileWithInvalidUtf8Content_WhenWritingAnyPatch_ThenRefused()
    {
        var file = ParseWithInvalidByte(inPath: false);

        Assert.True(file.HasInvalidText);
        Assert.Throws<InexactPatchException>(() => DiffPatchWriter.WriteSelection(file, Selection(new HunkLineSelection(0, [0])), PatchDirection.Forward));
        Assert.Throws<InexactPatchException>(() => DiffPatchWriter.WriteSelection(file, Selection(new HunkLineSelection(0, [0])), PatchDirection.Reverse));
        Assert.Throws<InexactPatchException>(() => DiffPatchWriter.WriteHunks(file, [0]));
        Assert.Throws<InexactPatchException>(() => DiffPatchWriter.WriteFile(file));
    }

    [Fact]
    public void GivenFileWithInvalidUtf8Path_WhenWritingSelection_ThenRefused()
    {
        var file = ParseWithInvalidByte(inPath: true);

        Assert.True(file.HasInvalidText);
        Assert.Throws<InexactPatchException>(() => DiffPatchWriter.WriteSelection(file, Selection(new HunkLineSelection(0, [0])), PatchDirection.Forward));
    }

    [Fact]
    public void GivenInvalidUtf8InOneFile_WhenParsingTwoFiles_ThenOnlyThatFileIsMarked()
    {
        var patch = Encoding.UTF8.GetBytes(DiffFixtures.TwoHunks + "\n").Concat(InvalidFileBytes(inPath: false)).ToArray();
        var parser = new DiffParser();
        var files = new List<DiffFile>();

        parser.Feed(patch, files);
        parser.Complete(files);

        Assert.Equal([false, true], files.Select(file => file.HasInvalidText));
        Assert.NotEmpty(DiffPatchWriter.WriteFile(files[0]));
    }

    private static DiffFile ParseWithInvalidByte(bool inPath)
    {
        var parser = new DiffParser();
        var files = new List<DiffFile>();
        parser.Feed(InvalidFileBytes(inPath), files);
        parser.Complete(files);
        return files.Single();
    }

    private static byte[] InvalidFileBytes(bool inPath)
    {
        var name = inPath ? "b\u00ff.txt" : "b.txt";
        var text = $"diff --git a/{name} b/{name}\nindex 1111111..2222222 100644\n--- a/{name}\n+++ b/{name}\n@@ -1 +1 @@\n-old\n+new @@\n";
        var bytes = Encoding.Latin1.GetBytes(text);
        // Latin-1 encodes U+00FF as the single byte 0xFF, which is not valid UTF-8; the content case plants it in the added line.
        return inPath ? bytes : Encoding.Latin1.GetBytes(text.Replace("new @@", "new \u00ff"));
    }

    private static Dictionary<int, IReadOnlyCollection<int>> Selection(params HunkLineSelection[] entries) =>
        entries.ToDictionary(entry => entry.Hunk, entry => (IReadOnlyCollection<int>)entry.Lines);
}
