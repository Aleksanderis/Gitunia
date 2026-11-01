namespace Gitunia.Tui.App.Rebase;

/// <summary>Says whether an overlay other than the rebase editor is open (its Confirm above all) and covers it.</summary>
public interface IOverlayCover
{
    /// <summary>True while a dialog, box, prompt or other overlay is open over the editor.</summary>
    bool IsOtherOverlayOpen();
}
