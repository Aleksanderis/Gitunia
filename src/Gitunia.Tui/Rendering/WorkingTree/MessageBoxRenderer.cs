using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>
/// Draws the commit message box of the Changes view: the editor rows and the option row below them with the SignOff,
/// No-Verify and Amend checkboxes and the Commit button (ux-workflows.md, workflow 2). The checkboxes and the button are the mouse path of
/// <c>Alt-s</c>, <c>Alt-n</c>, <c>Alt-a</c> and the commit key; <see cref="Buttons"/> says where they are for clicks.
/// The Commit button is a <see cref="KeyedChip"/> showing the key the keymap gives it in Insert mode (where the box is
/// typed in), and drops the key when the row is too narrow; the checkboxes are state, not chips, and keep their
/// brackets.
/// </summary>
public static class MessageBoxRenderer
{
    private const string Checked = "[x]";
    private const string Unchecked = "[ ]";
    private const string SignOffLabel = "SignOff";
    private const string NoVerifyLabel = "No-Verify";
    private const string AmendLabel = "Amend";
    private const string CommitLabel = "Commit";
    private const string Gap = "  ";
    private const string RunningText = "committing...";

    private sealed record OptionSpecification(string View, string Name);

    private static readonly OptionSpecification[] Options =
    [
        new(DefaultMouseMap.MessageSignOffButton, SignOffLabel),
        new(DefaultMouseMap.MessageNoVerifyButton, NoVerifyLabel),
        new(DefaultMouseMap.MessageAmendButton, AmendLabel),
    ];

    // The option row up to the Commit button: the Commit button needs the columns after it.
    private static readonly string AllOptionsText =
        $"{string.Join(Gap, Options.Select(option => $"{Unchecked} {option.Name}"))}{Gap}";

    /// <summary>Shown in the editor rows of an empty box that is not being edited.</summary>
    public const string HintText = "Press i or Enter to write the message, <space>c in the file lists";

    /// <summary>
    /// Where the checkboxes and the Commit button sit in an option row whose text has <paramref name="textWidth"/> columns
    /// (the row has <paramref name="padding"/> blank columns on each side).
    /// </summary>
    /// <param name="keys">Where the Commit key comes from; null uses the default keymap.</param>
    public static IReadOnlyList<TitleButton> Buttons(int textWidth, int padding, ChipKeys? keys = null)
    {
        var buttons = new List<TitleButton>();
        var column = padding;
        foreach (var (view, name) in Options)
        {
            var label = $"{Unchecked} {name}";
            buttons.Add(new TitleButton(view, label, column));
            column += TextWidth.Of(label) + Gap.Length;
        }

        if (CommitChip(textWidth, keys) is { } commit)
        {
            buttons.Add(commit with { Start = padding + commit.Start });
        }

        return buttons;
    }

    /// <summary>The option row, exactly <paramref name="width"/> columns wide, padded like the other panes.</summary>
    /// <param name="draft">The tab's draft, null while there is none.</param>
    /// <param name="width">Columns of the row including the padding.</param>
    /// <param name="padding">Blank columns on each side.</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="keys">Where the Commit key comes from; null uses the default keymap.</param>
    /// <param name="isCommitting">A commit runs: the row says so instead of offering the Commit button.</param>
    public static StyledLine OptionsLine(
        CommitDraft? draft,
        int width,
        int padding,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null,
        bool isCommitting = false)
    {
        var textWidth = PanePadding.TextWidth(width, padding);
        var builder = new LineBuilder(textWidth, glyphs.Ellipsis);
        var states = new[] { draft?.SignOff == true, draft?.NoVerify == true, draft?.Amend == true };
        for (var index = 0; index < Options.Length; index++)
        {
            builder.Append($"{(states[index] ? Checked : Unchecked)} {Options[index].Name}{Gap}", SegmentRole.Plain);
        }

        if (isCommitting)
        {
            var running = TextWidth.Fit(RunningText, Math.Max(0, builder.Remaining), glyphs.Ellipsis);
            builder.Append(new string(' ', Math.Max(0, builder.Remaining - TextWidth.Of(running))), SegmentRole.Plain);
            builder.Append(running, SegmentRole.Faint);
        }
        else if (CommitChip(textWidth, keys) is { } commit)
        {
            builder.Append(new string(' ', commit.Start - TextWidth.Of(AllOptionsText)), SegmentRole.Plain);
            foreach (var segment in KeyedChip.Segments(commit, glyphs))
            {
                builder.Append(segment.Text, segment.Role);
            }
        }

        return PanePadding.Wrap(builder.Build(), padding);
    }

    // The Commit chip flush right in a text of textWidth columns (Start relative to the text), keyed when the row has
    // room for the key, plain when only the label fits, null when not even that.
    private static TitleButton? CommitChip(int textWidth, ChipKeys? keys)
    {
        var key = (keys ?? ChipKeys.Default)
            .For(DefaultMouseMap.MessageCommitButton, DefaultKeymap.WorkingMessageView, KeyMode.Insert);
        var room = textWidth - TextWidth.Of(AllOptionsText);
        var view = DefaultMouseMap.MessageCommitButton;
        TitleButton[] candidates = [KeyedChip.Keyed(view, CommitLabel, key), KeyedChip.Plain(view, CommitLabel)];
        foreach (var chip in candidates)
        {
            if (chip.Width <= room)
            {
                return chip with { Start = textWidth - chip.Width };
            }
        }

        return null;
    }

    /// <summary>The rows of the editor part of the box (without the option row).</summary>
    /// <param name="draft">The tab's draft, null while there is none.</param>
    /// <param name="rect">The editor rectangle.</param>
    /// <param name="padding">Blank columns on each side.</param>
    /// <param name="showCursor">The box is being edited (focused, Insert mode).</param>
    /// <param name="glyphs">The character set.</param>
    /// <param name="isCommitting">A commit runs: the box is read-only and shows no cursor.</param>
    public static IReadOnlyList<StyledLine> EditorLines(
        CommitDraft? draft,
        PaneRect rect,
        int padding,
        bool showCursor,
        GraphGlyphSet glyphs,
        bool isCommitting = false)
    {
        var textWidth = PanePadding.TextWidth(rect.Width, padding);
        if (draft is null || (draft.Editor.IsBlank && !showCursor && draft.Editor.Lines.Count <= 1))
        {
            return HintLines(rect, textWidth, padding, glyphs);
        }

        var top = MessageEditorRenderer.ScrollTop(draft.Editor.CursorRow, draft.Top, rect.Height);
        draft.Top = top;
        var isCursorShown = showCursor && !isCommitting;
        var lines = MessageEditorRenderer.Render(draft.Editor, textWidth, rect.Height, isCursorShown, top);
        return [.. lines.Select(line => PanePadding.Wrap(line, padding))];
    }

    private static List<StyledLine> HintLines(PaneRect rect, int textWidth, int padding, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>(rect.Height);
        for (var row = 0; row < rect.Height; row++)
        {
            var builder = new LineBuilder(textWidth, glyphs.Ellipsis);
            if (row == 0)
            {
                builder.Append(HintText, SegmentRole.Faint);
            }

            lines.Add(PanePadding.Wrap(builder.Build(), padding));
        }

        return lines;
    }
}
