using Gitunia.Core.Editing;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>
/// Draws the commit message box of the working-tree view: the editor rows and the option row below them with the SignOff,
/// No-Verify and Amend checkboxes and the Commit button (ux.md, workflow 2). The checkboxes and the button are the mouse path of
/// <c>Alt-s</c>, <c>Alt-n</c>, <c>Alt-a</c> and <c>Ctrl-g</c>; <see cref="Buttons"/> says where they are for clicks.
/// </summary>
public static class MessageBoxRenderer
{
    private const string Checked = "[x]";
    private const string Unchecked = "[ ]";
    private const string SignOffLabel = "SignOff";
    private const string NoVerifyLabel = "No-Verify";
    private const string AmendLabel = "Amend";
    private const string CommitLabel = " Commit ";
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
    public static IReadOnlyList<TitleButton> Buttons(int textWidth, int padding)
    {
        var buttons = new List<TitleButton>();
        var column = padding;
        foreach (var (view, name) in Options)
        {
            var label = $"{Unchecked} {name}";
            buttons.Add(new TitleButton(view, label, column));
            column += TextWidth.Of(label) + Gap.Length;
        }

        var commitStart = padding + textWidth - TextWidth.Of(CommitLabel);
        if (commitStart >= column)
        {
            buttons.Add(new TitleButton(DefaultMouseMap.MessageCommitButton, CommitLabel, commitStart));
        }

        return buttons;
    }

    /// <summary>The option row, exactly <paramref name="width"/> columns wide, padded like the other panes.</summary>
    /// <param name="draft">The tab's draft, null while there is none.</param>
    /// <param name="width">Columns of the row including the padding.</param>
    /// <param name="padding">Blank columns on each side.</param>
    /// <param name="glyphs">The character set.</param>
    public static StyledLine OptionsLine(CommitDraft? draft, int width, int padding, GraphGlyphSet glyphs)
    {
        var textWidth = PanePadding.TextWidth(width, padding);
        var builder = new LineBuilder(textWidth, glyphs.Ellipsis);
        var states = new[] { draft?.SignOff == true, draft?.NoVerify == true, draft?.Amend == true };
        for (var index = 0; index < Options.Length; index++)
        {
            builder.Append($"{(states[index] ? Checked : Unchecked)} {Options[index].Name}{Gap}", SegmentRole.Plain);
        }

        var commitStart = textWidth - TextWidth.Of(CommitLabel);
        if (draft?.IsCommitting == true)
        {
            var running = TextWidth.Fit(RunningText, Math.Max(0, builder.Remaining), glyphs.Ellipsis);
            builder.Append(new string(' ', Math.Max(0, builder.Remaining - TextWidth.Of(running))), SegmentRole.Plain);
            builder.Append(running, SegmentRole.Faint);
        }
        else if (commitStart >= TextWidth.Of(AllOptionsText))
        {
            builder.Append(new string(' ', builder.Remaining - TextWidth.Of(CommitLabel)), SegmentRole.Plain);
            builder.Append(ButtonLabel.For(CommitLabel, glyphs.IsAscii), SegmentRole.Button);
        }

        return PanePadding.Wrap(builder.Build(), padding);
    }

    /// <summary>The rows of the editor part of the box (without the option row).</summary>
    /// <param name="draft">The tab's draft, null while there is none.</param>
    /// <param name="rect">The editor rectangle.</param>
    /// <param name="padding">Blank columns on each side.</param>
    /// <param name="showCursor">The box is being edited (focused, Insert mode).</param>
    /// <param name="glyphs">The character set.</param>
    public static IReadOnlyList<StyledLine> EditorLines(
        CommitDraft? draft,
        PaneRect rect,
        int padding,
        bool showCursor,
        GraphGlyphSet glyphs)
    {
        var textWidth = PanePadding.TextWidth(rect.Width, padding);
        if (draft is null || (draft.Editor.IsBlank && !showCursor && draft.Editor.Lines.Count <= 1))
        {
            return HintLines(rect, textWidth, padding, glyphs);
        }

        var top = MessageEditorRenderer.ScrollTop(draft.Editor.CursorRow, draft.Top, rect.Height);
        draft.Top = top;
        var isCursorShown = showCursor && !draft.IsCommitting;
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
