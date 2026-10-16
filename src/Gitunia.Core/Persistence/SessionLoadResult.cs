using System.Collections.Immutable;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Persistence;

/// <summary>The outcome of loading the session: the workspace to start with (and its load effects), and why.</summary>
/// <param name="Update">The restored workspace with a load effect per present tab; empty when nothing was restored.</param>
/// <param name="Status">How loading went.</param>
/// <param name="Detail">A short message for the status line when the file was discarded, otherwise null.</param>
public sealed record SessionLoadResult(WorkspaceUpdate Update, SessionLoadStatus Status, string? Detail)
{
    /// <summary>The saved commit drafts by tab id in <see cref="Update"/>; empty when none.</summary>
    public ImmutableDictionary<TabId, SessionDraft> Drafts { get; init; } = [];
}
