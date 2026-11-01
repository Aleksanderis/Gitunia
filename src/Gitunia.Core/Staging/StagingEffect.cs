using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Staging;

/// <summary>A change of the index. Each posts one result <see cref="StagingCommand"/>, after the status of the tab has
/// been read again; none changes state itself.</summary>
public abstract record StagingEffect : OperationEffect
{
    private protected StagingEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Staging)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Stage or unstage whole files (<c>git add</c>, <c>git restore --staged</c>).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Plan">The paths and what the report says.</param>
/// <param name="IsStage">Stage; false unstages.</param>
/// <param name="IsUnborn">There is no commit yet (unstaging then removes the entry from the index).</param>
/// <param name="Follow">What happens after it worked.</param>
public sealed record StagePathsEffect(
    TabId TabId,
    RepositoryLocation Location,
    StagingPlan Plan,
    bool IsStage,
    bool IsUnborn,
    StagingFollow Follow) : StagingEffect(TabId, Location);

/// <summary>Stage or unstage the hunk or lines picked in a diff.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Selection">The file, its side and the lines.</param>
/// <param name="IsUnborn">There is no commit yet.</param>
/// <param name="Move">Where the diff cursor goes afterwards.</param>
public sealed record StageSelectionEffect(
    TabId TabId,
    RepositoryLocation Location,
    StagingSelection Selection,
    bool IsUnborn,
    DiffCursorMove Move) : StagingEffect(TabId, Location);

/// <summary>Move the cursor of the diff once the diff of the staged file has been read again. The terminal layer
/// answers it; nothing comes back.</summary>
/// <param name="TabId">The tab the diff belongs to.</param>
/// <param name="Move">Where the cursor goes.</param>
public sealed record MoveDiffCursorEffect(TabId TabId, DiffCursorMove Move) : AppEffect;
