using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Ignore;

/// <summary>A change of <c>.gitignore</c>. Each posts one result <see cref="IgnoreCommand"/>, after the status of the tab
/// has been read again; none changes state itself.</summary>
public abstract record IgnoreEffect : OperationEffect
{
    private protected IgnoreEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Ignore)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }

    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new IgnoreFailed(TabId, text);
}

/// <summary>Append a pattern to the <c>.gitignore</c> at the root of the working tree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Pattern">The line the user picked.</param>
public sealed record AddIgnorePatternEffect(TabId TabId, RepositoryLocation Location, string Pattern)
    : IgnoreEffect(TabId, Location);
