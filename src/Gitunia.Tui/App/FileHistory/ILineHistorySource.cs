namespace Gitunia.Tui.App.FileHistory;

/// <summary>Tells which lines of a file the focused diff has selected.</summary>
public interface ILineHistorySource
{
    /// <summary>The Visual range of the focused diff, else its cursor line; refused when the focus is not on a diff.</summary>
    LineHistoryTarget Resolve();
}
