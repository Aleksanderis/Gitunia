namespace Gitunia.Tui.App;

/// <summary>
/// Keeps a quit from losing an unsent commit message without a word (ux.md, workflow 2): the first quit with such a message only
/// says so, a second quit right after (nothing else pressed in between) goes through. Pure; the application resets it on every other action.
/// </summary>
public sealed class QuitGuard
{
    private bool _isArmed;

    /// <summary>The warning for the status bar naming the tabs that hold a message.</summary>
    /// <param name="tabNames">The tabs with an unsent message.</param>
    public static string Warning(IReadOnlyList<string> tabNames) =>
        $"Unsent commit message in {string.Join(", ", tabNames)}: quit again to lose it, or commit it first";

    /// <summary>True when the program may stop now. With unsent messages the first call arms the guard and returns false.</summary>
    /// <param name="tabNames">The tabs with an unsent message; empty lets the quit through.</param>
    public bool MayQuit(IReadOnlyList<string> tabNames)
    {
        if (tabNames.Count == 0 || _isArmed)
        {
            return true;
        }

        _isArmed = true;
        return false;
    }

    /// <summary>Disarms the guard: another action came between the two quits.</summary>
    public void Reset() => _isArmed = false;
}
