using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>
/// Every setting the screen offers, as data. The screen, the writer and the docs table are generated from or checked
/// against this list; a later option adds one row here (and its row in the table of <c>docs/ux.md</c>).
/// </summary>
public static class SettingsRegistry
{
    /// <summary>The id of the frame style setting.</summary>
    public const string FrameStyleId = "frame-style";

    /// <summary>The id of the tab branch setting.</summary>
    public const string ShowBranchId = "tabs-show-branch";

    /// <summary>The id of the scan roots setting.</summary>
    public const string ScanRootsId = "scan-roots";

    /// <summary>The id of the row that opens the config file in the editor.</summary>
    public const string OpenConfigId = "open-config";

    /// <summary>The text of the rounded frame style in the file.</summary>
    public const string RoundedStyle = "rounded";

    /// <summary>The text of the square frame style in the file.</summary>
    public const string SquareStyle = "square";

    /// <summary>The settings in the order they are listed inside a category.</summary>
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        new SettingDefinition(
            FrameStyleId,
            SettingCategory.Appearance,
            "Frame style",
            SettingControl.Choice,
            ["frameStyle"],
            "The corners of the pane frames and the overlays. The ASCII character set always draws + - |.",
            RoundedStyle,
            [RoundedStyle, SquareStyle]),
        new SettingDefinition(
            ScanRootsId,
            SettingCategory.Repositories,
            "Scan roots",
            SettingControl.PathList,
            ["scanRoots"],
            "The folders the repository picker scans for repositories. An empty list turns the scan off.",
            "the conventional folders",
            []),
        new SettingDefinition(
            ShowBranchId,
            SettingCategory.Tabs,
            "Show branch in tab name",
            SettingControl.Toggle,
            ["tabs", "showBranch"],
            "Tab chips show the checked-out branch after the name. It makes the tabs long.",
            "off",
            []),
        new SettingDefinition(
            OpenConfigId,
            SettingCategory.ConfigFile,
            "Open config.json in $EDITOR",
            SettingControl.Action,
            [],
            "Opens the file in $VISUAL or $EDITOR. The settings are read again when the editor closes; "
                + "key bindings apply after a restart.",
            string.Empty,
            []),
    ];

    /// <summary>The title of a category in the categories column.</summary>
    /// <param name="category">The category.</param>
    public static string TitleOf(SettingCategory category) => category switch
    {
        SettingCategory.Appearance => "Appearance",
        SettingCategory.Keys => "Keys",
        SettingCategory.Repositories => "Repositories",
        SettingCategory.Git => "Git",
        SettingCategory.Tabs => "Tabs",
        _ => "Config file",
    };
}
