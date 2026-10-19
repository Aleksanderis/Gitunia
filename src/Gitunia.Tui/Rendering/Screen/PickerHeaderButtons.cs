using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// The buttons at the right end of the picker's hint line: clone and init, the mouse path of <c>Alt-c</c> and
/// <c>Alt-i</c>. Narrow screens show none (the hint text matters more); locating a missing repository has none.
/// </summary>
public static class PickerHeaderButtons
{
    /// <summary>The hint line is this row of the picker.</summary>
    public const int Row = 1;

    /// <summary>Screens narrower than this show no buttons; the hint text needs the room before.</summary>
    public const int MinimumWidth = 84;

    private const string CloneLabel = "[clone]";
    private const string InitLabel = "[init]";

    /// <summary>
    /// The buttons on a line of <paramref name="width"/> columns, left to right, with a space after the last.
    /// </summary>
    public static IReadOnlyList<TitleButton> Layout(int width)
    {
        if (width < MinimumWidth)
        {
            return [];
        }

        var start = width - TextWidth.Of(CloneLabel) - TextWidth.Of(InitLabel) - 2;
        return
        [
            new TitleButton(DefaultMouseMap.PickerCloneButton, CloneLabel, start),
            new TitleButton(DefaultMouseMap.PickerInitButton, InitLabel, start + TextWidth.Of(CloneLabel) + 1),
        ];
    }
}
