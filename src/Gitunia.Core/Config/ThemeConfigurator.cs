using System.Text.Json;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>Reads the <c>theme</c> setting of the config file.</summary>
public static class ThemeConfigurator
{
    /// <summary>The choice for a setting text; an unknown text keeps <see cref="ThemeChoice.Auto"/> and adds a problem.</summary>
    /// <param name="value">The value of <c>theme</c>, null when absent.</param>
    /// <param name="problems">Receives the problem for a text that is not <c>auto</c>, <c>dark</c> or <c>light</c>.</param>
    public static ThemeChoice Parse(JsonElement? value, ICollection<ConfigProblem> problems)
    {
        var text = value is { ValueKind: JsonValueKind.String } element ? element.GetString() : value?.ToString();
        if (value is { ValueKind: JsonValueKind.Null })
        {
            return ThemeChoice.Auto;
        }

        switch (text?.Trim().ToLowerInvariant())
        {
            case null:
            case "auto":
                return ThemeChoice.Auto;
            case "dark":
                return ThemeChoice.Dark;
            case "light":
                return ThemeChoice.Light;
            default:
                problems.Add(new ConfigProblem("theme", $"{text} is not a theme (use auto, dark or light), using auto"));
                return ThemeChoice.Auto;
        }
    }
}
