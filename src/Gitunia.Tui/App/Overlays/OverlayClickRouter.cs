using Gitunia.Core.Input;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Gives a click on an overlay line to the first overlay under it, in the order <see cref="InputRouting.ClickTargets"/>
/// gives.
/// </summary>
public sealed class OverlayClickRouter
{
    private readonly IReadOnlyDictionary<OverlayClickTarget, IOverlayClickTarget> _targets;

    /// <summary>Fails when a click target of the routing has no object behind it.</summary>
    /// <param name="targets">The object behind each click target.</param>
    public OverlayClickRouter(IReadOnlyDictionary<OverlayClickTarget, IOverlayClickTarget> targets)
    {
        _targets = targets;
        foreach (var target in InputRouting.ClickTargets().Where(target => !targets.ContainsKey(target)))
        {
            throw new InvalidOperationException($"No click handling for {target}");
        }
    }

    /// <summary>Handles a click on a frame line and column.</summary>
    /// <param name="line">The line of the frame.</param>
    /// <param name="column">The column of the frame.</param>
    /// <returns>What the click came to, null when no overlay is under it.</returns>
    public OverlayClick? ClickAt(int line, int column)
    {
        foreach (var target in InputRouting.ClickTargets())
        {
            if (_targets[target].ClickAt(line, column) is { } click)
            {
                return click;
            }
        }

        return null;
    }
}
