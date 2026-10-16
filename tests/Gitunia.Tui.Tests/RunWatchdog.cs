using System.Runtime.CompilerServices;

namespace Gitunia.Tui.Tests;

/// <summary>
/// Run-level backstop (#384): the whole suite takes seconds, so a run still alive after <see cref="Limit"/> is hung
/// (a blocked wait, a stuck child process, or an orphan whose worktree was deleted). It aborts the process with a
/// message instead of leaving an orphaned testhost that holds its caller open for hours.
/// </summary>
internal static class RunWatchdog
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    [ModuleInitializer]
    internal static void Start()
    {
        var thread = new Thread(() =>
        {
            Thread.Sleep(Limit);
            Console.Error.WriteLine($"Gitunia.Tui.Tests: run exceeded {Limit.TotalMinutes} minutes, aborting (see issue #384).");
            Environment.FailFast("Gitunia.Tui.Tests run watchdog expired");
        })
        {
            IsBackground = true,
            Name = "RunWatchdog",
        };
        thread.Start();
    }
}
