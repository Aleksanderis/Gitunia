namespace Gitunia.Core.Config.Models;

/// <summary>What <see cref="ConfigKeyLocator.Locate"/> found for a path.</summary>
public enum ConfigLocationKind
{
    /// <summary>The value exists; its span is set.</summary>
    Found,

    /// <summary>The text is valid but has no value at the path. The container is set when a member can be added.</summary>
    Absent,

    /// <summary>The text is blank or holds only comments.</summary>
    Empty,

    /// <summary>The text is not valid config JSON (comments and trailing commas allowed); the line is set.</summary>
    Invalid,

    /// <summary>A value on the way to the path exists but is not an object (for example <c>"tabs": 5</c>).</summary>
    ParentNotObject,
}
