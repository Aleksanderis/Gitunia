using System.Diagnostics;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>Kills a process from a cancellation callback, passed as callback state so no variable is captured.</summary>
public static class ProcessKiller
{
    public static void KillQuietly(object? state)
    {
        try
        {
            ((Process)state!).Kill();
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}
