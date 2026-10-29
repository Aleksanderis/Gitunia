namespace Gitunia.Core.Settings.Models;

/// <summary>
/// One row of the setting registry: what the screen shows, which config key it edits and how it is validated.
/// The screen is generated from these, so docs, screen and config reader cannot drift apart.
/// </summary>
/// <param name="Id">A stable identifier, never shown.</param>
/// <param name="Category">The group it is listed under.</param>
/// <param name="Name">The row label.</param>
/// <param name="Control">How it is edited.</param>
/// <param name="ConfigPath">Property names from the root of <c>config.json</c>; empty for an action row.</param>
/// <param name="Help">What the setting does, one or two sentences.</param>
/// <param name="Default">The default in words, shown in the help line.</param>
/// <param name="Choices">The options of a choice, in the order they are listed.</param>
/// <param name="Minimum">The lowest number of a number setting.</param>
/// <param name="Maximum">The highest number of a number setting.</param>
/// <param name="Step">What <c>+</c> and <c>-</c> add to a number setting.</param>
public sealed record SettingDefinition(
    string Id,
    SettingCategory Category,
    string Name,
    SettingControl Control,
    IReadOnlyList<string> ConfigPath,
    string Help,
    string Default,
    IReadOnlyList<string> Choices,
    int Minimum = 0,
    int Maximum = 0,
    int Step = 1)
{
    /// <summary>The config key in dotted form, for example <c>tabs.showBranch</c>; empty for an action row.</summary>
    public string ConfigKey => string.Join('.', ConfigPath);

    /// <summary>The help line under the lists: where it is written and what applies when it is absent.</summary>
    public string Detail
    {
        get
        {
            if (ConfigPath.Count == 0)
            {
                return string.Empty;
            }

            return $"Written to config.json as \"{ConfigKey}\". Default: {Default}.";
        }
    }
}
