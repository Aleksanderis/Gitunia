namespace Gitunia.Tui.App.ExternalTools;

/// <summary>Waits for the user after a tool failed, so its message stays readable.</summary>
public interface IUserPause
{
    /// <summary>Blocks until the user presses a key.</summary>
    void Wait();
}
