namespace Gitunia.Core.Config.Models;

/// <summary>
/// The config file as written. Unknown properties are ignored, so later sections (theme, options) can
/// be added.
/// </summary>
public sealed record ConfigDocument
{
    /// <summary>Key binding entries, applied in order on top of the default keymap.</summary>
    public List<ConfigKeyEntry?>? Keys { get; init; }
}
