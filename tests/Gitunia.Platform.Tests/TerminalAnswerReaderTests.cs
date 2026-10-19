using Xunit;

namespace Gitunia.Platform.Tests;

public class TerminalAnswerReaderTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(5);

    [Fact]
    public void GivenColourAnswerEndedByBackslashAndDa1_WhenReading_ThenStopsAtDa1WithoutWaiting()
    {
        var reader = ReaderOf("]11;rgb:ffff/ffff/ffff\\[?64;1;2c", out var remaining);

        var answer = reader.Read(Generous);

        Assert.Equal("]11;rgb:ffff/ffff/ffff\\[?64;1;2", answer);
        Assert.Equal(0, remaining());
        Assert.True(TerminalBackgroundQuery.ParseIsLight(answer!));
    }

    [Fact]
    public void GivenColourAnswerEndedByBell_WhenReading_ThenHexLetterCDoesNotEndTheRead()
    {
        var reader = ReaderOf("]11;rgb:cccc/cccc/cccc\u0007[?1;2c", out var remaining);

        var answer = reader.Read(Generous);

        Assert.Equal("]11;rgb:cccc/cccc/cccc\u0007[?1;2", answer);
        Assert.Equal(0, remaining());
        Assert.True(TerminalBackgroundQuery.ParseIsLight(answer!));
    }

    [Fact]
    public void GivenOnlyDa1Reply_WhenReading_ThenNoColourAndNoWait()
    {
        var reader = ReaderOf("[?64;1;2c", out _);

        var answer = reader.Read(Generous);

        Assert.Null(TerminalBackgroundQuery.ParseIsLight(answer ?? string.Empty));
    }

    [Fact]
    public void GivenSilentTerminal_WhenReading_ThenNullAfterTheTimeout()
    {
        var reader = new TerminalAnswerReader(() => false, () => throw new InvalidOperationException());

        Assert.Null(reader.Read(TimeSpan.FromMilliseconds(20)));
    }

    [Fact]
    public void GivenEndlessGarbage_WhenReading_ThenTheReadIsBounded()
    {
        var reader = new TerminalAnswerReader(() => true, () => 'x');

        var answer = reader.Read(Generous);

        Assert.Equal(256, answer!.Length);
    }

    private static TerminalAnswerReader ReaderOf(string text, out Func<int> remaining)
    {
        var queue = new Queue<char>(text);
        remaining = () => queue.Count;
        return new TerminalAnswerReader(() => queue.Count > 0, queue.Dequeue);
    }
}
