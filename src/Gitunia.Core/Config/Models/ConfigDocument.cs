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

    /// <summary>
    /// <c>auto</c> (default), <c>dark</c> or <c>light</c>, read by <c>ThemeConfigurator</c>. Any JSON value is
    /// accepted here so a wrong type costs only this setting.
    /// </summary>
    public JsonElement? Theme { get; init; }

    /// <summary><c>rounded</c> (default) or <c>square</c>, read by <c>OptionsConfigurator</c>.</summary>
    public JsonElement? FrameStyle { get; init; }

    /// <summary>The <c>tabs</c> object (<c>showBranch</c>), read by <c>OptionsConfigurator</c>.</summary>
    public JsonElement? Tabs { get; init; }

    /// <summary>The folders the repository picker scans, read by <c>OptionsConfigurator</c>.</summary>
    public JsonElement? ScanRoots { get; init; }
}
