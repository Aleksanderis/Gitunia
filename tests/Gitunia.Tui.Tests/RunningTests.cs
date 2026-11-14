using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Xunit.v3;

// ReSharper disable once RedundantNameQualifier : An assembly attribute precedes the namespace declaration, so the qualifier is required (removing it fails to compile)
[assembly: Gitunia.Tui.Tests.TrackRunningTests]

namespace Gitunia.Tui.Tests;

/// <summary>
/// Records which tests are in flight, so the run watchdog can name the stuck ones (#1254): the test the runner
/// reports at a crash is only the last one it saw start, and with parallel classes several are running at once.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class TrackRunningTestsAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest, IXunitTest test) => RunningTests.Started(test.UniqueID, test.TestDisplayName);

    public override void After(MethodInfo methodUnderTest, IXunitTest test) => RunningTests.Finished(test.UniqueID);
}

internal static class RunningTests
{
    private static readonly ConcurrentDictionary<string, RunningTest> Active = new();

    internal static void Started(string id, string name) => Active[id] = new RunningTest(name, Stopwatch.GetTimestamp());

    internal static void Finished(string id) => Active.TryRemove(id, out _);

    /// <summary>The in-flight tests, longest running first, one per line with the seconds each has run.</summary>
    internal static string Describe()
    {
        var now = Stopwatch.GetTimestamp();
        var lines = Active
            .OrderBy(pair => pair.Value.Started)
            .Select(pair => $"  {Stopwatch.GetElapsedTime(pair.Value.Started, now).TotalSeconds:F0} s  {pair.Value.Name}")
            .ToArray();
        return lines.Length == 0 ? "  (no test in flight: the host is stuck outside a test body)" : string.Join(Environment.NewLine, lines);
    }
}

internal sealed record RunningTest(string Name, long Started);
