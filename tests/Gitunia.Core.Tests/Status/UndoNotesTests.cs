using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Status;

// The undo lines name keys as plain text; these tests fail when the default keymap moves one of them.
[Trait("Os", "Independent")]
public sealed class UndoNotesTests
{
    [Theory]
    [InlineData("status.history", UndoNotes.MessagesKey)]
    [InlineData("reflog.open", UndoNotes.ReflogKey)]
    [InlineData("worktree.add", "<space>wa")]
    [InlineData("conflict-editor.recreate", "Ctrl-x")]
    public void GivenTheDefaultKeymap_WhenLookingUpAnActionNamedInAnUndoLine_ThenItIsBoundToThatKey(
        string action,
        string key)
    {
        var bindings = DefaultKeymap.CreateBindings().Where(binding => binding.Action == new ActionId(action));

        Assert.Contains(bindings, binding => binding.Sequence.ToString() == KeySequenceParser.Parse(key).ToString());
    }
}
