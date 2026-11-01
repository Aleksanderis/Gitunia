using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Core.Shell;

namespace Gitunia.Tui.App;

/// <summary>
/// Unsent commit messages are saved with the session on exit and restored per tab, so a quit never loses one. Only when
/// the session could not be saved does a quit guard remain: the first quit warns, the next one leaves (#369).
/// </summary>
/// <param name="host">Takes the quit command.</param>
public sealed class QuitRequests(Dispatcher host) : IQuitRequests
{
    /// <inheritdoc />
    public void Request() => host.Dispatch(new QuitRequested(CommitRules.HasAnyUnsentMessage(host.Workspace), false));

    /// <inheritdoc />
    public void Force() => host.Dispatch(new QuitRequested(CommitRules.HasAnyUnsentMessage(host.Workspace), true));
}
