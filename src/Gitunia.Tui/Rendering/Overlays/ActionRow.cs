using Gitunia.Core.Hints;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// The "key: description" rows of a screen that lists what its keys do (the reflog, the blame view, the file history, the
/// confirm dialog): the key on the left, the sentence on the right. The key is looked up in the keymap by the action
/// (<see cref="ChipKeys"/>), so a rebinding follows and no row spells a key by hand; the sentence is the
/// <see cref="KeyHint.Detail"/> of the screen's <see cref="HintSet"/>. The hints without a sentence are the footer's.
/// </summary>
public static class ActionRow
{
    private const string AnswerGap = "  ";

    /// <summary>The hints that have an action row of their own.</summary>
    /// <param name="hints">The declared hints of a screen.</param>
    public static List<KeyHint> RowsOf(IEnumerable<KeyHint> hints) =>
        [.. hints.Where(hint => !string.IsNullOrEmpty(hint.Detail))];

    /// <summary>The hints the footer draws: those that have no action row (and are not drawn as chips).</summary>
    /// <param name="hints">The declared hints of a screen.</param>
    public static List<KeyHint> FooterOf(IEnumerable<KeyHint> hints) => [.. hints.Where(hint => hint.Detail is null)];

    /// <summary>
    /// The key of the hint's action in the keymap: the shortest binding in <paramref name="keyView"/> (Normal mode unless
    /// <paramref name="mode"/> says otherwise), empty when the action has none, so a removed key is not advertised.
    /// </summary>
    /// <param name="action">The action the row stands for.</param>
    /// <param name="keyView">The keymap view the row sits in.</param>
    /// <param name="mode">The key mode the keys are looked up in.</param>
    /// <param name="keys">The lookup; null uses the default keymap.</param>
    public static string KeysOf(
        ActionId action,
        string keyView,
        KeyMode mode = KeyMode.Normal,
        ChipKeys? keys = null) =>
        (keys ?? ChipKeys.Default).ForAction(action, keyView, mode) ?? string.Empty;

    /// <summary>One row of a hint that has a sentence.</summary>
    /// <param name="hint">The hint; its first action names the key.</param>
    /// <param name="keyView">The keymap view the row sits in.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="glyphs">The characters to draw with.</param>
    /// <param name="keys">The lookup; null uses the default keymap.</param>
    public static StyledLine Line(
        KeyHint hint,
        string keyView,
        int width,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null) =>
        Line(KeysOf(hint.Actions[0], keyView, keys: keys), hint.Detail ?? hint.Description, width, glyphs);

    /// <summary>One row: <paramref name="keys"/> left, <paramref name="description"/> right.</summary>
    /// <param name="keys">The key text.</param>
    /// <param name="description">What the key does.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="glyphs">The characters to draw with.</param>
    /// <param name="isAvailable">False draws the row dimmed (an answer that is not possible).</param>
    public static StyledLine Line(
        string keys,
        string description,
        int width,
        GraphGlyphSet glyphs,
        bool isAvailable = true) =>
        OverlayBox.TwoColumns(
            keys,
            description,
            width,
            glyphs,
            isAvailable ? SegmentRole.KeyLabel : SegmentRole.Faint,
            SegmentRole.Faint);

    /// <summary>
    /// One answer row of a dialog: the key, two spaces, then what the answer does (a confirm dialog's yes and no, the
    /// parents of the mainline question); each is a row of its own so a click can answer it.
    /// </summary>
    /// <param name="keys">The key text.</param>
    /// <param name="label">What the answer does.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="glyphs">The characters to draw with.</param>
    /// <param name="isAvailable">False draws the row dimmed (an answer that is not possible).</param>
    public static StyledLine Answer(string keys, string label, int width, GraphGlyphSet glyphs, bool isAvailable = true)
    {
        var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis)
            .Append($" {keys}", isAvailable ? SegmentRole.KeyLabel : SegmentRole.Faint)
            .Append($"{AnswerGap}{label}", isAvailable ? SegmentRole.Plain : SegmentRole.Faint);

        return OverlayBox.Wrap(builder.Build(), glyphs);
    }
}
