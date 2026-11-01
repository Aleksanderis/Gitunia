using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Staging;

/// <summary>Reads the status of a tab again after the index changed. The runner implements it over the tab's
/// refresher.</summary>
public interface IStatusRefresher
{
    /// <summary>
    /// Re-reads the index-dependent status of the tab and runs <paramref name="done"/> on the UI thread once a read
    /// that started after this call has been applied or has failed. Runs it at once when the tab has no refresher (not
    /// loaded, or closed). Any thread may call it.
    /// </summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="done">What to do when the new status is in.</param>
    void RefreshThen(TabId tabId, Action done);
}
