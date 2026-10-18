using Gitunia.Git.Execution.Exceptions;

namespace Gitunia.Git.Rebase;

/// <summary>The user's environment would override Gitunia's todo (<c>GIT_SEQUENCE_EDITOR</c> is set); nothing ran.</summary>
public sealed class RebaseEnvironmentException : GitException
{
    public RebaseEnvironmentException(string message)
        : base(message)
    {
    }
}
