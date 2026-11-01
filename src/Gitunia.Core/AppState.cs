using Gitunia.Core.Workspaces;

namespace Gitunia.Core;

/// <summary>
/// The root state: everything that is logic and not drawing. Immutable, changed only by <see cref="AppReducer"/>, and
/// small (it is copied on every command). Big data (graph rows, diffs, trees) lives in stores in <c>Gitunia.App</c>,
/// never here. App-wide interaction state (key mode, overlays, running operations) joins it as its features move
/// (docs/architecture.md, "Effects as data").
/// </summary>
/// <param name="Workspace">The repository tabs with their per-tab state, recents and pins.</param>
public sealed record AppState(Workspace Workspace)
{
    /// <summary>No tabs, nothing open.</summary>
    public static AppState Empty { get; } = new(Workspace.Empty);
}
