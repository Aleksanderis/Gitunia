using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Effects;

/// <summary>Read a tab again after a git operation changed it (refs, graph, status), without announcing it.</summary>
/// <param name="TabId">The tab to refresh.</param>
public sealed record RefreshTabEffect(TabId TabId) : AppEffect;
