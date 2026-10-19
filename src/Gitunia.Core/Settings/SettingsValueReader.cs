using System.Collections.Immutable;
using System.Text.Json;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings.Models;

namespace Gitunia.Core.Settings;

/// <summary>
/// Turns the text of <c>config.json</c> into the values the settings screen shows, the options the running app uses and
/// the reasons a value in the file is not valid. Pure: the folder check is injected.
/// </summary>
public static class SettingsValueReader
{
    private const string NotEditable = "config.json cannot be edited";

    /// <summary>Reads the settings from a snapshot of the file.</summary>
    /// <param name="snapshot">The file as read by the config writer.</param>
    /// <param name="homeDirectory">What a leading <c>~</c> in a scan root stands for.</param>
    /// <param name="directoryExists">Whether a folder exists (injected so this stays pure).</param>
    public static SettingsLoad Read(ConfigSnapshot snapshot, string homeDirectory, Func<string, bool> directoryExists)
    {
        var problems = new List<ConfigProblem>();
        var banner = snapshot.Error is { } error ? $"{NotEditable}: {error}" : string.Empty;
        var document = new ConfigDocument();
        if (banner.Length == 0 && snapshot.Text.Trim().Length > 0)
        {
            document = ConfigLoader.Parse(snapshot.Text, problems);
            if (problems.Count > 0)
            {
                banner = $"{NotEditable}: {problems[0]}";
                problems.Clear();
            }
        }

        var theme = ThemeConfigurator.Parse(document.Theme, problems);
        var options = OptionsConfigurator.Read(document, homeDirectory, directoryExists, problems);
        var values = ImmutableDictionary.CreateBuilder<string, SettingValue>();
        values[SettingsRegistry.ThemeId] = SettingValue.OfChoice(ThemeName(theme));
        values[SettingsRegistry.FrameStyleId] = SettingValue.OfChoice(StyleName(options.FrameStyle));
        values[SettingsRegistry.ShowBranchId] = SettingValue.OfFlag(options.ShowBranchInTab);
        values[SettingsRegistry.ScanRootsId] = ReadScanRoots(document.ScanRoots);

        return new SettingsLoad(values.ToImmutable(), WarningsOf(problems), banner, options, theme);
    }

    /// <summary>The text of a frame style as written in the file.</summary>
    /// <param name="style">The style.</param>
    public static string StyleName(FrameStyle style) =>
        style == FrameStyle.Square ? SettingsRegistry.SquareStyle : SettingsRegistry.RoundedStyle;

    /// <summary>The text of a theme as written in the file.</summary>
    /// <param name="theme">The theme.</param>
    public static string ThemeName(ThemeChoice theme) => theme switch
    {
        ThemeChoice.Dark => "dark",
        ThemeChoice.Light => "light",
        _ => "auto",
    };

    /// <summary>The theme a choice option stands for; auto for an unknown text.</summary>
    /// <param name="text">The option text.</param>
    public static ThemeChoice ThemeOf(string text) => text switch
    {
        "dark" => ThemeChoice.Dark,
        "light" => ThemeChoice.Light,
        _ => ThemeChoice.Auto,
    };

    // The list as written, so editing it never rewrites an entry the user typed (a leading ~ stays one).
    private static SettingValue ReadScanRoots(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return SettingValue.OfPaths([], isDefault: true);
        }

        var entries = array.EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetString() ?? string.Empty);

        return SettingValue.OfPaths(entries);
    }

    private static ImmutableDictionary<string, string> WarningsOf(IReadOnlyList<ConfigProblem> problems)
    {
        var warnings = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (var definition in SettingsRegistry.All)
        {
            var match = problems.FirstOrDefault(problem => Concerns(problem.Path, definition.ConfigKey));
            if (match is not null)
            {
                warnings[definition.Id] = match.Message;
            }
        }

        return warnings.ToImmutable();
    }

    // The problem path is the key, a parent of it (tabs for tabs.showBranch) or an entry of it (scanRoots[2]).
    private static bool Concerns(string problemPath, string key)
    {
        if (problemPath.Length == 0 || key.Length == 0)
        {
            return false;
        }

        return problemPath == key
            || key.StartsWith($"{problemPath}.", StringComparison.Ordinal)
            || problemPath.StartsWith($"{key}[", StringComparison.Ordinal)
            || problemPath.StartsWith($"{key}.", StringComparison.Ordinal);
    }
}
