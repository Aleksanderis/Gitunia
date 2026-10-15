namespace Gitunia.Platform.ExternalTools;

/// <summary>
/// Runs a program with the terminal's own standard input and output (an editor, a difftool, a mergetool), which the
/// git runner cannot do because it redirects them. The caller must have released the terminal first.
/// </summary>
public interface IExternalToolLauncher
{
    /// <summary>False on an OS where handing the terminal over is not supported.</summary>
    bool IsSupported { get; }

    /// <summary>Starts the program, waits for it and returns how it ended. Never throws for a program that fails to start.</summary>
    ExternalToolResult Run(ExternalToolRequest request);
}
