namespace Gitunia.Git.Execution.Exceptions;

/// <summary>
/// The git executable exists but the operating system refused to start it (command line too long, access denied,
/// blocked by security software); the native message says why.
/// </summary>
public sealed class GitStartFailedException : GitException
{
    public GitStartFailedException(string executable, Exception innerException)
        : base($"Could not start '{executable}': {innerException.Message}", innerException)
    {
        Executable = executable;
    }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Public diagnostic data of the exception
    public string Executable { get; }
}
