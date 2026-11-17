using System.Collections.Immutable;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

/// <summary>
/// The filter list contract (docs/internal/ui-consistency.md, section e): every surface that filters a list says "no
/// match" with <c>UiTexts.NoMatch</c> in the empty-text role (not as a normal row), says loading the same way, shows the
/// position at the right end of the title, and is moved by the same keys (Tab, Shift-Tab, Up, Down, Ctrl-j, Ctrl-k) and
/// the wheel. A new filter surface adds its name to <see cref="TextSurfaces"/> and, when it is a list, to
/// <see cref="ListSurfaces"/>, and its builder to <see cref="FrameOf"/>.
/// </summary>
[Trait("Os", "Independent")]
public class FilterListContractTests
{
    private const string Query = "zzzzqq";

    private const int Width = 100;

    private const int Height = 30;

    private const string HomeDirectory = "/home/user";

    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    /// <summary>Every surface with a filter and a no-match text.</summary>
    public static TheoryData<string> TextSurfaces => ["prompt-picker", "palette", "reflog-picker", "help-filter", "settings-filter"];

    /// <summary>The surfaces that are a <c>FilterList</c> of rows: loading, position in the title, keys and wheel.</summary>
    public static TheoryData<string> ListSurfaces => ["prompt-picker", "palette", "reflog-picker"];

    [Theory]
    [MemberData(nameof(TextSurfaces))]
    public void GivenAQueryThatMatchesNothing_WhenRendering_ThenTheNoMatchTextIsInTheEmptyTextRole(string surface)
    {
        var frame = FrameOf(surface, NoMatchState(surface));

        var segment = SegmentWith(frame, NoMatchText(surface));

        Assert.Equal(SegmentRole.EmptyText, segment.Role);
    }

    [Theory]
    [MemberData(nameof(ListSurfaces))]
    public void GivenAListStillLoading_WhenRendering_ThenTheLoadingTextIsInTheEmptyTextRole(string surface)
    {
        var frame = FrameOf(surface, LoadingState(surface));

        var segment = SegmentWith(frame, UiTexts.Loading);

        Assert.Equal(SegmentRole.EmptyText, segment.Role);
    }

    [Theory]
    [MemberData(nameof(ListSurfaces))]
    public void GivenAListWithRows_WhenRendering_ThenThePositionIsAtTheRightEndOfTheTitle(string surface)
    {
        var frame = FrameOf(surface, RowsState(surface));

        var title = frame.Lines[0];

        Assert.EndsWith("1/3 ", title.Text, StringComparison.Ordinal);
        Assert.Equal(frame.Width, TextWidth.Of(title.Text));
    }

    [Theory]
    [MemberData(nameof(ListSurfaces))]
    public void GivenAListWithoutMatches_WhenRendering_ThenTheTitleHasNoPosition(string surface)
    {
        var frame = FrameOf(surface, NoMatchState(surface));

        Assert.DoesNotMatch(@"\d+/\d+ $", frame.Lines[0].Text);
    }

    [Theory]
    [MemberData(nameof(ListSurfaces))]
    public void GivenAListSurface_WhenPressingTabAndShiftTab_ThenTheHighlightMovesDownAndUp(string surface)
    {
        var (view, mode, next, previous) = KeysOf(surface);

        Assert.Equal(next, ActionOf(view, mode, KeyNames.Tab, KeyModifiers.None));
        Assert.Equal(previous, ActionOf(view, mode, KeyNames.Tab, KeyModifiers.Shift));
        Assert.Equal(next, ActionOf(view, mode, KeyNames.Down, KeyModifiers.None));
        Assert.Equal(previous, ActionOf(view, mode, KeyNames.Up, KeyModifiers.None));
        Assert.Equal(next, ActionOf(view, mode, "j", KeyModifiers.Ctrl));
        Assert.Equal(previous, ActionOf(view, mode, "k", KeyModifiers.Ctrl));
    }

    [Theory]
    [MemberData(nameof(ListSurfaces))]
    public void GivenAListSurface_WhenTheWheelTurns_ThenTheHighlightMoves(string surface)
    {
        var wheel = WheelOf(surface);
        var bindings = DefaultMouseMap.CreateBindings();

        Assert.Equal(wheel.Next, bindings.Single(binding => binding.View == wheel.View && binding.Gesture == MouseGesture.WheelDown).Action);
        Assert.Equal(wheel.Previous, bindings.Single(binding => binding.View == wheel.View && binding.Gesture == MouseGesture.WheelUp).Action);
    }

    private static ActionId? ActionOf(string view, KeyMode mode, string name, KeyModifiers modifiers)
    {
        var sequence = new KeySequence([new KeyEvent(name, modifiers)]);

        return Table.Bindings
            .FirstOrDefault(binding => binding.View == view && binding.Mode == mode && binding.Sequence.Equals(sequence))
            ?.Action;
    }

    private static KeySurface KeysOf(string surface) => surface == "palette"
        ? new KeySurface(DefaultKeymap.PaletteView, KeyMode.Command, ActionIds.PaletteNext, ActionIds.PalettePrevious)
        : new KeySurface(DefaultKeymap.FilterListView, KeyMode.Insert, ActionIds.FilterNext, ActionIds.FilterPrevious);

    // The prompt popup has its own wheel view (it is drawn over any pane); the others take the view of their keys.
    private static WheelSurface WheelOf(string surface) => surface switch
    {
        "prompt-picker" => new WheelSurface(DefaultMouseMap.PromptPopupView, ActionIds.ListDown, ActionIds.ListUp),
        "palette" => new WheelSurface(DefaultKeymap.PaletteView, ActionIds.PaletteNext, ActionIds.PalettePrevious),
        _ => new WheelSurface(DefaultKeymap.FilterListView, ActionIds.FilterNext, ActionIds.FilterPrevious),
    };

    private static object NoMatchState(string surface) => surface switch
    {
        "prompt-picker" => PromptPicker.Of(Choices(), UiTexts.NoMatch("item"), Query),
        "palette" => Palette().Type(Query),
        "reflog-picker" => new ReflogRefPicker(["HEAD", "main", "topic"], TextField.Of(Query)),
        "help-filter" => HelpBuilder.Filter(HelpBuilder.Build(Table, DefaultKeymap.GraphView), Query),
        "settings-filter" => SettingsWithFilter(),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };

    private static object LoadingState(string surface) => surface switch
    {
        "prompt-picker" => new PromptPicker(PromptPicker.Of([], UiTexts.NoMatch("item"), string.Empty).List.Loading()),
        "palette" => new PaletteState(Palette().List.WithItems([]).Loading()),
        "reflog-picker" => new ReflogRefPicker(new ReflogRefPicker([]).List.Loading()),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };

    private static object RowsState(string surface) => surface switch
    {
        "prompt-picker" => PromptPicker.Of(Choices(), UiTexts.NoMatch("item"), string.Empty),
        "palette" => new PaletteState(Palette().Entries.Take(3).ToList()),
        "reflog-picker" => new ReflogRefPicker(["HEAD", "main", "topic"]),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };

    private static string NoMatchText(string surface) => surface switch
    {
        "prompt-picker" => UiTexts.NoMatch("item"),
        "palette" => UiTexts.NoMatch("action"),
        "reflog-picker" => UiTexts.NoMatch("branch"),
        "help-filter" => UiTexts.NoMatch("action"),
        "settings-filter" => UiTexts.NoMatch("setting"),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };

    private static OverlayFrame FrameOf(string surface, object state)
    {
        var frame = state switch
        {
            PromptPicker picker => PromptPopupFrameBuilder.Build(
                PromptPopup.ForFilter("Pick", "Which one", picker.List.Field, picker.Rows(), "choose"),
                Width,
                Height,
                GraphGlyphSet.Unicode),
            PaletteState palette => PaletteFrameBuilder.Build(palette, Width, Height, GraphGlyphSet.Unicode),
            ReflogRefPicker picker => ReflogFrameBuilder.Build(
                ReflogWith(picker),
                Width,
                Height,
                GraphGlyphSet.Unicode,
                DateTimeOffset.UnixEpoch),
            HelpTab page => HelpFrameBuilder.Build(
                [page],
                0,
                DefaultKeymap.GraphView,
                0,
                Width,
                Height,
                GraphGlyphSet.Unicode,
                filter: TextField.Of(Query)),
            SettingsState settings => SettingsFrameBuilder.Build(settings, Width, Height, GraphGlyphSet.Unicode),
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
        };

        return frame ?? throw new InvalidOperationException($"{surface} drew no frame");
    }

    private static StyledSegment SegmentWith(OverlayFrame frame, string text) =>
        frame.Lines.SelectMany(line => line.Segments).First(segment => segment.Text.Contains(text, StringComparison.Ordinal));

    private static ReflogState ReflogWith(ReflogRefPicker picker) =>
        new ReflogState(new TabId(1), "HEAD", "HEAD", []) with { Picker = picker };

    private static ImmutableArray<PromptChoice> Choices() => [new("one", "one"), new("two", "two"), new("three", "three")];

    private static PaletteState Palette() => new(PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView));

    private static SettingsState SettingsWithFilter()
    {
        var load = SettingsValueReader.Read(new ConfigSnapshot(true, "{}", "hash"), HomeDirectory, _ => true);
        var state = SettingsState.Create([.. SettingsRegistry.All], load);

        return state.WithFilterText(TextField.Of(Query));
    }

    private sealed record WheelSurface(string View, ActionId Next, ActionId Previous);

    private sealed record KeySurface(string View, KeyMode Mode, ActionId Next, ActionId Previous);
}
