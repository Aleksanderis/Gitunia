using System.ComponentModel;
using System.Diagnostics;

namespace Gitunia.Git.Execution;

/// <summary>Stops a process tree without ever letting a kill failure replace the result the caller is about to report.</summary>
public static class ProcessKiller
{
    /// <summary>Kills the process and its children; a process that already ended or a child that cannot be stopped is ignored.</summary>
    public static void KillQuietly(Process process) => KillQuietly(() =>
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    });

    /// <summary>Runs <paramref name="kill"/>, swallowing the errors a kill can raise (exited, access denied, part of the tree left).</summary>
    public static void KillQuietly(Action kill)
    {
        try
        {
            kill();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or AggregateException)
        {
            // Already exited or exiting, or part of the tree could not be stopped: the original outcome stands.
        }
    }
}
