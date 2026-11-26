using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Effects;

/// <summary>Read a tab again after a git operation changed it (refs, graph, status).</summary>
/// <param name="TabId">The tab to refresh.</param>
/// <param name="Announce">True for a refresh the user asked for (it also reads the issue links); false after an operation.</param>
/// <param name="ScrollToNewCommits">True after a fetch: when the reloaded graph holds commits the old one did not, the list scrolls to the top and selects the newest commit.</param>
public sealed record RefreshTabEffect(TabId TabId, bool Announce = false, bool ScrollToNewCommits = false) : AppEffect;
