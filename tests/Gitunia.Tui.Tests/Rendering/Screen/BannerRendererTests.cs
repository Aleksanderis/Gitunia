using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Operations;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Screen;

public class BannerRendererTests
{
    private static readonly OperationBanner Rebase = new(
        "REBASING feature/x",
        "step 2/5  3 conflicts",
        [
            new BannerChip("<space>gc", "continue", ActionIds.OperationContinue),
            new BannerChip("<space>ga", "abort", ActionIds.OperationAbort),
            new BannerChip("<space>gk", "skip", ActionIds.OperationSkip),
        ]);

    [Fact]
    public void GivenABannerAndATallScreen_WhenComposing_ThenItReplacesTheBottomEdgeAndKeepsTheStatusBar()
    {
        var lines = Compose(Model(Rebase), 100, 12);

        Assert.Equal(12, lines.Count);
        Assert.StartsWith(
            " REBASING feature/x   step 2/5  3 conflicts  <space>gc continue  <space>ga abort  <space>gk skip",
            lines[^2].Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("╰", lines[^2].Text, StringComparison.Ordinal);
        Assert.StartsWith(" NORMAL ", lines[^1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REBASING", lines[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenABanner_WhenBuilding_ThenTheTitleChipStartsAtColumnZeroInTheWarningRole()
    {
        var segments = BannerRenderer.Build(Rebase, 100, GraphGlyphSet.Unicode).Line.Segments;

        Assert.Equal(SegmentRole.StatusOperation, segments[0].Role);
        Assert.StartsWith(" REBASING", segments[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAHintAndNarrowWidth_WhenBuilding_ThenTheShortHintReplacesTheLongOneAndTheChipsStayWhole()
    {
        var banner = Rebase with
        {
            Detail = "3 conflicts",
            Chips = Rebase.Chips.RemoveAt(2),
            Hint = "<space>to open conflict editor",
            ShortHint = "<space>to conflict editor",
        };

        var tight = BannerRenderer.Build(banner, 100, GraphGlyphSet.Unicode);
        var narrow = BannerRenderer.Build(banner, 80, GraphGlyphSet.Unicode);
        var wide = BannerRenderer.Build(banner, 160, GraphGlyphSet.Unicode);

        Assert.Equal(2, narrow.Chips.Count);
        Assert.DoesNotContain("conflict editor", narrow.Line.Text, StringComparison.Ordinal);
        Assert.Contains("<space>ga abort", narrow.Line.Text, StringComparison.Ordinal);
        Assert.Contains("<space>to conflict editor", tight.Line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<space>to open", tight.Line.Text, StringComparison.Ordinal);
        Assert.Contains("<space>to open", wide.Line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenThreeChipsAndTheCountsAt80Columns_WhenBuilding_ThenTheCountsGoSoNoChipIsCut()
    {
        var line = BannerRenderer.Build(Rebase, 80, GraphGlyphSet.Unicode);

        Assert.Equal(3, line.Chips.Count);
        Assert.Contains("<space>gk skip", line.Line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("step 2/5", line.Line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoBanner_WhenComposing_ThenTheBottomEdgeStaysTheFrame()
    {
        var lines = Compose(Model(null), 100, 12);

        Assert.StartsWith("╰", lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAShortScreen_WhenComposing_ThenTheStatusBarCarriesTheTextAndTheEdgeStaysTheFrame()
    {
        var lines = Compose(Model(Rebase), 100, ScreenLayout.MinimumHeightWithBanner - 1);

        Assert.StartsWith("╰", lines[^2].Text, StringComparison.Ordinal);
        Assert.Contains(
            "REBASING feature/x  step 2/5  3 conflicts  <space>gc continue",
            lines[^1].Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAShortScreenAndAMessage_WhenComposing_ThenTheMessageWinsTheStatusBar()
    {
        var model = Model(Rebase) with { Message = "Staged a.txt" };

        var lines = Compose(model, 100, ScreenLayout.MinimumHeightWithBanner - 1);

        Assert.Contains("Staged a.txt", lines[^1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REBASING", lines[^1].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(100)]
    public void GivenAnyWidth_WhenBuilding_ThenTheLineIsExactlyAsWideAndChipsStayInside(int width)
    {
        var banner = BannerRenderer.Build(Rebase, width, GraphGlyphSet.Unicode);

        Assert.Equal(width, TextWidth.Of(banner.Line.Text));
        Assert.All(banner.Chips, area => Assert.True(area.Start + area.Width <= width));
    }

    [Fact]
    public void GivenTheDrawnBanner_WhenLookingUpColumns_ThenEachChipFindsItsMouseView()
    {
        var banner = BannerRenderer.Build(Rebase, 100, GraphGlyphSet.Unicode);

        foreach (var (key, view) in new[]
        {
            ("<space>gc", DefaultMouseMap.BannerContinueButton),
            ("<space>ga", DefaultMouseMap.BannerAbortButton),
            ("<space>gk", DefaultMouseMap.BannerSkipButton),
        })
        {
            var column = banner.Line.Text.IndexOf(key, StringComparison.Ordinal);
            Assert.Equal(view, banner.ViewAt(column));
            Assert.Equal(view, banner.ViewAt(column + key.Length + 2));
        }

        Assert.Null(banner.ViewAt(2));
        Assert.Null(banner.ViewAt(99));
    }

    [Fact]
    public void GivenAMerge_WhenBuilding_ThenThereIsNoSkipChip()
    {
        var merge = Rebase with { Chips = [.. Rebase.Chips.Take(2)] };

        var banner = BannerRenderer.Build(merge, 100, GraphGlyphSet.Unicode);

        Assert.Equal(2, banner.Chips.Count);
        Assert.DoesNotContain("skip", banner.Line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheChipActions_WhenMappingToViews_ThenTheMouseMapRunsTheSameAction()
    {
        var map = DefaultMouseMap.CreateBindings();

        foreach (var chip in Rebase.Chips)
        {
            var view = BannerRenderer.ViewOf(chip.Action);
            Assert.Contains(map, binding => binding.View == view && binding.Action == chip.Action);
        }
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenComposing_ThenTheBannerStillReplacesTheEdge()
    {
        var lines = ScreenComposer.Compose(Model(Rebase), Graph(), 0, 0, 100, 12, GraphGlyphSet.Ascii);

        Assert.Contains("REBASING feature/x", lines[^2].Text, StringComparison.Ordinal);
    }

    private static ScreenModel Model(OperationBanner? banner) =>
        new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Banner: banner);

    private static IReadOnlyList<StyledLine> Compose(ScreenModel model, int width, int height) =>
        [.. ScreenComposer.Compose(model, Graph(), 0, 0, width, height, GraphGlyphSet.Unicode)];

    private static CommitGraph Graph() => CommitFactory.Graph(CommitFactory.Commit(1, "root", []));
}
