using Gitunia.App.Loading.Stash;
using Gitunia.Git.Stash;

namespace Gitunia.Tui.App.Stash;

/// <summary>
/// The runtime side of one tab's stash view: the stashes the tab can read like commits and the one the detail pane
/// shows now. UI thread only.
/// </summary>
public sealed class StashViewSession
{
    /// <summary>The stashes the tab's details and diff readers know by commit hash.</summary>
    public StashRegistry Registry { get; } = new();

    /// <summary>
    /// The stash the detail pane was asked to show, null for none. It only counts while the tab's details still
    /// select its commit (see <see cref="StashViewController"/>).
    /// </summary>
    public StashEntry? Shown { get; set; }
}
