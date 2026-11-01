using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Stash;
using Gitunia.App.Loading.Trees;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>The stores the runner builds for one tab; a session takes the ones it needs, a terminal session the diff stores too.</summary>
/// <param name="TabId">The tab the session belongs to.</param>
/// <param name="Details">The details of the selected commit.</param>
/// <param name="DiffStore">The diff of a commit file, null in sessions built without it (tests).</param>
/// <param name="WorkingDiffStore">The diff of a changed file, null in sessions built without it (tests).</param>
/// <param name="Tree">The Files tab, null in sessions built without it (tests).</param>
/// <param name="ContentStore">The blob reader of the Files tab, null in sessions built without it (tests).</param>
/// <param name="StashView">The stash view, a new one when null.</param>
public sealed record TabSessionParts(
    TabId TabId,
    CommitDetailsStore Details,
    DiffStore? DiffStore = null,
    WorkingDiffStore? WorkingDiffStore = null,
    FileTreeSession? Tree = null,
    FileContentStore? ContentStore = null,
    StashViewSession? StashView = null);
