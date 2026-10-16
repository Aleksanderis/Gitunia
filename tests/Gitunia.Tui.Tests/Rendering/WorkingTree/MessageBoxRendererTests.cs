using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

public class MessageBoxRendererTests
{
    private const int Padding = 1;

    private static string Text(StyledLine line) => string.Concat(line.Segments.Select(segment => segment.Text));

    [Fact]
    public void GivenADraftWithOptions_WhenRenderingTheOptionRow_ThenCheckboxesShowTheirStateAndTheCommitButtonIsAtTheRight()
    {
        var draft = new CommitDraft { SignOff = true };
        draft.SetAmend(true);

        var line = MessageBoxRenderer.OptionsLine(draft, 60, Padding, GraphGlyphSet.Unicode);

        Assert.Equal(60, TextWidth.Of(Text(line)));
        Assert.Equal($" [x] SignOff  [ ] No-Verify  [x] Amend{new string(' ', 14)}Commit  ", Text(line));
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.Button && segment.Text == " Commit ");
    }

    [Fact]
    public void GivenAnAsciiTerminal_WhenRenderingTheOptionRow_ThenTheButtonUsesBrackets()
    {
        var line = MessageBoxRenderer.OptionsLine(new CommitDraft(), 60, Padding, GraphGlyphSet.Ascii);

        Assert.Contains("[Commit]", Text(line), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANarrowBox_WhenRenderingTheOptionRow_ThenTheCommitButtonGivesWayToTheCheckboxes()
    {
        var line = MessageBoxRenderer.OptionsLine(new CommitDraft(), 44, Padding, GraphGlyphSet.Unicode);

        Assert.Equal(44, TextWidth.Of(Text(line)));
        Assert.DoesNotContain("Commit", Text(line), StringComparison.Ordinal);
        Assert.Contains("[ ] Amend", Text(line), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenACommitInProgress_WhenRenderingTheOptionRow_ThenItSaysSoInPlaceOfTheButton()
    {
        var line = MessageBoxRenderer.OptionsLine(new CommitDraft { IsCommitting = true }, 60, Padding, GraphGlyphSet.Unicode);

        Assert.Contains("committing...", Text(line), StringComparison.Ordinal);
        Assert.DoesNotContain(line.Segments, segment => segment.Role == SegmentRole.Button);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(100)]
    public void GivenARowWidth_WhenLayingOutTheButtons_ThenEachClickLandsOnItsOwnLabel(int width)
    {
        var textWidth = PanePadding.TextWidth(width, Padding);
        var buttons = MessageBoxRenderer.Buttons(textWidth, Padding);
        var line = Text(MessageBoxRenderer.OptionsLine(new CommitDraft(), width, Padding, GraphGlyphSet.Unicode));

        Assert.Equal(
            [
                DefaultMouseMap.MessageSignOffButton,
                DefaultMouseMap.MessageNoVerifyButton,
                DefaultMouseMap.MessageAmendButton,
                DefaultMouseMap.MessageCommitButton,
            ],
            buttons.Select(button => button.View));
        foreach (var button in buttons)
        {
            Assert.Equal(button.Label, line.Substring(button.Start, button.Width));
            Assert.Equal(button.View, TitleButton.ViewAt(buttons, button.Start));
            Assert.Equal(button.View, TitleButton.ViewAt(buttons, button.Start + button.Width - 1));
        }

        Assert.Null(TitleButton.ViewAt(buttons, 0));
    }

    [Fact]
    public void GivenNoDraft_WhenRenderingTheEditorRows_ThenTheHintShowsOnTheFirstRow()
    {
        var rows = MessageBoxRenderer.EditorLines(null, new PaneRect(0, 0, 80, 3), Padding, false, GraphGlyphSet.Unicode);

        Assert.Equal(3, rows.Count);
        Assert.Contains("Press i or Enter", Text(rows[0]), StringComparison.Ordinal);
        Assert.All(rows, row => Assert.Equal(80, TextWidth.Of(Text(row))));
    }

    [Fact]
    public void GivenATypedMessage_WhenRenderingTheEditorRows_ThenTheLinesAndTheCursorShow()
    {
        var draft = new CommitDraft();
        draft.Editor.SetText("Subject\n\nBody");

        var rows = MessageBoxRenderer.EditorLines(draft, new PaneRect(0, 0, 30, 4), Padding, true, GraphGlyphSet.Unicode);

        Assert.Equal(" Subject", Text(rows[0]).TrimEnd());
        Assert.Equal(" Body", Text(rows[2]).TrimEnd());
        Assert.Contains(rows[2].Segments, segment => segment.Role == SegmentRole.Cursor);
        Assert.DoesNotContain(rows[0].Segments, segment => segment.Role == SegmentRole.Cursor);
    }

    [Fact]
    public void GivenALongMessage_WhenTheCursorMovesUpInsideTheWindow_ThenTheTextDoesNotShift()
    {
        var draft = new CommitDraft();
        draft.Editor.SetText(string.Join('\n', Enumerable.Range(1, 10).Select(number => $"line {number}")));
        var rect = new PaneRect(0, 0, 30, 4);
        MessageBoxRenderer.EditorLines(draft, rect, Padding, true, GraphGlyphSet.Unicode);
        Assert.Equal(6, draft.Top);

        draft.Editor.Apply(new KeyEvent(KeyNames.Up, KeyModifiers.None));
        var rows = MessageBoxRenderer.EditorLines(draft, rect, Padding, true, GraphGlyphSet.Unicode);

        Assert.Equal(6, draft.Top);
        Assert.Equal(" line 7", Text(rows[0]).TrimEnd());
    }

    [Fact]
    public void GivenTheCursorAboveTheWindow_WhenScrolling_ThenTheWindowFollowsIt()
    {
        Assert.Equal(2, MessageEditorRenderer.ScrollTop(2, 6, 4));
        Assert.Equal(7, MessageEditorRenderer.ScrollTop(10, 6, 4));
        Assert.Equal(6, MessageEditorRenderer.ScrollTop(8, 6, 4));
        Assert.Equal(0, MessageEditorRenderer.ScrollTop(3, 0, 0));
    }

    [Fact]
    public void GivenACommitInProgress_WhenRenderingTheEditorRows_ThenThereIsNoCursor()
    {
        var draft = new CommitDraft { IsCommitting = true };
        draft.Editor.SetText("Subject");

        var rows = MessageBoxRenderer.EditorLines(draft, new PaneRect(0, 0, 30, 3), Padding, true, GraphGlyphSet.Unicode);

        Assert.DoesNotContain(rows.SelectMany(row => row.Segments), segment => segment.Role == SegmentRole.Cursor);
    }
}
