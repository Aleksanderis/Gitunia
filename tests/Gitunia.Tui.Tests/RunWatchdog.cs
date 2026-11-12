using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Gitunia.Tui.Tests;

/// <summary>
/// Run-level backstop (#384): a project's run still alive after <see cref="Limit"/> (10 minutes: the slowest project takes about 4 minutes on the macOS CI leg, and a 5-minute limit killed a run that was still passing tests, #1384) is hung
/// (a blocked wait, a stuck child process, or an orphan whose worktree was deleted). It aborts the process with a
/// message instead of leaving an orphaned testhost that holds its caller open for hours.
/// </summary>
internal static class RunWatchdog
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    [ModuleInitializer]
    internal static void Start()
    {
        // Linked into several test projects: name the one that is running, not the project that owns the file.
        var name = typeof(RunWatchdog).Assembly.GetName().Name;
        // #1290/#1369: on Windows the host once stayed alive for the whole limit after the last test had passed. This line
        // tells whether the runtime reached process exit (a handler or finalizer is stuck) or never did (a foreground thread).
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Console.Error.WriteLine($"{name}: process exit reached at {DateTime.UtcNow:O}");
        var thread = new Thread(() =>
        {
            Thread.Sleep(Limit);
            Console.Error.WriteLine($"{name}: run exceeded {Limit.TotalMinutes} minutes, aborting (see issue #384).");
            Console.Error.WriteLine($"Tests still running (the runner names only the last one it saw start):{Environment.NewLine}{RunningTests.Describe()}");
            Console.Error.WriteLine($"Process threads (id, state, wait reason):{Environment.NewLine}{DescribeThreads()}");
            Console.Error.Flush();
            Environment.FailFast($"{name} run watchdog expired");
        })
        {
            IsBackground = true,
            Name = "RunWatchdog",
        };
        thread.Start();
    }

    private static string DescribeThreads()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var lines = new List<string>();
            foreach (ProcessThread thread in process.Threads)
            {
                var reason = thread.ThreadState == System.Diagnostics.ThreadState.Wait ? thread.WaitReason.ToString() : "-";
                lines.Add($"  {thread.Id} {thread.ThreadState} {reason}");
            }

            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception exception)
        {
            return $"  unavailable: {exception.GetType().Name}";
        }
    }
}
