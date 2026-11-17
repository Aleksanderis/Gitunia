using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;

namespace Gitunia.Core.Editing;

/// <summary>
/// The keys a host adds to <see cref="MessageEditor.Apply"/>, shared by every host that its <see cref="EditorHostPolicy"/>
/// allows them in: copy (<c>Alt-w</c>) and cut (<c>Ctrl-x</c>) hand the selection to the clipboard, undo (<c>Ctrl-z</c>)
/// and redo (<c>Ctrl-y</c>) post a notice when there is nothing to go back to. Everything else is the editor's own key.
/// </summary>
public static class EditorHostKeys
{
    /// <summary>The notice when undo has nothing to take back.</summary>
    public const string NothingToUndoNotice = "Nothing to undo";

    /// <summary>The notice when redo has nothing to bring back.</summary>
    public const string NothingToRedoNotice = "Nothing to redo";

    /// <summary>What the clipboard notice calls the copied text.</summary>
    public const string SelectionDescription = "selected text";

    private static readonly KeyEvent CopyKey = new("w", KeyModifiers.Alt);
    private static readonly KeyEvent CutKey = new("x", KeyModifiers.Ctrl);
    private static readonly KeyEvent UndoKey = new("z", KeyModifiers.Ctrl);
    private static readonly KeyEvent RedoKey = new("y", KeyModifiers.Ctrl);

    /// <summary>Applies <paramref name="rawKey"/> to <paramref name="editor"/> and returns the effects it asks for.</summary>
    /// <param name="editor">The editor, changed in place.</param>
    /// <param name="rawKey">The key.</param>
    /// <param name="policy">What the host offers.</param>
    public static AppEffect[] Apply(MessageEditor editor, KeyEvent rawKey, EditorHostPolicy policy)
    {
        var key = rawKey.Normalize();
        var isCut = key == CutKey;
        if (policy.HasClipboard && (isCut || key == CopyKey))
        {
            return Clipboard(editor, isCut);
        }

        if (policy.HasUndo && key == UndoKey)
        {
            return editor.Undo() ? [] : [new PostStatusEffect(NothingToUndoNotice, StatusSeverity.Info)];
        }

        if (policy.HasUndo && key == RedoKey)
        {
            return editor.Redo() ? [] : [new PostStatusEffect(NothingToRedoNotice, StatusSeverity.Info)];
        }

        editor.Apply(key);

        return [];
    }

    private static AppEffect[] Clipboard(MessageEditor editor, bool isCut)
    {
        if (!editor.HasSelection)
        {
            return [];
        }

        var copy = new CopyToClipboardEffect(editor.SelectedText, SelectionDescription);
        if (isCut)
        {
            editor.DeleteSelection();
        }

        return [copy];
    }
}
