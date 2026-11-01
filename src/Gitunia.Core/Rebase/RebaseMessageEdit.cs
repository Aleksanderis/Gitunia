using Gitunia.Core.Editing;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Rebase;

/// <summary>The open message editor of a reword: whose message it edits and the editor holding the text.</summary>
/// <param name="Hash">The full hash of the commit being reworded.</param>
/// <param name="Editor">The multi-line editor with the message so far; never changed once it is in a state.</param>
public sealed record RebaseMessageEdit(string Hash, MessageEditor Editor)
{
    /// <summary>The edit after a key: the key is applied to a copy, so earlier states keep their text.</summary>
    /// <param name="key">The key.</param>
    public RebaseMessageEdit Typed(KeyEvent key)
    {
        var copy = Editor.Clone();
        copy.Apply(key);

        return this with { Editor = copy };
    }
}
