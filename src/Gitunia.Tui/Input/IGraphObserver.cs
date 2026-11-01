namespace Gitunia.Tui.Input;

/// <summary>Wants to know when the loaded graph changes (rows appended or loading finished).</summary>
public interface IGraphObserver
{
    /// <summary>The loaded graph changed.</summary>
    void OnGraphChanged();
}
