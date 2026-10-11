using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Persistence;

/// <summary>The outcome of loading the session: the workspace to start with (and its load effects), and why.</summary>
/// <param name="Update">The restored workspace with a load effect per present tab; empty when nothing was restored.</param>
/// <param name="Status">How loading went.</param>
/// <param name="Detail">A short message for the status line when the file was discarded, otherwise null.</param>
public sealed record SessionLoadResult(WorkspaceUpdate Update, SessionLoadStatus Status, string? Detail);
