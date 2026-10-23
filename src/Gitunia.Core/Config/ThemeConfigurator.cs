using System.Text.Json;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes;

namespace Gitunia.Core.Config;

/// <summary>Reads the <c>theme</c> setting of the config file.</summary>
public static class ThemeConfigurator
{
    /// <summary>
    /// The choice for a setting value; an unknown value keeps <see cref="ThemeChoice.Auto"/> and adds a problem.
    /// </summary>
    /// <param name="value">The value of <c>theme</c>, null when absent.</param>
    /// <param name="problems">Receives the problem for a value that is not a known theme.</param>
    /// <param name="themeNames">The names of the theme files that exist; null for the built-in ones only.</param>
    public static ThemeChoice Parse(
        JsonElement? value,
        ICollection<ConfigProblem> problems,
        IReadOnlyCollection<string>? themeNames = null)
    {
        if (value is null or { ValueKind: JsonValueKind.Null })
        {
            return ThemeChoice.Auto;
        }

        if (value.Value.ValueKind != JsonValueKind.String)
        {
            problems.Add(new ConfigProblem("theme", "theme must be a string (auto, dark, light or a theme name), using auto"));
            return ThemeChoice.Auto;
        }

        var text = value.Value.GetString() ?? string.Empty;
        var choice = ThemeChoice.FromText(text);
        var names = themeNames ?? BuiltInThemes.Names;
        if (!choice.IsNamedTheme || names.Contains(choice.Name))
        {
            return choice;
        }

        var message = $"\"{text}\" is not a theme (use auto, dark, light or one of {string.Join(", ", names)}), using auto";
        problems.Add(new ConfigProblem("theme", message));

        return ThemeChoice.Auto;
    }
}
