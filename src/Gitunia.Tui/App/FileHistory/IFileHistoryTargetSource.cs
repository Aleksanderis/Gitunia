namespace Gitunia.Tui.App.FileHistory;

/// <summary>Tells which file the cursor of the focused pane is on.</summary>
public interface IFileHistoryTargetSource
{
    /// <summary>The file under the cursor of the focused file list, null when the cursor is not on a file.</summary>
    FileHistoryTarget? FileUnderCursor();
}
