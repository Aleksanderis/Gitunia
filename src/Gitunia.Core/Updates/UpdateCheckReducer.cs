using Gitunia.Core.Shell;

namespace Gitunia.Core.Updates;

/// <summary>
/// The update hint as pure transitions: a request starts the check only when it is allowed and the running build is a
/// release (a development build has no release to compare with); a result sets or clears the hint.
/// </summary>
public static class UpdateCheckReducer
{
    /// <summary>Applies a command to the shell state.</summary>
    public static ShellUpdate Reduce(ShellState state, UpdateCheckCommand command) => command switch
    {
        UpdateCheckRequested request => Request(state, request),
        UpdateCheckCompleted completed => Complete(state, completed),
        _ => throw new UnhandledCommandException(command),
    };

    private static ShellUpdate Request(ShellState state, UpdateCheckRequested request)
    {
        if (!request.Enabled || !IsRelease(request.CurrentVersion))
        {
            return new ShellUpdate(state with { Update = UpdateHintState.Empty }, []);
        }

        var allowed = state.Update with { Allowed = true };

        return new ShellUpdate(state with { Update = allowed }, [new CheckForUpdateEffect(request.CurrentVersion)]);
    }

    // A result that arrives after the setting was turned off must not bring the hint back.
    private static ShellUpdate Complete(ShellState state, UpdateCheckCompleted completed)
    {
        if (!state.Update.Allowed)
        {
            return new ShellUpdate(state, []);
        }

        return new ShellUpdate(state with { Update = state.Update with { Notice = completed.Notice } }, []);
    }

    private static bool IsRelease(string version) =>
        ReleaseVersion.TryParse(version, out var parsed) && !parsed.IsDevelopmentBuild;
}
