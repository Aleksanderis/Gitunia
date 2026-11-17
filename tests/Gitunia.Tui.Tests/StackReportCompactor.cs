namespace Gitunia.Tui.Tests;

/// <summary>
/// Shortens a <c>dotnet-stack report</c>: the test platform cuts a crashed host's stderr at about 8 KB, and the first
/// Windows capture (#1369, run 43584745497) ended in the middle of the test thread's xunit frames, before any other
/// thread. The async plumbing frames say nothing, so they are dropped and each thread keeps its innermost frames.
/// </summary>
public static class StackReportCompactor
{
    /// <summary>The frames kept per thread.</summary>
    public const int FramesPerThread = 14;

    private const string ThreadHeader = "Thread";
    private const string CoreLibraryPrefix = "System.Private.CoreLib.il!";

    private static readonly string[] NoiseFrames =
    [
        "!Xunit.",
        "AsyncMethodBuilder",
        "ExecutionContext.RunInternal",
        "ExceptionAggregator",
    ];

    public static string Compact(string report)
    {
        var lines = new List<string>();
        var frames = 0;
        foreach (var line in report.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var text = line.TrimEnd('\r');
            if (text.StartsWith(ThreadHeader, StringComparison.Ordinal))
            {
                frames = 0;
                lines.Add(text);

                continue;
            }

            if (NoiseFrames.Any(text.Contains) || ++frames > FramesPerThread)
            {
                continue;
            }

            lines.Add(text.Replace(CoreLibraryPrefix, string.Empty, StringComparison.Ordinal));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
