namespace Gitunia.Tui.App.ExternalTools;

/// <summary>The tool's own output (git's reason for failing) is on the screen; keep it there until Enter is pressed.</summary>
public sealed class ConsolePause : IUserPause
{
    /// <inheritdoc />
    public void Wait()
    {
        Console.Out.WriteLine();
        Console.Out.Write("Press Enter to return to gitunia...");
        Console.Out.Flush();
        Console.In.ReadLine();
    }
}
