using System.Text.Json;

namespace Gitunia.Core.Config.Models;

/// <summary>
/// The config file as written. Unknown properties are ignored, so later sections (theme, options) can
/// be added.
/// </summary>
public sealed record ConfigDocument
{
    /// <summary>Key binding entries, applied in order on top of the default keymap.</summary>
    public List<ConfigKeyEntry?>? Keys { get; init; }

    /// <summary><c>auto</c> (default), <c>dark</c> or <c>light</c>, read by <c>ThemeConfigurator</c>; any JSON value is accepted here so a wrong type costs only this setting.</summary>
    public JsonElement? Theme { get; init; }
}
