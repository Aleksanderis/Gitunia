namespace Gitunia.Core.Shell;

/// <summary>The editor sessions of <see cref="ShellState"/> that exclude themselves when asking whether they are covered.</summary>
public enum ShellSession
{
    Reflog,
    Blame,
    RebaseEditor,
    Conflict,
}
