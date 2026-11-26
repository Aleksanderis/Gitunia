using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Sync;

/// <summary>A fetch of a tab ended, not cancelled.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="IsBackground">The background timer started it (otherwise the user did).</param>
public sealed record FetchEnded(TabId TabId, FetchOutcome Outcome, bool IsBackground);
