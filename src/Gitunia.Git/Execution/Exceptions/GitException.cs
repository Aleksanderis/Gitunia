namespace Gitunia.Git.Execution.Exceptions;

/// <summary>Base type of every error raised by the git layer.</summary>
public class GitException : Exception
{
    public GitException(string message)
        : base(message)
    {
    }

    public GitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
