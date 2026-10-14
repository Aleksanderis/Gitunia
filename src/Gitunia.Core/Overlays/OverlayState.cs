using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>
/// The overlay part of the window state: nothing, the help for a view or the command palette, plus whether the
/// which-key popup is showing. Transitions are pure methods; the shell runs effects (mode switches) around them.
/// </summary>
/// <param name="Kind">The open overlay.</param>
/// <param name="HelpView">The view the help lists keys for.</param>
/// <param name="HelpScroll">First visible help line.</param>
/// <param name="Palette">The palette while it is open.</param>
/// <param name="ReturnMode">The input mode to restore when the overlay closes.</param>
/// <param name="WhichKeyVisible">The popup for a pending chord is showing.</param>
public sealed record OverlayState(
    OverlayKind Kind,
    string HelpView,
    int HelpScroll,
    PaletteState? Palette,
    KeyMode ReturnMode,
    bool WhichKeyVisible)
{
    public static OverlayState None { get; } = new(OverlayKind.None, string.Empty, 0, null, KeyMode.Normal, false);

    public bool IsOpen => Kind != OverlayKind.None;

    public static OverlayState OpenHelp(string view, KeyMode returnMode) =>
        new(OverlayKind.Help, view, 0, null, returnMode, false);

    public static OverlayState OpenMessages(KeyMode returnMode) =>
        new(OverlayKind.Messages, string.Empty, 0, null, returnMode, false);

    public static OverlayState OpenPalette(PaletteState palette, KeyMode returnMode) =>
        new(OverlayKind.Palette, string.Empty, 0, palette, returnMode, false);

    public OverlayState ScrollHelpTo(int line) => this with { HelpScroll = Math.Max(0, line) };

    public OverlayState WithPalette(PaletteState palette) => this with { Palette = palette };

    public OverlayState WithWhichKey(bool visible) => this with { WhichKeyVisible = visible };
}
