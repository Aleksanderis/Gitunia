using Gitunia.Core.Editing;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Overlays;

/// <summary>
/// The overlay part of the window state: nothing, the help for a view or the command palette, plus whether the
/// which-key popup is showing. Transitions are pure methods; the shell runs effects (mode switches) around them.
/// </summary>
/// <param name="Kind">The open overlay.</param>
/// <param name="HelpView">The view the help lists keys for.</param>
/// <param name="HelpScroll">First visible help line.</param>
/// <param name="HelpTab">Index of the shown help tab.</param>
/// <param name="Palette">The palette while it is open.</param>
/// <param name="Confirm">The question while a confirm dialog is open.</param>
/// <param name="ReturnMode">The input mode to restore when the overlay closes.</param>
/// <param name="WhichKeyVisible">The popup for a pending chord is showing.</param>
/// <param name="MenuAnchor">Where the actions menu hangs while it is open (the row it was opened on).</param>
/// <param name="HelpFilter">What was typed in the help filter (<c>/</c>); empty when the help is not filtered.</param>
/// <param name="IsEditingHelpFilter">The filter line is taking typed text.</param>
/// <param name="HelpFilterCaret">The caret column in <see cref="HelpFilter"/> (see <see cref="PromptCaret"/>).</param>
/// <param name="MenuTitle">The title of the open menu; null for the default <c>Actions</c>.</param>
public sealed record OverlayState(
    OverlayKind Kind,
    string HelpView,
    int HelpScroll,
    PaletteState? Palette,
    ConfirmPrompt? Confirm,
    KeyMode ReturnMode,
    bool WhichKeyVisible,
    MenuAnchor? MenuAnchor = null,
    string? MenuTitle = null,
    int HelpTab = 0,
    string HelpFilter = "",
    bool IsEditingHelpFilter = false,
    int HelpFilterCaret = PromptCaret.End)
{
    public static OverlayState None { get; } = new(OverlayKind.None, string.Empty, 0, null, null, KeyMode.Normal, false);

    public bool IsOpen => Kind != OverlayKind.None;

    public static OverlayState OpenHelp(string view, KeyMode returnMode) =>
        new(OverlayKind.Help, view, 0, null, null, returnMode, false);

    public static OverlayState OpenMessages(KeyMode returnMode) =>
        new(OverlayKind.Messages, string.Empty, 0, null, null, returnMode, false);

    /// <summary>Opens the output overlay; <paramref name="title"/> is kept in <see cref="HelpView"/>, which the output overlay does not otherwise use.</summary>
    public static OverlayState OpenOutput(string title, KeyMode returnMode) =>
        new(OverlayKind.Output, title, 0, null, null, returnMode, false);

    public static OverlayState OpenPalette(PaletteState palette, KeyMode returnMode) =>
        new(OverlayKind.Palette, string.Empty, 0, palette, null, returnMode, false);

    /// <summary>Opens the actions menu of a row; its entries are those of <paramref name="menu"/>.</summary>
    public static OverlayState OpenContextMenu(
        PaletteState menu,
        MenuAnchor anchor,
        KeyMode returnMode,
        string? title = null) =>
        new(OverlayKind.ContextMenu, string.Empty, 0, menu, null, returnMode, false, anchor, title);

    /// <summary>Opens a confirm dialog; the keys are <c>y</c>, <c>n</c> and <c>Esc</c> only.</summary>
    public static OverlayState OpenConfirm(ConfirmPrompt prompt, KeyMode returnMode) =>
        new(OverlayKind.Confirm, string.Empty, 0, null, prompt, returnMode, false);

    public OverlayState ScrollHelpTo(int line) => this with { HelpScroll = Math.Max(0, line) };

    /// <summary>Shows another help tab, from its first line.</summary>
    public OverlayState ShowHelpTab(int tab) => this with { HelpTab = Math.Max(0, tab), HelpScroll = 0 };

    /// <summary>True while the help shows the filtered rows or the filter line takes text.</summary>
    public bool IsHelpFiltered => Kind == OverlayKind.Help && (HelpFilter.Length > 0 || IsEditingHelpFilter);

    /// <summary>Starts a new help filter: the line takes text.</summary>
    public OverlayState OpenHelpFilter() => this with { HelpFilter = string.Empty, HelpFilterCaret = PromptCaret.End, IsEditingHelpFilter = true, HelpScroll = 0 };

    /// <summary>Applies a key to the help filter (text at the caret, caret moves, deletions); the first line shows again when the text changes. Unchanged when the key does nothing.</summary>
    public OverlayState EditHelpFilter(KeyEvent key)
    {
        if (PromptTextEditor.Apply(HelpFilter, HelpFilterCaret, key) is not { } edit)
        {
            return this;
        }

        return edit.Text == HelpFilter
            ? this with { HelpFilterCaret = edit.Caret }
            : this with { HelpFilter = edit.Text, HelpFilterCaret = edit.Caret, HelpScroll = 0 };
    }

    /// <summary>Stops typing but keeps the filter, so the rows can be scrolled.</summary>
    public OverlayState AcceptHelpFilter() => this with { IsEditingHelpFilter = false };

    /// <summary>Drops the filter and shows the page that was open before it (from its first line).</summary>
    public OverlayState ClearHelpFilter() => this with { HelpFilter = string.Empty, IsEditingHelpFilter = false, HelpScroll = 0 };

    public OverlayState WithPalette(PaletteState palette) => this with { Palette = palette };

    public OverlayState WithWhichKey(bool visible) => this with { WhichKeyVisible = visible };
}
