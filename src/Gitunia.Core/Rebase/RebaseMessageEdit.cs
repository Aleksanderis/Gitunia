using Gitunia.Core.Editing;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Rebase;

/// <summary>The open message editor of a reword: whose message it edits and the editor holding the text.</summary>
/// <param name="Hash">The full hash of the commit being reworded.</param>
/// <param name="Editor">The multi-line editor with the message so far; never changed once it is in a state.</param>
/// <param name="Seed">The message the editor opened with, to tell a typed draft from an untouched one.</param>
public sealed record RebaseMessageEdit(string Hash, MessageEditor Editor, string Seed = "")
{
    /// <summary>True when the text differs from what the editor opened with.</summary>
    public bool IsChanged => Editor.Text != Seed;

    /// <summary>
    /// The edit after a key as the reword host allows it (<see cref="EditorHosts.Reword"/>: undo, copy and cut): the
    /// key is applied to a copy, so earlier states keep their text.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="effects">What the key asks for (the clipboard, a notice).</param>
    public RebaseMessageEdit Typed(KeyEvent key, out AppEffect[] effects)
    {
        var copy = Editor.Clone();
        effects = EditorHostKeys.Apply(copy, key, EditorHosts.Reword);

        return this with { Editor = copy };
    }
}
