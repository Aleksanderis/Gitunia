using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Sync;

/// <summary>The facts of the root the sync reducer reads.</summary>
/// <param name="TabId">The tab the command is for.</param>
/// <param name="Location">Where the tab runs its commands.</param>
/// <param name="Refs">The refs the tab shows.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsBusy">A fetch, pull, push or remote branch delete already runs for the tab.</param>
/// <param name="IsOtherBusy">An operation of another kind runs for the tab (a pull moves the working tree, and a transfer does not run beside remote edits).</param>
/// <param name="IsOverlayOpen">A prompt, confirm or output box is open; an unsolicited offer never replaces it.</param>
/// <param name="State">The tab's running transfer, null when none.</param>
public sealed record SyncContext(
    TabId TabId,
    RepositoryLocation Location,
    ImmutableArray<GitRef> Refs,
    bool IsActive,
    bool IsBusy,
    bool IsOtherBusy,
    bool IsOverlayOpen,
    SyncState? State);
