namespace Gitunia.Tui.App;

/// <summary>
/// Decides whether a quit request leaves at once. Unsent commit messages are saved with the session on exit, so the only guard
/// left is a failed save: the first quit then warns, the next one leaves (#369).
/// </summary>
/// <param name="hasUnsentMessage">Whether any tab holds an unsent commit message.</param>
/// <param name="flushSucceeded">Saves the session now; false when that failed.</param>
public sealed class QuitGuard(Func<bool> hasUnsentMessage, Func<bool> flushSucceeded)
{
    private bool _warned;

    /// <summary>True when the quit should go ahead; false for the first quit that is held back.</summary>
    public bool ShouldQuit()
    {
        if (_warned || !hasUnsentMessage() || flushSucceeded())
        {
            return true;
        }

        _warned = true;
        return false;
    }

    /// <summary>Forgets the warning, e.g. when a draft changed: a new unsent message deserves its own warning.</summary>
    public void Reset() => _warned = false;
}
