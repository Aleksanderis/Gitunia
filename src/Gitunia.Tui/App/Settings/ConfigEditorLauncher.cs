using Gitunia.App.Status;
using Gitunia.Platform.ExternalTools;
using Gitunia.Tui.App.ExternalTools;

namespace Gitunia.Tui.App.Settings;

/// <summary>
/// Opens <c>config.json</c> in the user's editor (<c>$VISUAL</c>, else <c>$EDITOR</c>): suspends the UI, runs the
/// editor on the real terminal, resumes and reports how it ended. Like the external tools of the graph, but no
/// repository is involved.
/// The variable is split at spaces into the program and its arguments (no quoting, no shell).
/// </summary>
/// <param name="launcher">Runs the program on the terminal.</param>
/// <param name="terminal">Gives the terminal away for the duration.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="environment">Reads an environment variable (injected so tests need no real one).</param>
/// <param name="pause">Waits for a key after a failure, so its message stays readable.</param>
public sealed class ConfigEditorLauncher(
    IExternalToolLauncher launcher,
    ITerminalSuspender terminal,
    StatusReporter statuses,
    Func<string, string?> environment,
    IUserPause pause)
{
    private const string VisualVariable = "VISUAL";
    private const string EditorVariable = "EDITOR";
    private const string EditorName = "Editor";
    private const StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries
        | StringSplitOptions.TrimEntries;

    private bool _running;
    private ExternalToolResult _result = new(ExternalToolResult.NotStartedExitCode, "did not run");

    /// <summary>Starts the hand-off; returns the reason it cannot start, empty when it did.</summary>
    /// <param name="filePath">The file to edit.</param>
    /// <param name="resumed">Runs on the UI thread once the editor is closed and the UI is back.</param>
    public string Launch(string filePath, Action resumed)
    {
        if (_running)
        {
            return "The editor is already running";
        }

        if (!launcher.IsSupported)
        {
            return "Opening an editor is not supported on this operating system";
        }

        var command = Command();
        if (command.Length == 0)
        {
            return $"Set ${VisualVariable} or ${EditorVariable} to open config.json";
        }

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Could not create the config folder: {exception.Message}";
        }

        var request = new ExternalToolRequest(command[0], [.. command.Skip(1), filePath], directory);
        _running = true;
        try
        {
            terminal.Suspend(() => Run(request), () => Resumed(resumed));
        }
        catch
        {
            _running = false;
            throw;
        }

        return string.Empty;
    }

    private string[] Command()
    {
        var text = environment(VisualVariable);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = environment(EditorVariable);
        }

        return (text ?? string.Empty).Split(' ', SplitOptions);
    }

    private void Run(ExternalToolRequest request)
    {
        _result = launcher.Run(request);
        if (!_result.Succeeded)
        {
            if (_result.Error is { } error)
            {
                Console.Error.WriteLine(error);
            }

            pause.Wait();
        }
    }

    private void Resumed(Action resumed)
    {
        _running = false;
        if (_result.Succeeded)
        {
            statuses.Post(_result.Describe(EditorName));
        }
        else
        {
            statuses.Error(_result.Describe(EditorName));
        }

        resumed();
    }
}
