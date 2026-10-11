namespace Gitunia.Core.Persistence;

/// <summary>How loading the session file went. Every status leaves the app able to start.</summary>
public enum SessionLoadStatus
{
    /// <summary>There was no session file (first run).</summary>
    Missing,

    /// <summary>The session was read and restored.</summary>
    Restored,

    /// <summary>The file was unreadable, malformed or from another schema version; it was set aside and an empty workspace is used.</summary>
    Discarded,
}
