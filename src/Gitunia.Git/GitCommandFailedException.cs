namespace Gitunia.Git;

/// <summary>Git exited with a code that the call did not accept.</summary>
public sealed class GitCommandFailedException : GitException
{
    public GitCommandFailedException(IReadOnlyList<string> arguments, int exitCode, string standardError)
        : base($"git {string.Join(' ', arguments)} exited with code {exitCode}: {standardError.Trim()}")
    {
        Arguments = arguments;
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public IReadOnlyList<string> Arguments { get; }

    public int ExitCode { get; }

    public string StandardError { get; }
}
