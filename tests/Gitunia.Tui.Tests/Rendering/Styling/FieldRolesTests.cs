using Gitunia.App.Blame;
using Gitunia.Core.Blame;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Picker;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

/// <summary>
/// Frame check over the fields of the app: typed text is drawn in the typed-text role of its surface and the caret cell
/// in the caret role, never in a role the caller picked. A new field adds a row to <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class FieldRolesTests
{
    private const string Typed = "abcd";
    private const int CaretAt = 1;

    public static TheoryData<string> Surfaces =>
    [
        "prompt popup",
        "palette",
        "sidebar filter",
        "footer bar",
        "help filter",
        "settings filter",
        "reflog search",
        "reflog picker",
        "blame search",
        "picker query",
        "status bar",
    ];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAField_WhenDrawn_ThenTheTypedTextAndTheCaretCellHaveTheRolesOfTheirSurface(string surface)
    {
        var field = LineOf(surface);

        var typed = TextOf(field.Line, field.Roles.TypedText);
        var caret = TextOf(field.Line, field.Roles.Caret);

        Assert.Contains("a", typed, StringComparison.Ordinal);
        Assert.Contains("cd", typed, StringComparison.Ordinal);
        Assert.Equal("b", caret);
    }

    [Theory]
    [InlineData("prompt popup")]
    [InlineData("sidebar filter")]
    public void GivenAFieldInABodyRow_WhenDrawn_ThenTheTypedTextIsTheTypedTextRole(string surface)
    {
        var line = LineOf(surface).Line;

        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.TypedText && segment.Text == "a");
        Assert.DoesNotContain(line.Segments, segment => segment.Role == SegmentRole.OverlayKey && segment.Text == "a");
    }

    [Fact]
    public void GivenAnEmptyFieldWithAPlaceholder_WhenDrawn_ThenThePlaceholderFollowsTheCaretInThePlaceholderRole()
    {
        var builder = new LineBuilder(30, "…");

        new CaretField("> ", string.Empty, PromptCaret.End, Placeholder: "type to filter").AppendTo(builder, FieldRoles.Body);
        var line = builder.Build();

        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.Faint && segment.Text == " type to filter");
        Assert.Equal(" ", CursorCell.Of(line));
    }

    [Fact]
    public void GivenAFieldWithText_WhenDrawn_ThenNoPlaceholderShows()
    {
        var builder = new LineBuilder(30, "…");

        new CaretField("> ", "x", PromptCaret.End, Placeholder: "type to filter").AppendTo(builder, FieldRoles.Body);

        Assert.DoesNotContain("type to filter", builder.Build().Text, StringComparison.Ordinal);
    }

    private static FieldLine LineOf(string surface) => surface switch
    {
        "prompt popup" => new FieldLine(PromptPopupLine(), FieldRoles.Body),
        "palette" => new FieldLine(PaletteTitle(), FieldRoles.Title),
        "sidebar filter" => new FieldLine(SidebarRowRenderer.FilterLine(new TextField(Typed, CaretAt), true, 30, GraphGlyphSet.Unicode), FieldRoles.Body),
        "footer bar" => new FieldLine(
            OverlayBox.Hints([], 40, GraphGlyphSet.Unicode, field: new CaretField("/", Typed, CaretAt)),
            FieldRoles.Bar),
        "help filter" => new FieldLine(HelpFilterTitle(), FieldRoles.Title),
        "settings filter" => new FieldLine(SettingsFilterRow(), FieldRoles.Body),
        "reflog search" => new FieldLine(ReflogFooter(search: true), FieldRoles.Bar),
        "reflog picker" => new FieldLine(ReflogFooter(search: false), FieldRoles.Bar),
        "blame search" => new FieldLine(BlameFooter(), FieldRoles.Bar),
        "picker query" => new FieldLine(PickerQuery(), FieldRoles.Body),
        "status bar" => new FieldLine(StatusBar(), FieldRoles.Bar),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown surface"),
    };

    private static StyledLine HelpFilterTitle()
    {
        var tabs = HelpBuilder.Build(DefaultKeymap.Create(), DefaultKeymap.GraphView);
        var frame = HelpFrameBuilder.Build(
            [HelpBuilder.Filter(tabs, string.Empty)],
            0,
            DefaultKeymap.GraphView,
            0,
            80,
            24,
            GraphGlyphSet.Unicode,
            filter: new TextField(Typed, CaretAt),
            isEditingFilter: true);

        return frame!.Lines[0];
    }

    private static StyledLine SettingsFilterRow()
    {
        var load = SettingsValueReader.Read(new ConfigSnapshot(true, "{}", "hash"), "/home/user", _ => true);
        var state = SettingsState.Create([.. SettingsRegistry.All], load)
            .WithFilterText(new TextField(Typed, CaretAt)) with { IsEditingFilter = true };

        return SettingsFrameBuilder.Build(state, 80, 15, GraphGlyphSet.Unicode)!.Lines[1];
    }

    private static StyledLine ReflogFooter(bool search)
    {
        var state = new ReflogState(new TabId(1), "HEAD", "HEAD", []);
        state = search
            ? state.OpenSearch().EditSearch(new TextField(Typed, CaretAt))
            : state with { Picker = new ReflogRefPicker(["HEAD"], new TextField(Typed, CaretAt)) };

        return ReflogFrameBuilder.Build(state, 80, 24, GraphGlyphSet.Unicode, DateTimeOffset.UnixEpoch)!.Lines[^2];
    }

    private static StyledLine PickerQuery()
    {
        var picker = new PickerState(new TextField(Typed, CaretAt), 0, [], string.Empty, null);
        var lines = PickerComposer.Compose(picker, "/home/user", 100, 30, GraphGlyphSet.Unicode);

        return lines.First(line => line.Text.Contains(Typed[..1], StringComparison.Ordinal)
            && line.Segments.Any(segment => segment.Role == FieldRoles.Body.Caret));
    }

    private static StyledLine StatusBar()
    {
        var model = new ScreenModel("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty)
        {
            SearchLine = new PromptLine("/", Typed, CaretAt, "  1/2"),
        };

        return ChromeBuilder.Status(model, 80, GraphGlyphSet.Unicode);
    }

    private static StyledLine BlameFooter()
    {
        var state = new BlameState(
            new TabId(1),
            "a.txt",
            "a.txt",
            LineCount: 1,
            Search: new TextField(Typed, CaretAt),
            IsEditingSearch: true);
        var data = new BlameData(1, ["a"]);

        return BlameFrameBuilder.Build(state, data, DateTimeOffset.UnixEpoch, 100, 30, GraphGlyphSet.Unicode)!.Lines[^2];
    }

    private static StyledLine PromptPopupLine()
    {
        var prompt = PromptPopup.ForText("T", "C", new TextField(Typed, CaretAt), "ok");
        var frame = PromptPopupFrameBuilder.Build(prompt, 80, 24, GraphGlyphSet.Unicode);

        return frame!.Lines[2];
    }

    private static StyledLine PaletteTitle()
    {
        var palette = new PaletteState([]).Type(Typed);
        palette = palette.Edit(new KeyEvent(KeyNames.Home));
        palette = palette.Edit(new KeyEvent(KeyNames.Right));
        var frame = PaletteFrameBuilder.Build(palette, 80, 24, GraphGlyphSet.Unicode);

        return frame!.Lines[0];
    }

    private static string TextOf(StyledLine line, SegmentRole role) =>
        string.Concat(line.Segments.Where(segment => segment.Role == role).Select(segment => segment.Text));

    private sealed record FieldLine(StyledLine Line, FieldRoles Roles);
}
