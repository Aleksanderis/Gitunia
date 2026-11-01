using Gitunia.Core;

namespace Gitunia.App;

/// <summary>
/// Where effect handlers send their result commands. Any thread may post; the reducer always runs on the UI thread.
/// </summary>
public interface IDispatcher
{
    /// <summary>Queues the command for the UI thread, where it is reduced and its effects are handed to the
    /// runner.</summary>
    void Post(AppCommand command);
}
