using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Themes;

namespace Gitunia.Core.Settings;

/// <summary>
/// Every setting the screen offers, as data. The screen, the writer and the docs table are generated from or checked
/// against this list; a later option adds one row here (and its row in the table of <c>docs/ux.md</c>).
/// </summary>
public static class SettingsRegistry
{
    /// <summary>The id of the theme setting.</summary>
    public const string ThemeId = "theme";

    /// <summary>The id of the tab branch setting.</summary>
    public const string ShowBranchId = "tabs-show-branch";

    /// <summary>The id of the scan roots setting.</summary>
    public const string ScanRootsId = "scan-roots";

    /// <summary>The id of the read-only key reference.</summary>
    public const string KeyReferenceId = "key-reference";

    /// <summary>The id of the row that opens the config file in the editor.</summary>
    public const string OpenConfigId = "open-config";

    /// <summary>The settings in the order they are listed inside a category.</summary>
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        new SettingDefinition(
            ThemeId,
            SettingCategory.Appearance,
            "Theme",
            SettingControl.Choice,
            ["theme"],
            "The colours of the interface: auto follows the terminal (Catppuccin Mocha or Latte), "
                + "the rest are theme files. The cursor previews, Enter keeps, Esc restores.",
            ThemeChoice.AutoName,
            [ThemeChoice.AutoName, .. BuiltInThemes.Names]),
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
            KeyReferenceId,
            SettingCategory.Keys,
            "Key reference",
            SettingControl.KeyReference,
            [],
            "Every key of the running keymap, defaults and your keys entries, by view. Read-only: Enter on a key "
                + "shows the config.json entry that binds it again.",
            string.Empty,
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

    /// <summary>The settings with the theme options replaced by auto and the given theme names.</summary>
    /// <param name="themeNames">The names of all theme files on offer (built-in and the user's).</param>
    public static IReadOnlyList<SettingDefinition> WithThemes(IEnumerable<string> themeNames) =>
    [
        .. All.Select(definition => definition.Id == ThemeId
            ? definition with
            {
                Choices = [ThemeChoice.AutoName, .. themeNames],
            }
            : definition),
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
