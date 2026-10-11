namespace Gitunia.Core.Persistence;

/// <summary>The session file contents: open tabs, active tab, recents and pins. Paths are absolute.</summary>
public sealed record SessionDocument
{
    /// <summary>The schema version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Schema version; a file with another version is not read.</summary>
    public int Version { get; init; }

    /// <summary>Index into <see cref="Tabs"/> of the active tab, null when there is none.</summary>
    public int? ActiveTabIndex { get; init; }

    /// <summary>Open tabs in display order.</summary>
    public List<SessionTabDocument>? Tabs { get; init; }

    /// <summary>Recently opened repository root paths, most recent first.</summary>
    public List<string>? Recents { get; init; }

    /// <summary>Pinned repository root paths.</summary>
    public List<string>? Pins { get; init; }
}
