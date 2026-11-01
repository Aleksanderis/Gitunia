using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;

namespace Gitunia.Core.Shell;

/// <summary>
/// Key mode, status messages, the confirm dialog and the output overlay as pure transitions
/// <c>(ShellState, ShellCommand) -> ShellUpdate</c>. No clock is read: commands carry the time.
/// </summary>
public static class ShellReducer
{
    /// <summary>The warning shown when a resize leaves no room for the open dialog.</summary>
    public const string ResizedNotice = "The screen became too small for the question: nothing was done";

    /// <summary>Applies a command to the shell state.</summary>
    public static ShellUpdate Reduce(ShellState state, ShellCommand command) => command switch
    {
        ConfirmAnsweredCommand answered => Answer(state, answered.Answer),
        ConfirmRequestedCommand request => Ask(state, request),
        OutputClosedCommand => CloseOutput(state),
        OutputShownCommand shown => ShowOutput(state, shown),
        RunActionCommand run => new ShellUpdate(state, [new RunActionEffect(run.Run)]),
        ScreenResizedCommand resized => Resize(state, resized),
        SetKeyModeCommand mode => new ShellUpdate(WithMode(state, mode.Mode), []),
        StatusAgedCommand aged => new ShellUpdate(state, Schedule(state, aged.At)),
        StatusDismissedCommand dismissed => Dismiss(state, dismissed.At),
        StatusPostedCommand posted => Post(state, posted),
        _ => throw new UnhandledCommandException(command),
    };

    // The key mode to return to when an open dialog closes: an open dialog or box replaced by a newer one keeps its.
    private static KeyMode ReturnModeOf(ShellState state)
    {
        if (state.Confirm is { } confirm)
        {
            return confirm.ReturnMode;
        }

        return state.Output is { } output ? output.ReturnMode : state.Mode;
    }

    private static ShellState WithMode(ShellState state, KeyMode mode) =>
        state with { Mode = mode, ModeChanges = state.ModeChanges + 1 };

    private static ShellUpdate Ask(ShellState state, ConfirmRequestedCommand request)
    {
        if (!Fits(state, request.Prompt))
        {
            return new ShellUpdate(state, [], request.Refusal);
        }

        var dialog = new ConfirmDialog(request.Prompt, request.OnAccepted, ReturnModeOf(state));
        var opened = WithMode(state with { Confirm = dialog, Output = null }, KeyMode.Confirm);

        return new ShellUpdate(opened, []);
    }

    private static bool Fits(ShellState state, ConfirmPrompt prompt) =>
        state.Screen is not { } screen || ConfirmLayout.Fits(prompt, screen.Width, screen.Height);

    private static ShellUpdate Answer(ShellState state, ConfirmAnswer answer)
    {
        if (state.Confirm is not { } dialog)
        {
            return new ShellUpdate(state, []);
        }

        var closed = WithMode(state with { Confirm = null }, dialog.ReturnMode);

        return new ShellUpdate(closed, [], answer == ConfirmAnswer.Yes ? dialog.OnAccepted : null);
    }

    // A dialog nobody can read must not be answerable with a stray y: it closes without answering.
    private static ShellUpdate Resize(ShellState state, ScreenResizedCommand resized)
    {
        var resizedState = state with { Screen = resized.Screen };
        if (state.Confirm is not { } dialog || Fits(resizedState, dialog.Prompt))
        {
            return new ShellUpdate(resizedState, []);
        }

        var closed = WithMode(resizedState with { Confirm = null }, dialog.ReturnMode);
        var warning = new StatusPostedCommand(ResizedNotice, StatusSeverity.Warning, resized.At);

        return new ShellUpdate(closed, [], warning);
    }

    private static ShellUpdate ShowOutput(ShellState state, OutputShownCommand shown)
    {
        var output = new OutputOverlay(shown.Title, shown.Heading, shown.Lines, ReturnModeOf(state));
        var opened = WithMode(state with { Output = output, Confirm = null }, KeyMode.Normal);

        return new ShellUpdate(opened, []);
    }

    private static ShellUpdate CloseOutput(ShellState state)
    {
        if (state.Output is not { } output)
        {
            return new ShellUpdate(state, []);
        }

        return new ShellUpdate(WithMode(state with { Output = null }, output.ReturnMode), []);
    }

    private static ShellUpdate Post(ShellState state, StatusPostedCommand posted)
    {
        if (state.Status.Current(posted.At) is { } current
            && current.Text == posted.Text
            && current.Severity == posted.Severity)
        {
            // The same failure again (a repeated refresh) would only fill the history with copies.
            return new ShellUpdate(state, []);
        }

        var shown = state with { Status = state.Status.Post(posted.Text, posted.Severity, posted.At) };

        return new ShellUpdate(shown, Schedule(shown, posted.At));
    }

    private static ShellUpdate Dismiss(ShellState state, DateTimeOffset now)
    {
        if (state.Status.Current(now) is null)
        {
            return new ShellUpdate(state, []);
        }

        return new ShellUpdate(state with { Status = state.Status.Dismiss() }, []);
    }

    // The bar is drawn again when the shown message loses its highlight and when it expires.
    private static ImmutableArray<AppEffect> Schedule(ShellState state, DateTimeOffset now)
    {
        if (state.Status.Current(now) is { } message && StatusLifetime.NextChange(message, now) is { } next)
        {
            return [new ScheduleStatusAgingEffect(next - now)];
        }

        return [];
    }
}
