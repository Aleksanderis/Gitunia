namespace Gitunia.Git.Execution.Exceptions;

/// <summary>Git exited with a code that the call did not accept.</summary>
public sealed class GitCommandFailedException : GitException
{
    public GitCommandFailedException(IReadOnlyList<string> arguments, int exitCode, string standardError, string standardOutput = "")
        : base($"git {string.Join(' ', arguments)} exited with code {exitCode}: {standardError.Trim()}")
    {
        Arguments = arguments;
        ExitCode = exitCode;
        StandardError = standardError;
        StandardOutput = standardOutput;
    }

    public IReadOnlyList<string> Arguments { get; }

    public int ExitCode { get; }

    public string StandardError { get; }

    /// <summary>What git printed on stdout (empty when the call did not capture it), e.g. "nothing to commit".</summary>
    public string StandardOutput { get; }
}
