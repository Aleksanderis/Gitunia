using Gitunia.App.Tabs;
using Gitunia.Core.Sidebar;

namespace Gitunia.App.Sidebar;

/// <summary>
/// Looks up the commit of a sidebar row in the loaded graph of the tab's session, which holds the rows, and answers with
/// the index or that it is missing. Runs on the UI thread. It never reads or changes state.
/// </summary>
/// <param name="sessions">The sessions of the open tabs.</param>
/// <param name="dispatcher">Where the answer goes.</param>
public sealed class SidebarEffectHandler(ITabSessions sessions, IDispatcher dispatcher)
{
    /// <summary>Finds the commit and posts <see cref="SidebarCommitLocated"/> or <see cref="SidebarCommitMissing"/>.</summary>
    public void Locate(LocateGraphCommitEffect effect)
    {
        var session = sessions.Find(effect.TabId);
        var index = session?.Graph.IndexOf(effect.Commit) ?? -1;
        if (index >= 0)
        {
            dispatcher.Post(new SidebarCommitLocated(effect.TabId, index));

            return;
        }

        // Past the soft row limit the commit may still come: the rest loads now, and the next try finds it.
        session?.RequestExpand(true);
        dispatcher.Post(new SidebarCommitMissing(effect.TabId, effect.Label, session?.IsGraphComplete ?? true));
    }
}
