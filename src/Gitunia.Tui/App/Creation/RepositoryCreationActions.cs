using Gitunia.App.Shell;
using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Picker;
using Gitunia.Core.Search;
using Gitunia.Core.Status;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Overlays;
using Gitunia.Core.Text;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Creation;

/// <summary>
/// Clone and init from the repository picker (ux-workflows.md, workflow 0). <c>Alt-c</c> asks for a source, then for the
/// directory (default: the folder typed in the picker, else the first scan root, plus the repository name) and clones
/// with the progress as the status bar line; <c>Alt-i</c> asks for a directory and runs <c>git init</c> there. Both
/// open the result in a new tab. One creation runs at a time and belongs to no tab, so any tab (or none) stays usable.
/// <c>Esc</c> or the cancel chip stops a clone; the creator removes what the clone made. A clone never prompts for
/// credentials: a failure says so and names the way out. #45 offers the real terminal, the integration point is
/// <see cref="FailureText"/> (an auth failure is recognised there) and the clone call in <c>RunCloneAsync</c>.
/// </summary>
/// <param name="creator">Runs clone and init.</param>
/// <param name="picker">The picker the prompts open from and the result is opened through.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="host">Switches the keymap mode.</param>
/// <param name="redraw">Redraws the chrome (popup, progress line, chips).</param>
/// <param name="homeDirectory">The home directory (<c>~</c>).</param>
/// <param name="defaultParent">The folder a clone goes into when the picker has no path: the first scan root.</param>
public sealed class RepositoryCreationActions(
    IRepositoryCreator creator,
    PickerController picker,
    StatusReporter statuses,
    Action<Action> postToUi,
    Dispatcher host,
    Action redraw,
    string homeDirectory,
    Func<string> defaultParent) : IPromptHandler
{
    private const string PickerPrefix = "picker.";
    private const string BusyNotice = "Busy: a clone or init is still running";
    private const string LocatingNotice = "Not while locating a repository";
    private const string EmptyNotice = "Type something first";
    private const string CloneActivity = "Cloning";
    private const string CancelledText = "Clone cancelled; nothing was left behind";
    private const string PromptsOffMarker = "terminal prompts disabled";
    private const string DeniedMarker = "Permission denied";
    private const string AuthMarker = "Authentication failed";
    private const string CredentialHint =
        "set up a credential helper or an ssh-agent key, credential prompts are not supported yet";

    private CreationPromptKind _kind;
    private string _text = string.Empty;
    private string _source = string.Empty;
    private Running? _running;
    private bool _isInitRunning;

    public string Notice => string.Empty;

    public string StatusLine => string.Empty;

    /// <summary>Whether a clone runs (the status bar shows its progress and a cancel chip).</summary>
    public bool IsCloning => _running is not null;

    public PromptPopup? Popup
    {
        get
        {
            if (_kind == CreationPromptKind.None || !picker.IsOpen)
            {
                return null;
            }

            return _kind switch
            {
                CreationPromptKind.CloneSource => PromptPopup.ForText(
                    "Clone repository", "URL or path of the repository to clone", _text, "next"),
                CreationPromptKind.CloneTarget => PromptPopup.ForText(
                    "Clone repository", $"Clone {_source} into", _text, "clone"),
                _ => PromptPopup.ForText(
                    "Initialise repository",
                    "Directory for git init (created when missing, files are kept)",
                    _text,
                    "init"),
            };
        }
    }

    /// <summary>
    /// The status bar line of the running clone (<c>Cloning: Receiving objects 45%</c>), empty when none.
    /// </summary>
    public string ProgressLine()
    {
        if (_running is not { } running)
        {
            return string.Empty;
        }

        var line = RemoteProgressText.Format(CloneActivity, running.Latest);
        return running.IsCancelling ? $"{line} (cancelling)" : line;
    }

    public bool TryExecute(ActionId action, int? count)
    {
        if (_kind != CreationPromptKind.None && !picker.IsOpen)
        {
            // The picker went away behind the prompt (another tab was activated): the question is void.
            Close();
        }

        if (_kind != CreationPromptKind.None)
        {
            return TryHandleOpenPrompt(action);
        }

        if (action == ActionIds.RemoteCancel)
        {
            return _running is not null && Cancel();
        }

        if (action != ActionIds.PickerClone && action != ActionIds.PickerInit)
        {
            return false;
        }

        if (picker.State is not { } state)
        {
            return false;
        }

        Warn(NoticeFor(action, state));
        redraw();
        return true;
    }

    /// <summary>Esc cancels a running clone; the caller asks only after every other holder of Esc passed.</summary>
    public bool TryCancelWithEscape(ActionId action) =>
        action == ActionIds.Cancel && _running is not null && Cancel();

    public bool HandleTextKey(KeyEvent key)
    {
        if (_kind == CreationPromptKind.None || !picker.IsOpen)
        {
            return false;
        }

        if (PromptTextEditor.Apply(_text, key) is { } edited)
        {
            _text = edited;
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }

    /// <summary>
    /// Stops a running clone and waits for it to clean up (at the end of the program, so a half-made directory does
    /// not stay); gives up after a few seconds.
    /// </summary>
    public void CancelAndWait()
    {
        if (_running is not { } running)
        {
            return;
        }

        running.Cancellation.Cancel();
        running.Task?.Wait(TimeSpan.FromSeconds(5));
    }

    private static string FailureText(Exception exception)
    {
        var reason = exception is CloneTargetException ? exception.Message : RemoteFailureText.Reason(exception);
        var needsCredentials = reason.Contains(PromptsOffMarker, StringComparison.OrdinalIgnoreCase)
            || reason.Contains(DeniedMarker, StringComparison.OrdinalIgnoreCase)
            || reason.Contains(AuthMarker, StringComparison.OrdinalIgnoreCase);
        return needsCredentials ? $"{reason} ({CredentialHint})" : reason;
    }

    private bool TryHandleOpenPrompt(ActionId action)
    {
        if (action == ActionIds.InputAccept || action == ActionIds.PickerAccept)
        {
            Warn(Accept());
        }
        else if (action == ActionIds.Cancel)
        {
            Close();
        }
        else if (!action.Value.StartsWith(PickerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        redraw();
        return true;
    }

    private string NoticeFor(ActionId action, PickerState state)
    {
        if (state.LocatingTabId is not null)
        {
            return LocatingNotice;
        }

        if (_running is not null || _isInitRunning)
        {
            return BusyNotice;
        }

        return Begin(action, state);
    }

    private string Begin(ActionId action, PickerState state)
    {
        if (action == ActionIds.PickerClone)
        {
            _kind = CreationPromptKind.CloneSource;
            _text = string.Empty;
        }
        else
        {
            _kind = CreationPromptKind.InitDirectory;
            _text = Abbreviate(state.IsPathMode ? ResolveTyped(state.Query) : picker.BaseDirectory());
        }

        host.SetKeyMode(KeyMode.Insert);
        return string.Empty;
    }

    private string Accept()
    {
        var typed = _text.Trim();
        if (typed.Length == 0)
        {
            return EmptyNotice;
        }

        if (_kind == CreationPromptKind.CloneSource)
        {
            _source = CloneSource.Resolve(typed, picker.BaseDirectory(), homeDirectory);
            _kind = CreationPromptKind.CloneTarget;
            _text = Abbreviate(Path.Combine(ClonePrefix(), CloneSource.RepositoryName(_source)));
            return string.Empty;
        }

        var directory = PathQuery.Resolve(typed, homeDirectory, picker.BaseDirectory());
        var kind = _kind;
        Close();
        if (kind == CreationPromptKind.CloneTarget)
        {
            StartClone(new CloneRequest(_source, directory));
        }
        else
        {
            StartInit(directory);
        }

        return string.Empty;
    }

    // The notices go to the status bar: this handler claims the action, so the router never reads its Notice.
    private void Warn(string text)
    {
        if (text.Length > 0)
        {
            statuses.Post(text, StatusSeverity.Warning);
        }
    }

    private string ClonePrefix()
    {
        if (picker.State is { IsPathMode: true } state)
        {
            var typed = ResolveTyped(state.Query);
            var endsWithSeparator = state.Query.EndsWith('/') || state.Query.EndsWith('\\');
            return endsWithSeparator || Directory.Exists(typed) ? typed : Path.GetDirectoryName(typed) ?? typed;
        }

        return defaultParent();
    }

    private string ResolveTyped(string query) => PathQuery.Resolve(query, homeDirectory, picker.BaseDirectory());

    private string Abbreviate(string path) => PathDisplay.Abbreviate(path, homeDirectory, OperatingSystem.IsWindows());

    private void Close()
    {
        _kind = CreationPromptKind.None;
        _text = string.Empty;
        host.SetKeyMode(picker.IsOpen ? KeyMode.Insert : KeyMode.Normal);
    }

    private bool Cancel()
    {
        if (_running is not { } running)
        {
            return false;
        }

        running.IsCancelling = true;
        running.Cancellation.Cancel();
        redraw();
        return true;
    }

    private void StartClone(CloneRequest request)
    {
        var running = new Running(new CancellationTokenSource());
        _running = running;
        var progress = new UiProgress(report => postToUi(() => OnProgress(running, report)));
        running.Task = Task.Run(() => RunCloneAsync(running, request, progress), CancellationToken.None);
        redraw();
    }

    // The place to add credential prompts (#45): this call runs with prompts off and fails with git's message.
    private async Task RunCloneAsync(Running running, CloneRequest request, IProgress<RemoteProgress> progress)
    {
        try
        {
            var location = await creator
                .CloneAsync(request, progress, running.Cancellation.Token)
                .ConfigureAwait(false);
            var text = $"Cloned {Path.GetFileName(location.RootPath)}";
            postToUi(() => Finish(running, text, location, StatusSeverity.Info));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Finish(running, CancelledText, null, StatusSeverity.Warning));
        }
        catch (Exception exception) when (exception is GitException or IOException or UnauthorizedAccessException)
        {
            var text = $"Clone failed: {FailureText(exception)}";
            postToUi(() => Finish(running, text, null, StatusSeverity.Error));
        }
    }

    private void StartInit(string directory)
    {
        _isInitRunning = true;
        _ = Task.Run(() => RunInitAsync(directory), CancellationToken.None);
        redraw();
    }

    private async Task RunInitAsync(string directory)
    {
        try
        {
            var result = await creator.InitAsync(directory, CancellationToken.None).ConfigureAwait(false);
            var name = Path.GetFileName(result.Location.RootPath);
            var text = result.WasRepository ? $"{name} already is a repository: opened it" : $"Initialised {name}";
            postToUi(() => FinishInit(text, result.Location, StatusSeverity.Info));
        }
        catch (Exception exception) when (exception is GitException or IOException or UnauthorizedAccessException)
        {
            var text = $"Init failed: {FailureText(exception)}";
            postToUi(() => FinishInit(text, null, StatusSeverity.Error));
        }
    }

    private void OnProgress(Running running, RemoteProgress report)
    {
        if (_running == running)
        {
            running.Latest = report;
            redraw();
        }
    }

    private void Finish(Running running, string text, RepositoryLocation? location, StatusSeverity severity)
    {
        running.Cancellation.Dispose();
        _running = null;
        statuses.Post(text, severity);
        if (location is not null)
        {
            picker.OpenLocation(location);
        }

        redraw();
    }

    private void FinishInit(string text, RepositoryLocation? location, StatusSeverity severity)
    {
        _isInitRunning = false;
        statuses.Post(text, severity);
        if (location is not null)
        {
            picker.OpenLocation(location);
        }

        redraw();
    }

    private sealed class Running(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

        public Task? Task { get; set; }

        public RemoteProgress? Latest { get; set; }

        public bool IsCancelling { get; set; }
    }

    private sealed class UiProgress(Action<RemoteProgress> report) : IProgress<RemoteProgress>
    {
        public void Report(RemoteProgress value) => report(value);
    }
}
