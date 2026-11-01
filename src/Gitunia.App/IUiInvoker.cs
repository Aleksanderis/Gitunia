namespace Gitunia.App;

/// <summary>Runs an action on the UI thread, from any thread. The terminal layer implements it over its main
/// loop.</summary>
public interface IUiInvoker
{
    /// <summary>Queues the action for the UI thread.</summary>
    void Post(Action action);
}
