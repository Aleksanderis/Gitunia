using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

[Trait("Os", "Independent")]
public class WorkingTreeRowRendererTests
{
    [Fact]
    public void GivenAModifiedFile_WhenRendering_ThenItShowsTheLetterAndThePathPaddedToTheWidth()
    {
        var line = WorkingTreeRowRenderer.Render(Modified("src/a.cs"), WorkingTreeSection.Unstaged, 20, false, GraphGlyphSet.Unicode);

        Assert.Equal("M src/a.cs          ", line.Text);
        Assert.Equal(SegmentRole.FileChanged, line.Segments[0].Role);
    }

    [Fact]
    public void GivenADeletedFile_WhenRendering_ThenTheLetterIsInTheDeletedRole()
    {
        var line = WorkingTreeRowRenderer.Render(Deleted("a"), WorkingTreeSection.Unstaged, 10, false, GraphGlyphSet.Unicode);

        Assert.StartsWith("D a", line.Text, StringComparison.Ordinal);
        Assert.Equal(SegmentRole.FileDeleted, line.Segments[0].Role);
    }

    [Fact]
    public void GivenAnUntrackedFile_WhenRendering_ThenTheLetterIsAQuestionMark()
    {
        var line = WorkingTreeRowRenderer.Render(Untracked("new.txt"), WorkingTreeSection.Unstaged, 10, false, GraphGlyphSet.Unicode);

        Assert.StartsWith("? new.txt", line.Text, StringComparison.Ordinal);
        Assert.Equal(SegmentRole.FileAdded, line.Segments[0].Role);
    }

    [Fact]
    public void GivenAStagedRename_WhenRendering_ThenItShowsTheOldAndTheNewPath()
    {
        var line = WorkingTreeRowRenderer.Render(StagedRename("old.txt", "new.txt"), WorkingTreeSection.Staged, 30, false, GraphGlyphSet.Unicode);

        Assert.StartsWith("R old.txt -> new.txt", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAStagedRenameEditedInTheWorkingTree_WhenRenderingEachList_ThenOnlyTheStagedRowShowsTheOldPath()
    {
        var entry = StagedRename("old.txt", "new.txt") with { WorktreeChange = FileChangeKind.Modified };

        var unstaged = WorkingTreeRowRenderer.Render(entry, WorkingTreeSection.Unstaged, 30, false, GraphGlyphSet.Unicode);
        var staged = WorkingTreeRowRenderer.Render(entry, WorkingTreeSection.Staged, 30, false, GraphGlyphSet.Unicode);

        Assert.StartsWith("M new.txt", unstaged.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("old.txt", unstaged.Text, StringComparison.Ordinal);
        Assert.StartsWith("R old.txt -> new.txt", staged.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAConflict_WhenRendering_ThenItShowsABangAndTheKindOfConflict()
    {
        var failures = new List<string>();
        (ConflictKind kind, string text)[] rows =
        [
            (ConflictKind.BothModified, "both modified"),
            (ConflictKind.BothAdded, "both added"),
            (ConflictKind.DeletedByThem, "deleted by them"),
            (ConflictKind.DeletedByUs, "deleted by us"),
            (ConflictKind.AddedByUs, "added by us"),
            (ConflictKind.AddedByThem, "added by them"),
            (ConflictKind.BothDeleted, "both deleted"),
        ];

        foreach (var (kind, text) in rows)
        {
            try
            {
                CheckItShowsABangAndTheKindOfConflict(kind, text);
            }
            catch (Exception exception)
            {
                failures.Add($"{kind}, {text}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckItShowsABangAndTheKindOfConflict(ConflictKind kind, string text)
    {
        var line = WorkingTreeRowRenderer.Render(Conflict("c.txt", kind), WorkingTreeSection.Unstaged, 40, false, GraphGlyphSet.Unicode);

        Assert.StartsWith($"! c.txt  {text}", line.Text, StringComparison.Ordinal);
        Assert.Equal(SegmentRole.Error, line.Segments[0].Role);
    }

    [Fact]
    public void GivenAFileChangedInBothSides_WhenRenderingEachList_ThenEachListShowsItsOwnSide()
    {
        var both = StagedAdded("x.txt") with { WorktreeChange = FileChangeKind.Modified };

        Assert.StartsWith("A ", WorkingTreeRowRenderer.Render(both, WorkingTreeSection.Staged, 10, false, GraphGlyphSet.Unicode).Text, StringComparison.Ordinal);
        Assert.StartsWith("M ", WorkingTreeRowRenderer.Render(both, WorkingTreeSection.Unstaged, 10, false, GraphGlyphSet.Unicode).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALongPath_WhenRendering_ThenItIsCutWithAnEllipsisToTheExactWidth()
    {
        var line = WorkingTreeRowRenderer.Render(Modified("a/very/long/path/to/a/file.txt"), WorkingTreeSection.Unstaged, 12, false, GraphGlyphSet.Unicode);

        Assert.Equal("M a/very/lo…", line.Text);
        Assert.Equal(12, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenAPathWithBytesThatAreNotUtf8_WhenRendering_ThenLoneSurrogatesBecomeReplacementCharacters()
    {
        var escaped = $"bad{(char)0xDCFF}name.txt";

        var line = WorkingTreeRowRenderer.Render(Modified(escaped), WorkingTreeSection.Unstaged, 20, false, GraphGlyphSet.Unicode);

        Assert.Equal("M bad�name.txt      ", line.Text);
        Assert.DoesNotContain(line.Text, char.IsSurrogate);
    }

    [Fact]
    public void GivenAValidSurrogatePair_WhenMakingPrintable_ThenItIsKept()
    {
        Assert.Equal("a\U0001F600b", WorkingTreeRowRenderer.Printable("a\U0001F600b"));
    }

    [Fact]
    public void GivenALoneHighSurrogate_WhenMakingPrintable_ThenItIsReplaced()
    {
        Assert.Equal("a�b", WorkingTreeRowRenderer.Printable($"a{(char)0xD800}b"));
    }

    [Fact]
    public void GivenAControlCharacterInAPath_WhenRendering_ThenItShowsAsASpace()
    {
        var line = WorkingTreeRowRenderer.Render(Modified("a\tb"), WorkingTreeSection.Unstaged, 10, false, GraphGlyphSet.Unicode);

        Assert.Equal("M a b     ", line.Text);
    }

    [Fact]
    public void GivenTheSelectedRow_WhenRendering_ThenTheLineIsSelected()
    {
        Assert.True(WorkingTreeRowRenderer.Render(Modified("a"), WorkingTreeSection.Unstaged, 10, true, GraphGlyphSet.Unicode).IsSelected);
    }
}
