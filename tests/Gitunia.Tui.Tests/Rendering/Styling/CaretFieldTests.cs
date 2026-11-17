using Gitunia.Core.Editing;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

public class CaretFieldTests
{
    private const string Ellipsis = "…";

    [Fact]
    public void GivenTextWiderThanTheRow_WhenAppendingTheField_ThenTheLineFitsAndTheCursorCellStaysInView()
    {
        var builder = new LineBuilder(10, Ellipsis);

        new CaretField("/", new string('a', 30) + "XYZ", 15).AppendTo(builder, SegmentRole.Plain);
        var line = builder.Build();

        Assert.Equal(10, TextWidth.Of(line.Text));
        Assert.Equal("a", CursorCell.Of(line));
        Assert.DoesNotContain('X', line.Text);
    }

    [Fact]
    public void GivenWideCharacters_WhenAppendingTheField_ThenNoneIsSplitAndTheCursorCoversAWholeCharacter()
    {
        var builder = new LineBuilder(7, Ellipsis);

        new CaretField(string.Empty, "中文字漢字", 2).AppendTo(builder, SegmentRole.Plain);
        var line = builder.Build();

        Assert.Equal(7, TextWidth.Of(line.Text));
        Assert.Equal("字", CursorCell.Of(line));
    }

    [Fact]
    public void GivenTheCaretAtTheEnd_WhenAppendingTheField_ThenTheCursorIsABlankCellAfterTheText()
    {
        var builder = new LineBuilder(12, Ellipsis);

        new CaretField("> ", "abc", PromptCaret.End, " 1/2").AppendTo(builder, SegmentRole.Status);
        var line = builder.Build();

        Assert.StartsWith("> abc  1/2", line.Text, StringComparison.Ordinal);
        Assert.Equal(" ", CursorCell.Of(line));
    }
}
