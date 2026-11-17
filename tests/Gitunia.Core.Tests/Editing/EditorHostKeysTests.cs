using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class EditorHostKeysTests
{
    [Fact]
    public void GivenASelectionAndAHostWithAClipboard_WhenCopying_ThenTheSelectedTextIsTheEffectAndTheEditorIsUnchanged()
    {
        var editor = Selected("hello");

        var effect = EditorHostKeys.CopySelection(editor, EditorHosts.ConflictResult);

        Assert.Equal("ell", effect?.Text);
        Assert.Equal(EditorHostKeys.SelectionDescription, effect?.Description);
        Assert.Equal("hello", editor.Text);
    }

    [Fact]
    public void GivenNothingSelected_WhenCopying_ThenThereIsNoEffect()
    {
        var editor = new MessageEditor();
        editor.SetText("hello");

        Assert.Null(EditorHostKeys.CopySelection(editor, EditorHosts.ConflictResult));
    }

    [Fact]
    public void GivenAHostWithoutAClipboard_WhenCopying_ThenThereIsNoEffect()
    {
        var editor = Selected("hello");

        Assert.Null(EditorHostKeys.CopySelection(editor, EditorHosts.SettingsInput));
    }

    [Fact]
    public void GivenTheConflictResultHost_ThenItCopiesAndKeepsItsOwnUndo()
    {
        Assert.True(EditorHosts.ConflictResult.HasClipboard);
        Assert.False(EditorHosts.ConflictResult.HasUndo);
        Assert.Equal(DraftPolicy.Keep, EditorHosts.ConflictResult.Escape);
    }

    private static MessageEditor Selected(string text)
    {
        var editor = new MessageEditor();
        editor.SetText(text);
        editor.SetCursor(0, 1);
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));

        return editor;
    }
}
