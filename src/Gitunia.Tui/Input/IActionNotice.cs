namespace Gitunia.Tui.Input;

/// <summary>A handler that tells the status bar about the action it just took (for example "No match").</summary>
public interface IActionNotice
{
    /// <summary>A short message about the last action, empty when none.</summary>
    string Notice { get; }
}
