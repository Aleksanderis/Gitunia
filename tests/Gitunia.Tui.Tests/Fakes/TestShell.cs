using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// The shell state of a test: a dispatcher whose confirm dialogs, key mode, status messages and output box are the
/// real ones (<see cref="ShellState"/>). It records every dialog that was shown, answers them like a user, runs the
/// callbacks of controllers that have not moved to commands yet and opens the prompts that reducers ask for, as the
/// effect runner does in the app.
/// </summary>
public sealed class TestShell
{
    private ConfirmDialog? _seen;
    private long _modeChanges;

    public TestShell(Dispatcher? host = null)
    {
        Host = host ?? new Dispatcher(AppState.Empty);
        Statuses = new StatusReporter(Host);
        Host.EffectRaised += RunCallback;
        Host.Changed += RecordDialog;
    }

    public Dispatcher Host { get; }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public StatusReporter Statuses { get; }

    /// <summary>Every confirm dialog that was shown, oldest first.</summary>
    // ReSharper disable once CollectionNeverQueried.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public List<ConfirmPrompt> Prompts { get; } = [];

    /// <summary>The key mode after every switch, oldest first (a switch to the same mode counts).</summary>
    // ReSharper disable once CollectionNeverQueried.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public List<KeyMode> Modes { get; } = [];

    /// <summary>The output boxes that were shown, oldest first.</summary>
    public List<OutputOverlay> Outputs { get; } = [];

    /// <summary>The commands that accepted dialogs ran, oldest first (the terminal retry stands for any accepted command).</summary>
    // ReSharper disable once CollectionNeverQueried.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public List<RunTerminalRetryEffect> Accepted { get; } = [];

    /// <summary>Whether a dialog was still open when each accepted command ran, parallel to <see cref="Accepted"/>.</summary>
    // ReSharper disable once CollectionNeverQueried.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public List<bool> AcceptedWhileAsking { get; } = [];

    public KeyMode Mode => Host.State.Shell.Mode;

    public bool IsAsking => Host.State.Shell.Confirm is not null;

    /// <summary>False makes the screen too small for any dialog: it is not shown and the action must not start.</summary>
    public bool CanShowDialog
    {
        get;
        set
        {
            field = value;
            var size = value ? new ScreenExtent(200, 60) : new ScreenExtent(1, 1);
            Host.Dispatch(new ScreenResizedCommand(size, Host.Clock.GetUtcNow()));
        }
    } = true;

    /// <summary>
    /// Asks a confirm dialog whose accepted command is a terminal retry (recorded in <see cref="Accepted"/>); false
    /// when the dialog did not fit the screen and nothing was shown.
    /// </summary>
    public bool Confirm(ConfirmPrompt prompt)
    {
        Host.Dispatch(new ConfirmRequestedCommand(prompt, new RetryInTerminalConfirmed(default, "Test", []), null));

        return ReferenceEquals(Host.State.Shell.Confirm?.Prompt, prompt);
    }

    /// <summary>Answers the open dialog with <c>y</c>; false when none is open.</summary>
    public bool Answer() => Reply(ConfirmAnswer.Yes);

    /// <summary>Answers the open dialog with <c>n</c>; false when none is open.</summary>
    public bool Decline() => Reply(ConfirmAnswer.No);

    private bool Reply(ConfirmAnswer answer)
    {
        if (!IsAsking)
        {
            return false;
        }

        Host.Dispatch(new ConfirmAnsweredCommand(answer));

        return true;
    }

    private void RunCallback(AppEffect effect)
    {
        if (effect is RunTerminalRetryEffect retry)
        {
            Accepted.Add(retry);
            AcceptedWhileAsking.Add(IsAsking);
        }
        else if (effect is ShowPromptEffect prompt)
        {
            Host.Dispatch(new PromptRequestedCommand(prompt.Request));
        }
    }

    private void RecordDialog()
    {
        var shell = Host.State.Shell;
        if (shell.ModeChanges != _modeChanges)
        {
            _modeChanges = shell.ModeChanges;
            Modes.Add(shell.Mode);
        }

        if (shell.Output is { } output && !Outputs.Contains(output))
        {
            Outputs.Add(output);
        }

        if (shell.Confirm is { } dialog && !ReferenceEquals(dialog, _seen))
        {
            Prompts.Add(dialog.Prompt);
        }

        _seen = shell.Confirm;
    }
}
