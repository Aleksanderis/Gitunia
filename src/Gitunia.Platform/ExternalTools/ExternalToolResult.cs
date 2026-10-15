namespace Gitunia.Platform.ExternalTools;

/// <summary>How an external tool ended.</summary>
/// <param name="ExitCode">The exit code, or -1 when the program could not be started.</param>
/// <param name="Error">Why it could not be started, null when it ran.</param>
public sealed record ExternalToolResult(int ExitCode, string? Error = null)
{
    public bool Succeeded => Error is null && ExitCode == 0;

    /// <summary>One line for the status bar.</summary>
    public string Describe(string toolName) =>
        Error is not null ? $"{toolName}: {Error}" : Succeeded ? $"{toolName} finished" : $"{toolName} exited with code {ExitCode}";
}
