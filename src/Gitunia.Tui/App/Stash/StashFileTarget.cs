using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Tui.App.Stash;

/// <summary>The file of a shown stash that a restore acts on: fixed when the action starts.</summary>
/// <param name="Entry">The stash the detail pane shows.</param>
/// <param name="File">The file row under the cursor and where in the stash it is kept.</param>
public sealed record StashFileTarget(StashEntry Entry, StashFileChange File);
