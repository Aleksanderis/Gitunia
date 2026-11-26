namespace Gitunia.Core.Sync;

/// <summary>How a fetch ended, as far as the background timer cares.</summary>
public enum FetchOutcome
{
    /// <summary>The fetch finished.</summary>
    Succeeded,

    /// <summary>The fetch failed (offline, a lock, a bad remote).</summary>
    Failed,

    /// <summary>The fetch failed because the remote wants credentials, which the background never asks for.</summary>
    NeedsCredentials,
}
