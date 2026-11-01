namespace Gitunia.Tui.App.Pick;

/// <summary>Says what a cherry-pick or revert acts on right now and ends the Visual range when it took it.</summary>
public interface IPickSelectionSource
{
    /// <summary>The sidebar row, the graph commit or the Visual range as the user sees it at this moment.</summary>
    PickSelection Current();

    /// <summary>Ends the Visual range.</summary>
    void EndRange();
}
