using System.Text.Json;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>Reads the <c>theme</c> setting of the config file.</summary>
public static class ThemeConfigurator
{
    /// <summary>
    /// The choice for a setting value; an unknown value keeps <see cref="ThemeChoice.Auto"/> and adds a problem.
    /// </summary>
    /// <param name="value">The value of <c>theme</c>, null when absent.</param>
    /// <param name="problems">Receives the problem for a value that is not <c>auto</c>, <c>dark</c> or <c>light</c>.</param>
    public static ThemeChoice Parse(JsonElement? value, ICollection<ConfigProblem> problems)
    {
        if (value is null or { ValueKind: JsonValueKind.Null })
        {
            return ThemeChoice.Auto;
        }

        if (value.Value.ValueKind != JsonValueKind.String)
        {
            problems.Add(new ConfigProblem("theme", "theme must be a string (auto, dark or light), using auto"));
            return ThemeChoice.Auto;
        }

        var text = value.Value.GetString() ?? string.Empty;
        switch (text.Trim().ToLowerInvariant())
        {
            case "auto":
                return ThemeChoice.Auto;
            case "dark":
                return ThemeChoice.Dark;
            case "light":
                return ThemeChoice.Light;
            default:
                var message = $"\"{text}\" is not a theme (use auto, dark or light), using auto";
                problems.Add(new ConfigProblem("theme", message));
                return ThemeChoice.Auto;
        }
    }
}
