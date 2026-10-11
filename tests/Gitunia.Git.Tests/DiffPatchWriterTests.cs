using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class DiffPatchWriterTests
{
    private const string Header = "diff --git a/src/app.txt b/src/app.txt\nindex 1111111..2222222 100644\n--- a/src/app.txt\n+++ b/src/app.txt\n";

    private static DiffFile TwoHunkFile => DiffParser.Parse(DiffFixtures.TwoHunks).Items.Single();

    [Fact]
    public void GivenOnlyOneAddedLineSelected_WhenWritingForward_ThenRemovalBecomesContextAndOtherAdditionIsDropped()
    {
        var selection = Selection((0, [2]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Forward);

        Assert.Equal(
            Header + "@@ -3,4 +3,5 @@ void Run()\n context before\n old\n+new one\n context after\n tail\n",
            patch);
    }

    [Fact]
    public void GivenOnlyTheRemovalSelected_WhenWritingForward_ThenAdditionsAreDroppedAndCountsShrink()
    {
        var selection = Selection((0, [1]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Forward);

        Assert.Equal(Header + "@@ -3,4 +3,3 @@ void Run()\n context before\n-old\n context after\n tail\n", patch);
    }

    [Fact]
    public void GivenTwoHunksSelected_WhenWritingForward_ThenSecondHunkStartIsShiftedByTheFirst()
    {
        var selection = Selection((0, [1, 2, 3]), (1, [0, 1]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Forward);

        Assert.Contains("@@ -3,4 +3,5 @@ void Run()\n", patch);
        Assert.Contains("@@ -20,1 +21,1 @@\n-single\n+single changed\n", patch);
    }

    [Fact]
    public void GivenOnlyOneAddedLineSelected_WhenWritingReverse_ThenOtherAdditionStaysAsContext()
    {
        var selection = Selection((0, [2]));

        var patch = DiffPatchWriter.WriteSelection(TwoHunkFile, selection, PatchDirection.Reverse);

        Assert.Equal(
            Header + "@@ -3,4 +3,5 @@ void Run()\n context before\n+new one\n new two\n context after\n tail\n",
            patch);
    }

    [Fact]
    public void GivenOnlyContextOrNothingSelected_WhenWriting_ThenEmpty()
    {
        Assert.Equal(string.Empty, DiffPatchWriter.WriteSelection(TwoHunkFile, Selection((0, [0, 4])), PatchDirection.Forward));
        Assert.Equal(string.Empty, DiffPatchWriter.WriteSelection(TwoHunkFile, Selection(), PatchDirection.Forward));
    }

    [Fact]
    public void GivenOneSideOfNoNewlineChangeSelected_WhenWriting_ThenOtherSideIsCoupledAndMarkersStay()
    {
        var file = DiffParser.Parse(DiffFixtures.NoNewlineBothSides).Items.Single();

        var patch = DiffPatchWriter.WriteSelection(file, Selection((0, [2])), PatchDirection.Forward);

        Assert.EndsWith(" first\n-last\n\\ No newline at end of file\n+last changed\n\\ No newline at end of file\n", patch, StringComparison.Ordinal);
        Assert.Contains("@@ -1,2 +1,2 @@", patch, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenHunkIndexes_WhenWritingHunks_ThenOnlyThoseHunksFollowTheHeader()
    {
        var patch = DiffPatchWriter.WriteHunks(TwoHunkFile, [1]);

        Assert.Equal(Header + "@@ -20 +21 @@\n-single\n+single changed\n", patch);
        Assert.Equal("@@ -20 +21 @@\n-single\n+single changed\n", DiffPatchWriter.WriteHunk(TwoHunkFile.Hunks[1]));
    }

    private static Dictionary<int, IReadOnlyCollection<int>> Selection(params (int Hunk, int[] Lines)[] entries) =>
        entries.ToDictionary(entry => entry.Hunk, entry => (IReadOnlyCollection<int>)entry.Lines);
}
