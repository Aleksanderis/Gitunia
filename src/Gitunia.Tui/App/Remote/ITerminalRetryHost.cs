using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.App.Remote;

/// <summary>What the terminal retry needs from the application shell: the overlays, the tab activity and the terminal.</summary>
public interface ITerminalRetryHost
{
    /// <summary>Whether a prompt, picker or other overlay is open; the retry Confirm never replaces one (a stray <c>y</c>).</summary>
    bool IsOverlayOpen();

    /// <summary>Whether an operation of the tab runs; the terminal must not race it.</summary>
    bool IsBusy(TabId tabId);

    /// <summary>
    /// Runs git with these arguments on the real terminal (name for the status bar, arguments, repository); returns a
    /// notice when it cannot start, empty otherwise.
    /// </summary>
    string RunGitInTerminal(string name, IReadOnlyList<string> arguments, RepositoryLocation location);
}
