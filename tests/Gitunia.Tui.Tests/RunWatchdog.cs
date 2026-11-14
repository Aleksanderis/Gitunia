using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Gitunia.Tui.Tests;

/// <summary>
/// Run-level backstop (#384): a project's run still alive after <see cref="Limit"/> (5 minutes; 10 on macOS, where Gitunia.App.Tests took over 5 minutes in run 43175668900 while still passing tests, #1384; the Linux CI step timeout is 10 minutes, so a longer limit there would never print its dump) is hung
/// (a blocked wait, a stuck child process, or an orphan whose worktree was deleted). It aborts the process with a
/// message instead of leaving an orphaned testhost that holds its caller open for hours.
/// </summary>
internal static class RunWatchdog
{
    private const string StackToolVariable = "GITUNIA_STACK_TOOL";
    private static readonly TimeSpan StackToolTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(OperatingSystem.IsMacOS() ? 10 : 5);

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
            Console.Error.WriteLine($"Capturing managed stacks (up to {StackToolTimeout.TotalSeconds:F0} s)...");
            Console.Error.Flush();
            Console.Error.WriteLine($"Managed stacks:{Environment.NewLine}{DescribeManagedStacks()}");
            Console.Error.Flush();
            Environment.FailFast($"{name} run watchdog expired");
        })
        {
            IsBackground = true,
            Name = "RunWatchdog",
        };
        thread.Start();
    }

    /// <summary>
    /// The managed stack of every thread, from the <c>dotnet-stack</c> tool when CI installed it (#1369: the thread list above
    /// cannot say what a hung Windows host waits for). The tool attaches to this very process over the diagnostics
    /// port, so it runs before <c>FailFast</c> and is bounded.
    /// </summary>
    private static string DescribeManagedStacks()
    {
        var tool = Environment.GetEnvironmentVariable(StackToolVariable);
        if (string.IsNullOrEmpty(tool))
        {
            return $"  (not captured: {StackToolVariable} is not set)";
        }

        try
        {
            var start = new ProcessStartInfo(tool)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("report");
            start.ArgumentList.Add("-p");
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.Environment["DOTNET_ROLL_FORWARD"] = "Major";
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(StackToolTimeout))
            {
                process.Kill(true);

                return "  (dotnet-stack did not finish in time)";
            }

            return $"{output.Result}{errors.Result}";
        }
        catch (Exception exception)
        {
            return $"  unavailable: {exception.GetType().Name}: {exception.Message}";
        }
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
