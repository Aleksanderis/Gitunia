using Gitunia.Core.Editing;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Panes;

public class MessageEditorRendererTests
{
    [Fact]
    public void GivenText_WhenRendering_ThenEveryRowFillsTheWidthAndCursorIsMarked()
    {
        var editor = new MessageEditor();
        editor.SetText("fix\nbody");

        var lines = MessageEditorRenderer.Render(editor, 10, 3, true);

        Assert.Equal(["fix       ", "body      ", "          "], lines.Select(line => line.Text));
        var cursor = Assert.Single(lines[1].Segments, segment => segment.Role == SegmentRole.Cursor);
        Assert.Equal(" ", cursor.Text);
    }

    [Fact]
    public void GivenWideCharacters_WhenRendering_ThenTheyAreNeverSplitAtTheEdge()
    {
        var editor = new MessageEditor();
        editor.SetText("中文中");

        var lines = MessageEditorRenderer.Render(editor, 5, 1, false);

        Assert.Equal(5, TextWidth.Of(lines[0].Text));
        Assert.Equal("中文 ", lines[0].Text);
    }

    [Fact]
    public void GivenCursorBeyondTheWidth_WhenRendering_ThenTheLineScrollsToShowIt()
    {
        var editor = new MessageEditor();
        editor.SetText("abcdefghij");

        var lines = MessageEditorRenderer.Render(editor, 4, 1, true);

        Assert.Equal("hij ", lines[0].Text);
        Assert.Equal(SegmentRole.Cursor, lines[0].Segments[^1].Role);
    }

    [Fact]
    public void GivenSubjectPastTheSoftLimit_WhenRendering_ThenOnlyTheOverflowIsHinted()
    {
        var editor = new MessageEditor();
        editor.SetText($"{new string('a', MessageEditor.SubjectSoftLimit)}bc");

        var lines = MessageEditorRenderer.Render(editor, 80, 1, false);

        var hint = Assert.Single(lines[0].Segments, segment => segment.Role == SegmentRole.Error);
        Assert.Equal("bc", hint.Text);
    }

    [Fact]
    public void GivenMoreLinesThanHeight_WhenRendering_ThenTheCursorRowStaysVisible()
    {
        var editor = new MessageEditor();
        editor.SetText("1\n2\n3\n4");

        var lines = MessageEditorRenderer.Render(editor, 3, 2, true);

        Assert.Equal(["3  ", "4  "], lines.Select(line => line.Text));
    }
}
