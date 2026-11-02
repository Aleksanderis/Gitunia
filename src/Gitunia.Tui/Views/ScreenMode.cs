namespace Gitunia.Tui.Views;

/// <summary>What the screen shows: the history panes or the Changes view, and the repository picker over either.</summary>
public sealed class ScreenMode
{
    /// <summary>The Changes view is shown in place of the history panes.</summary>
    public bool IsWorking { get; set; }

    /// <summary>The repository picker covers the panes.</summary>
    public bool IsPicker { get; set; }

    /// <summary>The Changes view panes are on screen.</summary>
    public bool ShowsWorkingPanes => IsWorking && !IsPicker;
}
