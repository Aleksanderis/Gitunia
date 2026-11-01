using Gitunia.App;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Overlays;

namespace Gitunia.Tui.App.Remote;

/// <summary>What the terminal retry of a credential failure asks of the shell.</summary>
/// <param name="host">Holds the tabs and what they run.</param>
/// <param name="cover">Tells when an overlay is open.</param>
/// <param name="tools">Runs git on the real terminal.</param>
public sealed class TerminalRetryHost(Dispatcher host, OverlayCover cover, ExternalToolRunner tools) : ITerminalRetryHost
{
    /// <inheritdoc />
    public bool IsOverlayOpen() => cover.IsAnyOverlayOpen();

    /// <inheritdoc />
    public bool IsBusy(TabId tabId) => host.Workspace.FindTab(tabId)?.Running.IsBusy == true;

    /// <inheritdoc />
    public string RunGitInTerminal(string name, IReadOnlyList<string> arguments, RepositoryLocation location) =>
        tools.LaunchGit(name, arguments, location);
}
