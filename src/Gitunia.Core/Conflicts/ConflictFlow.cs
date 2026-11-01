using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Text;

namespace Gitunia.Core.Conflicts;

/// <summary>The steps the conflict reducers share: results with effects, the editor shown at a scroll position, and
/// the key mode that follows it.</summary>
internal static class ConflictFlow
{
    public static ConflictUpdate Done(ConflictSession session, params AppEffect[] effects) =>
        new(session, [.. effects]);

    public static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Info) =>
        new(text, severity);

    public static PostStatusEffect Warning(string text) => Post(text, StatusSeverity.Warning);

    public static ConflictUpdate Warn(ConflictSession session, string text) => Done(session, Warning(text));

    /// <summary>The file name as the status line shows it.</summary>
    public static string Name(string path) => PrintableText.Of(path);

    /// <summary>Both updates one after the other: the second one's session, the effects of both.</summary>
    public static ConflictUpdate Then(ConflictUpdate first, ConflictUpdate second) =>
        new(second.Session, [.. first.Effects, .. second.Effects]);

    public static ConflictUpdate Show(
        ConflictSession session,
        ConflictEditorState editor,
        params AppEffect[] effects) => Shown(session, editor, false, effects);

    /// <summary>Shows the editor with the cursor a third down the panes (after a jump to a conflict).</summary>
    public static ConflictUpdate ShowCentered(
        ConflictSession session,
        ConflictEditorState editor,
        params AppEffect[] effects) => Shown(session, editor, true, effects);

    /// <summary>Closes the editor: a later result for it is stale and the keymap goes back to Normal.</summary>
    public static ConflictUpdate Close(ConflictSession session, params AppEffect[] effects)
    {
        var closed = session with { Editor = null, Generation = session.Generation + 1, Mode = KeyMode.Normal };
        if (session.Mode == KeyMode.Normal)
        {
            return Done(closed, effects);
        }

        return Done(closed, [new SetKeyModeEffect(KeyMode.Normal), .. effects]);
    }

    /// <summary>Rows of the pane the cursor is in, for the half page keys.</summary>
    public static int PaneRows(ConflictSession session, ConflictEditorState editor)
    {
        const int defaultRows = 10;
        if (session.Viewports is not { } viewports)
        {
            return defaultRows;
        }

        var viewport = viewports.For(editor);

        return editor.IsVersionPane ? viewport.VersionRows : viewport.ResultRows;
    }

    private static ConflictUpdate Shown(
        ConflictSession session,
        ConflictEditorState editor,
        bool isCentered,
        AppEffect[] effects)
    {
        var followed = Followed(session, editor, isCentered);
        var mode = DesiredMode(followed);
        var shown = session with { Editor = followed, Mode = mode };
        if (mode == session.Mode)
        {
            return Done(shown, effects);
        }

        return Done(shown, [.. effects, new SetKeyModeEffect(mode)]);
    }

    // Scrolls the panes so the cursor shows at the current screen size.
    private static ConflictEditorState Followed(ConflictSession session, ConflictEditorState editor, bool isCentered)
    {
        if (session.Viewports is not { } viewports)
        {
            return editor;
        }

        var viewport = viewports.For(editor);

        return editor.Follow(viewport.VersionRows, viewport.ResultRows, isCentered);
    }

    private static KeyMode DesiredMode(ConflictEditorState editor)
    {
        if (editor.IsEditing)
        {
            return KeyMode.Insert;
        }

        return editor.IsVisual ? KeyMode.Visual : KeyMode.Normal;
    }
}
