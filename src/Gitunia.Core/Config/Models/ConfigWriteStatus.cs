namespace Gitunia.Core.Config.Models;

/// <summary>How a config write ended.</summary>
public enum ConfigWriteStatus
{
    /// <summary>The file was written.</summary>
    Written,

    /// <summary>The file changed since it was read; nothing was written.</summary>
    Stale,

    /// <summary>The file cannot be edited (does not parse, parent not an object); nothing was written.</summary>
    Refused,

    /// <summary>The file could not be read or written; nothing was changed.</summary>
    Failed,
}
