using Gitunia.Core.Running;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Discard;

/// <summary>A change of the working tree that cannot be undone. Each posts one result <see cref="DiscardCommand"/>, after
/// the status of the tab has been read again; none changes state itself.</summary>
public abstract record DiscardEffect : OperationEffect
{
    private protected DiscardEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Discard)
    {
        Location = location;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }

    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new DiscardFailed(TabId, text);
}

/// <summary>Restore the tracked files from the index and delete the untracked ones, in one batch.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Files">The files the dialog counted.</param>
/// <param name="Skipped">How many picked files were left out (reported).</param>
public sealed record DiscardFilesEffect(
    TabId TabId,
    RepositoryLocation Location,
    IReadOnlyList<StatusEntry> Files,
    int Skipped) : DiscardEffect(TabId, Location);

/// <summary>Discard one file whole, or the hunk or lines picked in its diff.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Selection">The file and the lines; no lines discards the whole file.</param>
/// <param name="AllowWholeFile">A file that cannot be split goes whole. True only when the dialog said so; a hunk or
/// lines that cannot be cut are refused, never widened.</param>
/// <param name="Move">Where the diff cursor goes afterwards, null when the key was not in the diff.</param>
public sealed record DiscardSelectionEffect(
    TabId TabId,
    RepositoryLocation Location,
    StagingSelection Selection,
    bool AllowWholeFile,
    DiffCursorMove? Move) : DiscardEffect(TabId, Location);
