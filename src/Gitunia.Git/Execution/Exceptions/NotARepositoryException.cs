namespace Gitunia.Git.Execution.Exceptions;

/// <summary>The path does not exist or is not inside a git repository.</summary>
public sealed class NotARepositoryException : GitException
{
    public NotARepositoryException(string path, string reason)
        : base($"'{path}' is not a git repository: {reason}")
    {
        Path = path;
    }

    public string Path { get; }
}
