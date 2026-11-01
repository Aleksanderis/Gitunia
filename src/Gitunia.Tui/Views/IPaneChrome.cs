namespace Gitunia.Tui.Views;

/// <summary>What the pane views ask of the screen they sit in: its size, and a new layout or redraw after they changed.</summary>
public interface IPaneChrome
{
    /// <summary>Terminal columns and rows.</summary>
    ScreenSize ScreenSize { get; }

    /// <summary>Asks the views to lay out again after the pane arrangement changed.</summary>
    void Relayout();

    /// <summary>Redraws the pane titles and the status bar after a pane changed on its own.</summary>
    void RefreshChrome();
}
