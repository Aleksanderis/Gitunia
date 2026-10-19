using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Git.Rebase;

/// <summary>The range or the todo cannot be rebased interactively; nothing was changed.</summary>
public sealed class RebaseRangeException : GitException
{
    public RebaseRangeException(string message)
        : base(message)
    {
    }
}
