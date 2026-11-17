using Xunit;

namespace Gitunia.Tui.Tests;

public sealed class StackReportCompactorTests
{
    [Fact]
    public void GivenAThreadWithMoreFramesThanTheCap_WhenCompacted_ThenItsHeaderAndInnermostFramesRemain()
    {
        var frames = Enumerable.Range(1, StackReportCompactor.FramesPerThread + 5).Select(index => $"  Frame{index}");
        var report = string.Join("\n", frames.Prepend("Thread (0x1):").Append("Thread (0x2):").Append("  Other"));

        var lines = StackReportCompactor.Compact(report).Split(Environment.NewLine);

        Assert.Equal("Thread (0x1):", lines[0]);
        Assert.Equal($"  Frame{StackReportCompactor.FramesPerThread}", lines[StackReportCompactor.FramesPerThread]);
        Assert.Equal(["Thread (0x2):", "  Other"], lines[^2..]);
        Assert.Equal(StackReportCompactor.FramesPerThread + 3, lines.Length);
    }

    [Fact]
    public void GivenFramesOfTheTestRunnerAndAsyncPlumbing_WhenCompacted_ThenTheyAreDropped()
    {
        const string report = "Thread (0x1):\n  System.Private.CoreLib.il!Wait()\n  xunit.v3.core!Xunit.Run()\n"
            + "  AsyncMethodBuilderCore.Start()\n  Gitunia!Shell.Dispose()";

        var lines = StackReportCompactor.Compact(report).Split(Environment.NewLine);

        Assert.Equal(["Thread (0x1):", "  Wait()", "  Gitunia!Shell.Dispose()"], lines);
    }
}
