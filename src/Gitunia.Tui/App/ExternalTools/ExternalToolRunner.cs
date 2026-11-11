using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.Core.Shell;
using Gitunia.Git.Execution;
using Gitunia.Git.ExternalTools;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform.ExternalTools;

namespace Gitunia.Tui.App.ExternalTools;

/// <summary>
/// Hands the terminal to an external tool: suspends the UI, runs git with the real terminal, resumes, reports the
/// outcome in the status bar and asks for a refresh (the tool may have changed the repository).
/// </summary>
public sealed class ExternalToolRunner(
    IExternalToolLauncher launcher,
    ITerminalSuspender terminal,
    StatusReporter statuses,
    Dispatcher host,
    IUserPause pause,
    string gitExecutable = GitExecutableLocator.BareName)
{
    private bool _running;
    private ExternalToolResult _result = new(ExternalToolResult.NotStartedExitCode, "did not run");

    /// <summary>Starts the hand-off; a notice for the status bar when it cannot start, empty otherwise.</summary>
    public string Launch(ExternalToolKind kind, RepositoryLocation location, string? commit = null) =>
        Start(
            ExternalToolCommands.NameOf(kind),
            ExternalToolCommands.Build(kind, location.CommandPath, commit),
            location);

    /// <summary>
    /// Hands the terminal to a git command that needs to prompt (a fetch, pull or push that failed for lack of
    /// credentials, #45); <paramref name="name"/> is shown in the status bar. Returns a notice when it cannot start.
    /// </summary>
    public string LaunchGit(string name, IReadOnlyList<string> arguments, RepositoryLocation location) =>
        Start(name, ExternalToolCommands.InRepository(location.CommandPath, arguments), location);

    private string Start(string name, IReadOnlyList<string> arguments, RepositoryLocation location)
    {
        if (_running)
        {
            return "An external tool is already running";
        }

        if (!launcher.IsSupported)
        {
            return "External tools are not supported on this operating system";
        }

        var request = new ExternalToolRequest(gitExecutable, arguments, location.CommandPath);
        _running = true;
        try
        {
            terminal.Suspend(() => Run(request), () => Resumed(name, location));
        }
        catch
        {
            _running = false;
            throw;
        }

        return string.Empty;
    }

    private void Run(ExternalToolRequest request)
    {
        _result = launcher.Run(request);
        if (!_result.Succeeded)
        {
            // The tool's own message (nothing to commit, no files need merging, ...) is still on the terminal.
            if (_result.Error is { } error)
            {
                Console.Error.WriteLine(error);
            }

            pause.Wait();
        }
    }

    private void Resumed(string name, RepositoryLocation location)
    {
        _running = false;
        var result = _result;
        if (result.Succeeded)
        {
            statuses.Post(result.Describe(name));
        }
        else
        {
            statuses.Error(result.Describe(name));
        }

        if (host.Workspace.ActiveTabId is { } tabId && host.Workspace.FindTab(tabId)?.Location == location)
        {
            host.Dispatch(new TabRefreshRequested(tabId, false));
        }
    }
}
