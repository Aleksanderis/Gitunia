using Gitunia.Core.Status;
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
    StatusChannel statuses,
    Action<RepositoryLocation> refresh,
    Action pauseForUser,
    string gitExecutable = "git")
{
    private bool _running;
    private ExternalToolResult _result = new(ExternalToolResult.NotStartedExitCode, "did not run");

    /// <summary>Starts the hand-off; a notice for the status bar when it cannot start, empty otherwise.</summary>
    public string Launch(ExternalToolKind kind, RepositoryLocation location, string? commit = null)
    {
        if (_running)
        {
            return "An external tool is already running";
        }

        if (!launcher.IsSupported)
        {
            return "External tools are not supported on this operating system";
        }

        var request = new ExternalToolRequest(
            gitExecutable,
            ExternalToolCommands.Build(kind, location.CommandPath, commit),
            location.CommandPath);
        _running = true;
        try
        {
            terminal.Suspend(() => Run(request), () => Resumed(kind, location));
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

            pauseForUser();
        }
    }

    private void Resumed(ExternalToolKind kind, RepositoryLocation location)
    {
        _running = false;
        var result = _result;
        var name = ExternalToolCommands.NameOf(kind);
        if (result.Succeeded)
        {
            statuses.Post(result.Describe(name));
        }
        else
        {
            statuses.Error(result.Describe(name));
        }

        refresh(location);
    }
}
