using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Workspaces;

/// <summary>Work the runner performs after a state change. Effects are data and carry the path they run against.</summary>
public abstract record WorkspaceEffect : AppEffect
{
    private protected WorkspaceEffect()
    {
    }
}

/// <summary>Load HEAD and refs for a newly opened tab; results come back as <see cref="TabCommand"/>s with the same tab id.</summary>
/// <param name="TabId">The tab the results belong to.</param>
/// <param name="RepositoryPath">The path git runs against.</param>
public sealed record LoadRepositoryEffect(TabId TabId, string RepositoryPath) : WorkspaceEffect;

/// <summary>Cancel the in-flight load of a tab that was closed.</summary>
/// <param name="TabId">The closed tab.</param>
public sealed record CancelLoadEffect(TabId TabId) : WorkspaceEffect;
