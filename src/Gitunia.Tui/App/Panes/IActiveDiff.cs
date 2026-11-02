namespace Gitunia.Tui.App.Panes;

/// <summary>Which diff of the active tab a <see cref="DiffListActions"/> works on.</summary>
public interface IActiveDiff
{
    /// <summary>The active tab's diff, null while it has none.</summary>
    IDiffViewState? Current();
}
