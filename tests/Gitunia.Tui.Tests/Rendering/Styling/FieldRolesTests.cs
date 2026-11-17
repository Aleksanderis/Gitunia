using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
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

    public static TheoryData<string> Surfaces => ["prompt popup", "palette", "sidebar filter", "footer bar"];

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
        "sidebar filter" => new FieldLine(SidebarRowRenderer.FilterLine(Typed, true, 30, GraphGlyphSet.Unicode, CaretAt), FieldRoles.Body),
        "footer bar" => new FieldLine(
            OverlayBox.Hints([], 40, GraphGlyphSet.Unicode, field: new CaretField("/", Typed, CaretAt)),
            FieldRoles.Bar),
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown surface"),
    };

    private static StyledLine PromptPopupLine()
    {
        var prompt = PromptPopup.ForText("T", "C", Typed, "ok") with { InputCaret = CaretAt };
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
