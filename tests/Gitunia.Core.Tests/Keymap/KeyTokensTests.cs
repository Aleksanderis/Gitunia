using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

// The undo lines and worktree refusals name keys through tokens, resolved against the running keymap (#1196).
[Trait("Os", "Independent")]
public sealed class KeyTokensTests
{
    [Theory]
    [InlineData("status.history", "<space>m")]
    [InlineData("reflog.open", "<space>l")]
    [InlineData("worktree.add", "<space>wa")]
    [InlineData("conflict-editor.recreate", "Ctrl-x")]
    public void GivenTheDefaultKeymap_WhenResolvingAToken_ThenTheDefaultKeyIsNamed(string action, string key)
    {
        var table = new KeyBindingTable(DefaultKeymap.CreateBindings());

        var text = KeyTokens.Resolve(KeyTokens.Of(action, "the thing"), table);

        Assert.Equal($"the thing ({key})", text);
    }

    [Fact]
    public void GivenARemappedAction_WhenResolvingTheUndoLine_ThenTheNewKeyIsNamed()
    {
        var remapped = new KeyBinding(
            KeyMode.Normal,
            KeyBindingTable.GlobalView,
            KeySequenceParser.Parse("<space>zz"),
            new ActionId("status.history"));
        var table = new KeyBindingTable([remapped]);

        var text = KeyTokens.Resolve(UndoNotes.FromHistory, table);

        Assert.Equal("Afterwards the message history (<space>zz) shows how to bring it back.", text);
    }

    [Fact]
    public void GivenAnUnboundAction_WhenResolving_ThenOnlyTheNameStays()
    {
        var table = new KeyBindingTable([]);

        var text = KeyTokens.Resolve(UndoNotes.FromReflog, table);

        Assert.Equal("Afterwards you can go back from the reflog; the message history has the exact command.", text);
        Assert.Equal("a new worktree", KeyTokens.Resolve(KeyTokens.Of("worktree.add", "a new worktree"), null));
    }

    [Fact]
    public void GivenTextWithoutTokens_WhenResolving_ThenItIsReturnedUnchanged()
    {
        Assert.Equal("{plain} text", KeyTokens.Resolve("{plain} text", null));
    }
}
