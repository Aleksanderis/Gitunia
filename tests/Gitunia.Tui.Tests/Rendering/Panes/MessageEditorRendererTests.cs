using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Panes;

[Trait("Os", "Independent")]
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
    public void GivenAScrolledCursorLine_WhenAskingTheOffset_ThenItMatchesWhatIsDrawn()
    {
        var editor = new MessageEditor();
        editor.SetText("abcdefghij");

        var offset = MessageEditorRenderer.ScrollOffset(editor, 4);
        var drawn = MessageEditorRenderer.Render(editor, 4, 1, true)[0].Text;

        Assert.Equal(7, offset);
        Assert.Equal("hij ", drawn);
        editor.PlaceCursor(0, offset + 1);
        Assert.Equal(8, editor.CursorColumn);
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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(-2, -2)]
    public void GivenTinyBox_WhenRendering_ThenItNeverThrows(int width, int height)
    {
        var editor = new MessageEditor();
        editor.SetText("中文\nx");

        var lines = MessageEditorRenderer.Render(editor, width, height, true);

        Assert.Equal(Math.Max(0, height), lines.Count);
        Assert.All(lines, line => Assert.True(TextWidth.Of(line.Text) <= Math.Max(1, width)));
    }

    [Fact]
    public void GivenMoreLinesThanHeight_WhenRendering_ThenTheCursorRowStaysVisible()
    {
        var editor = new MessageEditor();
        editor.SetText("1\n2\n3\n4");

        var lines = MessageEditorRenderer.Render(editor, 3, 2, true);

        Assert.Equal(["3  ", "4  "], lines.Select(line => line.Text));
    }

    [Fact]
    public void GivenASelection_WhenRendering_ThenOnlyTheSelectedCellsAreInTheRange()
    {
        var editor = new MessageEditor();
        editor.SetText("abcdef");
        editor.SetCursor(0, 1);
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Right, KeyModifiers.Shift));

        var line = MessageEditorRenderer.Render(editor, 8, 1, true)[0];

        var selected = line.Segments.Where(segment => segment.Selection == SegmentSelection.Range);
        Assert.Equal("bc", string.Concat(selected.Select(segment => segment.Text)));
        Assert.Equal("abcdef  ", line.Text);
    }

    [Fact]
    public void GivenASelectionAcrossLines_WhenRendering_ThenTheLineBreakShowsAsOneSelectedCellAndOtherLinesAreUntouched()
    {
        var editor = new MessageEditor();
        editor.SetText("ab\ncd\nef");
        editor.SetCursor(0, 1);
        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));

        var lines = MessageEditorRenderer.Render(editor, 5, 3, true, scrollTop: 0);

        Assert.Equal("b ", string.Concat(lines[0].Segments.Where(segment => segment.Selection == SegmentSelection.Range).Select(segment => segment.Text)));
        Assert.Equal("c", string.Concat(lines[1].Segments.Where(segment => segment.Selection == SegmentSelection.Range).Select(segment => segment.Text)).Trim());
        Assert.DoesNotContain(lines[2].Segments, segment => segment.Selection == SegmentSelection.Range);
    }

    [Fact]
    public void GivenAnEmptyMiddleLineInASelection_WhenRendering_ThenItShowsASelectedCell()
    {
        var editor = new MessageEditor();
        editor.SetText("a\n\nb");
        editor.SetCursor(0, 0);
        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));
        editor.Apply(new KeyEvent(KeyNames.Down, KeyModifiers.Shift));

        var lines = MessageEditorRenderer.Render(editor, 4, 3, true, scrollTop: 0);

        Assert.Contains(lines[1].Segments, segment => segment.Selection == SegmentSelection.Range && segment.Text == " ");
    }
}
