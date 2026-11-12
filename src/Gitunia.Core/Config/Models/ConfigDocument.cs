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

    /// <summary>The <c>tabs</c> object (<c>showBranch</c>), read by <c>OptionsConfigurator</c>.</summary>
    public JsonElement? Tabs { get; init; }

    /// <summary>The folders the repository picker scans, read by <c>OptionsConfigurator</c>.</summary>
    public JsonElement? ScanRoots { get; init; }

    /// <summary>
    /// <c>true</c> turns on live file watching (off by default: tabs poll), read by <c>OptionsConfigurator</c>.
    /// </summary>
    public JsonElement? WatchFiles { get; init; }

    /// <summary>
    /// <c>none</c> (default), <c>rounded</c> or <c>angled</c>: the ends of the branch label chips, read by
    /// <c>OptionsConfigurator</c>.
    /// </summary>
    public JsonElement? LabelCaps { get; init; }

    /// <summary>
    /// <c>light</c> (default) or <c>heavy</c>: how the graph lines are drawn, read by
    /// <c>OptionsConfigurator</c>.
    /// </summary>
    public JsonElement? LaneStyle { get; init; }

    /// <summary>The <c>hints</c> object (<c>whichKey</c>, <c>statusBar</c>), read by <c>OptionsConfigurator</c>.</summary>
    public JsonElement? Hints { get; init; }

    /// <summary>
    /// <c>everyday</c> (default), <c>advanced</c> or <c>advanced+</c>: the command level (#839), read by
    /// <c>OptionsConfigurator</c>.
    /// </summary>
    public JsonElement? CommandLevel { get; init; }

    /// <summary>
    /// <c>true</c> (default) copies a mouse selection to the clipboard when it is made, read by
    /// <c>OptionsConfigurator</c>.
    /// </summary>
    public JsonElement? CopyOnSelect { get; init; }

    /// <summary>
    /// <c>true</c> (default) lets the app look for a newer release once a day and say so in the status bar, read by
    /// <c>OptionsConfigurator</c>.
    /// </summary>
    public JsonElement? UpdateCheck { get; init; }
}
