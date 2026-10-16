using System.Collections.Immutable;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Persistence;

/// <summary>A workspace rebuilt from the session file with the commit drafts of its tabs.</summary>
/// <param name="Update">The workspace and its load effects.</param>
/// <param name="Drafts">The saved drafts by the id the tab got in <paramref name="Update"/>.</param>
public sealed record RestoredSession(WorkspaceUpdate Update, ImmutableDictionary<TabId, SessionDraft> Drafts);
