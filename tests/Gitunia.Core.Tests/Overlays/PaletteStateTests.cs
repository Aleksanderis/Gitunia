using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

public class PaletteStateTests
{
    private static PaletteState Palette() => new(PaletteEntryBuilder.Build(DefaultKeymap.Create(), DefaultKeymap.GraphView));

    [Fact]
    public void GivenEmptyQuery_WhenListing_ThenEveryEntryIsVisible()
    {
        var palette = Palette();

        Assert.Equal(palette.Entries.Count, palette.Visible().Count);
    }

    [Fact]
    public void GivenAFuzzyQuery_WhenFiltering_ThenOnlySubsequenceMatchesRemainBestFirst()
    {
        var palette = Palette().Type("jmp");

        var visible = palette.Visible();

        Assert.NotEmpty(visible);
        Assert.Equal(ActionIds.GraphJumpToHead, visible[0].Action);
        Assert.All(visible, entry => Assert.True(FuzzyMatcher.Score("jmp", entry.Title) is not null || FuzzyMatcher.Score("jmp", entry.Action.Value) is not null));
    }

    [Fact]
    public void GivenQueryTypedInUpperCase_WhenFiltering_ThenMatchingIgnoresCase()
    {
        Assert.Equal(Palette().Type("head").Visible().Select(entry => entry.Action), Palette().Type("HEAD").Visible().Select(entry => entry.Action));
    }

    [Fact]
    public void GivenQueryMatchingNothing_WhenFiltering_ThenNothingIsSelected()
    {
        var palette = Palette().Type("zzzzqq");

        Assert.Empty(palette.Visible());
        Assert.Null(palette.Selected);
        Assert.Equal(0, palette.Move(3).SelectedIndex);
    }

    [Fact]
    public void GivenTypedText_WhenDeletingBack_ThenTheFilterWidensAgain()
    {
        var palette = Palette().Type("zz").DeleteLast().DeleteLast();

        Assert.Equal(string.Empty, palette.Query);
        Assert.Equal(palette.Entries.Count, palette.Visible().Count);
    }

    [Fact]
    public void GivenSelection_WhenMovingPastTheEnds_ThenItStops()
    {
        var palette = Palette();

        Assert.Equal(0, palette.Move(-1).SelectedIndex);
        Assert.Equal(palette.Entries.Count - 1, palette.Move(10_000).SelectedIndex);
    }

    [Fact]
    public void GivenSelection_WhenTypingMore_ThenSelectionReturnsToTheBestMatch()
    {
        var palette = Palette().Move(5).Type("q");

        Assert.Equal(0, palette.SelectedIndex);
    }

    [Fact]
    public void GivenEmptyQuery_WhenScoring_ThenItMatchesWithZero()
    {
        Assert.Equal(0, FuzzyMatcher.Score(string.Empty, "anything"));
    }

    [Fact]
    public void GivenCharactersOutOfOrder_WhenScoring_ThenThereIsNoMatch()
    {
        Assert.Null(FuzzyMatcher.Score("ba", "ab"));
    }

    [Fact]
    public void GivenContiguousAndScatteredMatch_WhenScoring_ThenContiguousWins()
    {
        Assert.True(FuzzyMatcher.Score("tab", "Next tab") > FuzzyMatcher.Score("tab", "Take a branch"));
    }

    [Fact]
    public void GivenTypedEmoji_WhenDeletingBack_ThenNoLoneSurrogateRemains()
    {
        var palette = Palette().Type("a").Type("\U0001F600").DeleteLast();

        Assert.Equal("a", palette.Query);
    }
}
