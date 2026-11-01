using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;

namespace Gitunia.Core.Shell;

/// <summary>
/// Decides whether a quit request leaves at once. Unsent commit messages are saved with the session on exit, so the only
/// guard left is a failed save: the first quit then warns, the next one leaves (#369). Pure: the save itself is
/// <see cref="FlushSessionEffect"/>.
/// </summary>
public static class QuitReducer
{
    /// <summary>The warning of the first quit after a failed save.</summary>
    public const string WarningText =
        "Unsent commit message and the session could not be saved: quit again to leave and lose it";

    /// <summary>Applies a command to the shell state.</summary>
    public static ShellUpdate Reduce(ShellState state, QuitCommand command) => command switch
    {
        QuitRequested request => Request(state, request),
        SessionFlushed flushed => Flushed(state, flushed),
        QuitWarningReset => new ShellUpdate(state with { QuitWarned = false }, []),
        _ => throw new UnhandledCommandException(command),
    };

    private static ShellUpdate Request(ShellState state, QuitRequested request)
    {
        if (state.QuitWarned || !request.HasUnsentMessage)
        {
            return new ShellUpdate(state, [new QuitEffect(request.Force)]);
        }

        return new ShellUpdate(state, [new FlushSessionEffect(request.Force)]);
    }

    private static ShellUpdate Flushed(ShellState state, SessionFlushed flushed)
    {
        if (flushed.Succeeded)
        {
            return new ShellUpdate(state, [new QuitEffect(flushed.Force)]);
        }

        ImmutableArray<AppEffect> warning = [new PostStatusEffect(WarningText, StatusSeverity.Warning)];

        return new ShellUpdate(state with { QuitWarned = true }, warning);
    }
}
