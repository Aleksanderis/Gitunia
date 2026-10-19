using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class SettingsFrameBuilderTests
{
    private const string Home = "/home/user";

    [Fact]
    public void GivenTheDefaultSettings_WhenRenderingAt100x14_ThenTheCategoriesAndTheFirstSettingsShow()
    {
        var frame = SettingsFrameBuilder.Build(Open(), 100, 14, GraphGlyphSet.Unicode)!;

        Assert.Equal(
            string.Join(
                '\n',
                " Settings".PadRight(76),
                "│ / filter                                                                 │",
                "│  Appearance    │  Frame style                                [rounded ▾] │",
                "│  Repositories  │                                                         │",
                "│  Tabs          │                                                         │",
                "│  Config file   │                                                         │",
                "│                │                                                         │",
                "│ ──────────────────────────────────────────────────────────────────────── │",
                "│ Enter or l opens the category; j and k move between categories.          │",
                "│                                                                          │",
                "│ j/k move  l/Enter open  / filter  Esc close                              │",
                "╰──────────────────────────────────────────────────────────────────────────╯"),
            string.Join('\n', frame.Lines.Select(line => line.Text)));
        Assert.Equal(12, frame.Column);
    }

    [Fact]
    public void GivenAnyScreenSize_WhenRendering_ThenEveryLineHasTheWidthOfTheFrameAndNothingThrows()
    {
        foreach (var width in new[] { 1, 10, 17, 30, 39, 40, 60, 79, 80, 120 })
        {
            foreach (var height in new[] { 1, 4, 8, 11, 12, 20, 50 })
            {
                var frame = SettingsFrameBuilder.Build(Open(), width, height, GraphGlyphSet.Unicode);

                var isUniform = frame is null || frame.Lines.All(line => TextWidth.Of(line.Text) == frame.Width);
                Assert.True(isUniform, $"{width}x{height}");
                Assert.True(frame is null || frame.Column + frame.Width <= width, $"{width}x{height} fits the screen");
            }
        }
    }

    [Theory]
    [InlineData(39, 30)]
    [InlineData(80, 11)]
    public void GivenAScreenUnder40x12_WhenRendering_ThenOnlyATooSmallLineShows(int width, int height)
    {
        var frame = SettingsFrameBuilder.Build(Open(), width, height, GraphGlyphSet.Unicode)!;

        Assert.Equal(4, frame.Height);
        Assert.Contains("too small", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Equal(0, SettingsFrameBuilder.VisibleRows(width, height));
        Assert.Equal(SettingsHit.None, SettingsFrameBuilder.HitAt(Open(), 5, 2, width, height, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenAScreenUnder80Columns_WhenRendering_ThenCategoriesBecomeHeadingsOfOneList()
    {
        var state = Open().WithLayout(collapsed: true);

        var frame = SettingsFrameBuilder.Build(state, 60, 24, GraphGlyphSet.Unicode)!;
        var body = frame.Lines.Skip(2).Take(8).Select(line => line.Text).ToList();

        Assert.Contains(body, line => line.Contains("Appearance", StringComparison.Ordinal));
        Assert.Contains(body, line => line.Contains("Tabs", StringComparison.Ordinal));
        Assert.Contains(body, line => line.Contains("Show branch in tab name", StringComparison.Ordinal));
        Assert.All(body, line => Assert.Equal(2, line.Count(character => character == '│')));
    }

    [Fact]
    public void GivenTheCursorInTheSettingsColumn_WhenRendering_ThenOnlyThatRowIsSelectedAndTheCategoryIsDim()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.FrameStyleId));

        var frame = SettingsFrameBuilder.Build(state, 100, 30, GraphGlyphSet.Unicode)!;

        var row = frame.Lines[2].Segments;
        Assert.Contains(
            row,
            segment => segment.Selection == SegmentSelection.Dim && segment.Text.Contains("Appearance"));
        Assert.Contains(
            row,
            segment => segment.Selection == SegmentSelection.Focused && segment.Text.Contains("Frame style"));
        Assert.Equal(
            1,
            frame.Lines.Count(line => line.Segments.Any(segment => segment.Selection == SegmentSelection.Focused)));
    }

    [Fact]
    public void GivenAnOpenChoice_WhenRendering_ThenTheOptionsFollowTheRowWithTheCurrentOneMarked()
    {
        var state = Reduce(Open().Reveal(Definition(SettingsRegistry.FrameStyleId)), SettingsCommandKind.Activate);

        var frame = SettingsFrameBuilder.Build(state, 100, 30, GraphGlyphSet.Unicode)!;
        var text = frame.Lines.Select(line => line.Text).ToList();

        Assert.Contains("● rounded", text[3], StringComparison.Ordinal);
        Assert.Contains("○ square", text[4], StringComparison.Ordinal);
        Assert.Contains("Esc restore", text[^2], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheAsciiGlyphSet_WhenRenderingAChoice_ThenNoBoxDrawingOrArrowCharactersAppear()
    {
        var state = Reduce(Open().Reveal(Definition(SettingsRegistry.FrameStyleId)), SettingsCommandKind.Activate);

        var frame = SettingsFrameBuilder.Build(state, 100, 30, GraphGlyphSet.Ascii)!;

        Assert.All(frame.Lines, line => Assert.True(line.Text.All(character => character < 128), line.Text));
        Assert.Contains("(*) rounded", frame.Lines[3].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPathList_WhenRendering_ThenTheFoldersAreListedWithRemoveMarkersAndTheRowHasAnAddMarker()
    {
        var state = Open("{ \"scanRoots\": [\"/srv/git\", \"~/code\"] }")
            .Reveal(Definition(SettingsRegistry.ScanRootsId));

        var frame = SettingsFrameBuilder.Build(state, 100, 30, GraphGlyphSet.Unicode)!;
        var text = frame.Lines.Select(line => line.Text).ToList();

        Assert.Contains("Scan roots", text[2], StringComparison.Ordinal);
        Assert.Contains("2 folders [+]", text[2], StringComparison.Ordinal);
        Assert.Contains("/srv/git", text[3], StringComparison.Ordinal);
        Assert.Contains("[x]", text[3], StringComparison.Ordinal);
        Assert.Contains("~/code", text[4], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADefaultPathList_WhenRendering_ThenTheRowSaysTheDefaultFoldersApply()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.ScanRootsId));

        var frame = SettingsFrameBuilder.Build(state, 100, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains("default folders [+]", frame.Lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFolderBeingTyped_WhenRendering_ThenTheTextAndACursorShowOnTheNewRow()
    {
        var adding = Reduce(Open().Reveal(Definition(SettingsRegistry.ScanRootsId)), SettingsCommandKind.Add);
        var typing = adding.WithInputText("/sr");

        var frame = SettingsFrameBuilder.Build(typing, 100, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains("> /sr_", frame.Lines[3].Text, StringComparison.Ordinal);
        Assert.Contains("Enter save  Esc cancel", frame.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPendingRemoval_WhenRendering_ThenTheQuestionNamesTheFolderAndThePathStaysUntouched()
    {
        var state = Open("{ \"scanRoots\": [\"/srv/git\"] }").Reveal(
            Definition(SettingsRegistry.ScanRootsId),
            SettingRowKind.Item,
            0);
        var asked = Reduce(state, SettingsCommandKind.Remove);

        var frame = SettingsFrameBuilder.Build(asked, 100, 30, GraphGlyphSet.Unicode)!;
        var layout = SettingsLayout.For(100, 30)!;

        var question = frame.Lines[layout.RuleLine + 1].Text;
        Assert.Contains("Remove \"/srv/git\" from Scan roots?", question, StringComparison.Ordinal);
        Assert.Contains("The folder itself is not touched", question, StringComparison.Ordinal);
        Assert.Contains("y  remove it", frame.Lines[layout.SecondHelpLine].Text, StringComparison.Ordinal);
        Assert.Contains("n  keep it", frame.Lines[layout.SecondHelpLine].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFileThatCannotBeEdited_WhenRendering_ThenTheBannerAndTheReadOnlyTitleShow()
    {
        var frame = SettingsFrameBuilder.Build(Open("{ oops"), 100, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains("(read-only)", frame.Lines[0].Text, StringComparison.Ordinal);
        Assert.Contains("config.json cannot be edited", frame.Lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAValueThatIsNotValid_WhenRendering_ThenTheRowCarriesAWarningMarkerAndTheHelpLineTheReason()
    {
        var state = Open("{ \"frameStyle\": \"round\" }").Reveal(Definition(SettingsRegistry.FrameStyleId));

        var frame = SettingsFrameBuilder.Build(state, 100, 30, GraphGlyphSet.Unicode)!;
        var layout = SettingsLayout.For(100, 30)!;

        Assert.Contains("! Frame style", frame.Lines[2].Text, StringComparison.Ordinal);
        Assert.Contains("! frameStyle must be", frame.Lines[layout.RuleLine + 1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFilter_WhenRendering_ThenMatchesShowTheirCategoryAndTheCount()
    {
        var typing = Reduce(Open(), SettingsCommandKind.FilterOpen).WithFilterText("tab");
        var accepted = Reduce(typing, SettingsCommandKind.InputAccept);

        var frame = SettingsFrameBuilder.Build(accepted, 100, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains("/tab  1 match", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("Tabs: Show branch in tab name", frame.Lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFilterWithoutMatches_WhenRendering_ThenTheHelpLineSaysSo()
    {
        var typing = Reduce(Open(), SettingsCommandKind.FilterOpen).WithFilterText("zzz");

        var frame = SettingsFrameBuilder.Build(typing, 100, 30, GraphGlyphSet.Unicode)!;
        var layout = SettingsLayout.For(100, 30)!;

        Assert.Contains("No setting matches", frame.Lines[layout.RuleLine + 1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenLongHelp_WhenRenderingAtTheNarrowestLayout_ThenTheHelpIsCutWithAnEllipsisInsteadOfOverflowing()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.ScanRootsId));

        var frame = SettingsFrameBuilder.Build(state, 40, 12, GraphGlyphSet.Unicode)!;

        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTheTwoColumnLayout_WhenHittingTheCategoriesAndRows_ThenTheHitNamesTheCategoryOrRow()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.ShowBranchId));
        var left = SettingsFrameBuilder.HitAt(state, 3, 4, 100, 30, GraphGlyphSet.Unicode);
        var label = SettingsFrameBuilder.HitAt(state, 25, 2 + 0, 100, 30, GraphGlyphSet.Unicode);

        Assert.Equal(new SettingsHit(SettingsHitKind.Category, 2), left);
        Assert.Equal(SettingsHitKind.Row, label.Kind);
        Assert.Equal(SettingsHit.None, SettingsFrameBuilder.HitAt(state, 0, 4, 100, 30, GraphGlyphSet.Unicode));
        Assert.Equal(SettingsHit.None, SettingsFrameBuilder.HitAt(state, 3, 0, 100, 30, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenARowWithAControl_WhenHittingItsRightEnd_ThenTheHitIsTheControl()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.ShowBranchId));
        var layout = SettingsLayout.For(100, 30)!;
        var column = layout.Width - 4;

        var hit = SettingsFrameBuilder.HitAt(state, column, 2, 100, 30, GraphGlyphSet.Unicode);

        Assert.Equal(SettingsHitKind.Control, hit.Kind);
        Assert.Equal(0, hit.Index);
    }

    [Fact]
    public void GivenAPathList_WhenHittingItsMarkers_ThenTheAddAndRemoveHitsCarryTheRow()
    {
        var state = Open("{ \"scanRoots\": [\"/a\", \"/b\"] }").Reveal(Definition(SettingsRegistry.ScanRootsId));
        var layout = SettingsLayout.For(100, 30)!;
        var column = layout.Width - 3;

        var add = SettingsFrameBuilder.HitAt(state, column, 2, 100, 30, GraphGlyphSet.Unicode);
        var remove = SettingsFrameBuilder.HitAt(state, column, 4, 100, 30, GraphGlyphSet.Unicode);
        var label = SettingsFrameBuilder.HitAt(state, 22, 4, 100, 30, GraphGlyphSet.Unicode);

        Assert.Equal(new SettingsHit(SettingsHitKind.Add, 0), add);
        Assert.Equal(new SettingsHit(SettingsHitKind.Remove, 2), remove);
        Assert.Equal(new SettingsHit(SettingsHitKind.Row, 2), label);
    }

    [Fact]
    public void GivenTheRemovalQuestion_WhenHittingItsAnswerLine_ThenTheLeftHalfIsYesAndTheRightHalfIsNo()
    {
        var state = Open("{ \"scanRoots\": [\"/a\"] }")
            .Reveal(Definition(SettingsRegistry.ScanRootsId), SettingRowKind.Item, 0);
        var asked = Reduce(state, SettingsCommandKind.Remove);
        var layout = SettingsLayout.For(100, 30)!;

        var yes = SettingsFrameBuilder.HitAt(asked, 5, layout.SecondHelpLine, 100, 30, GraphGlyphSet.Unicode);
        var noColumn = layout.Width - 5;
        var no = SettingsFrameBuilder.HitAt(asked, noColumn, layout.SecondHelpLine, 100, 30, GraphGlyphSet.Unicode);

        Assert.Equal(SettingsHitKind.ConfirmYes, yes.Kind);
        Assert.Equal(SettingsHitKind.ConfirmNo, no.Kind);
    }

    [Fact]
    public void GivenTheCollapsedLayout_WhenHittingAHeading_ThenNothingIsHit()
    {
        var state = Open().WithLayout(collapsed: true);

        var heading = SettingsFrameBuilder.HitAt(state, 5, 2, 60, 24, GraphGlyphSet.Unicode);
        var row = SettingsFrameBuilder.HitAt(state, 5, 3, 60, 24, GraphGlyphSet.Unicode);

        Assert.Equal(SettingsHit.None, heading);
        Assert.Equal(new SettingsHit(SettingsHitKind.Row, 0), row);
    }

    [Fact]
    public void GivenALongList_WhenTheCursorIsFarDown_ThenTheWindowScrollsToKeepItVisible()
    {
        var paths = string.Join(", ", Enumerable.Range(0, 40).Select(number => $"\"/srv/{number}\""));
        var state = Open($"{{ \"scanRoots\": [{paths}] }}")
            .Reveal(Definition(SettingsRegistry.ScanRootsId), SettingRowKind.Item, 35);

        var frame = SettingsFrameBuilder.Build(state, 100, 20, GraphGlyphSet.Unicode)!;

        Assert.Contains(frame.Lines, line => line.Text.Contains("/srv/35", StringComparison.Ordinal));
        Assert.DoesNotContain(frame.Lines, line => line.Text.Contains("/srv/0 ", StringComparison.Ordinal));
    }

    private static SettingDefinition Definition(string id) =>
        SettingsRegistry.All.First(definition => definition.Id == id);

    private static SettingsState Open(string text = "{}")
    {
        var load = SettingsValueReader.Read(new ConfigSnapshot(true, text, "hash"), Home, _ => true);

        return SettingsState.Create([.. SettingsRegistry.All], load);
    }

    private static SettingsState Reduce(SettingsState state, SettingsCommandKind command) =>
        SettingsReducer.Reduce(state, command, new SettingsEnvironment(Home, _ => true)).State;
}
