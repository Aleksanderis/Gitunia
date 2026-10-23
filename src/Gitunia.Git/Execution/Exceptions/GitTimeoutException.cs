namespace Gitunia.Git.Execution.Exceptions;

/// <summary>Git did not finish (or produce output, for streams) within the timeout; the process was killed.</summary>
public sealed class GitTimeoutException : GitException
{
    public GitTimeoutException(IReadOnlyList<string> arguments, TimeSpan timeout)
        : base($"git {string.Join(' ', arguments)} timed out after {timeout.TotalSeconds:0.#} s")
    {
        Arguments = arguments;
        Timeout = timeout;
    }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global : public diagnostic data of the exception
    public IReadOnlyList<string> Arguments { get; }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global : public diagnostic data of the exception
    public TimeSpan Timeout { get; }
}
