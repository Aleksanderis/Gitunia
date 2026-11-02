namespace Gitunia.Git.Execution.Exceptions;

/// <summary>The git executable could not be started.</summary>
public sealed class GitNotFoundException : GitException
{
    public GitNotFoundException(string executable, Exception innerException)
        : base($"Could not start '{executable}'. Is git installed and on PATH?", innerException)
    {
        Executable = executable;
    }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Public diagnostic data of the exception
    public string Executable { get; }
}
