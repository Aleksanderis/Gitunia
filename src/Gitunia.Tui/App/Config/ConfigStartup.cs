using Gitunia.App.Status;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Themes;
using Gitunia.Platform;

namespace Gitunia.Tui.App.Config;

/// <summary>
/// Reads the config file at startup and builds what it configures. A bad config never stops the app: the
/// defaults apply for whatever could not be used and every problem is reported in the status history.
/// </summary>
/// <param name="configDirectory">The directory holding <c>config.json</c>.</param>
/// <param name="homeDirectory">What <c>~</c> stands for in a scan root.</param>
/// <param name="defaultLabelCaps">The label caps when the file sets none.</param>
public sealed class ConfigStartup(string configDirectory, string homeDirectory, LabelCapStyle defaultLabelCaps = LabelCapStyle.Rounded)
{
    private const int MaximumReportedProblems = 5;
    private const string ThemesFolder = "themes";

    /// <summary>The label caps while the file sets none: rounded everywhere, the classic console included (the owner wants caps on by default; a font without Powerline glyphs sets <c>labelCaps</c> to none).</summary>
    /// <param name="capabilities">What the platform detected.</param>
    public static LabelCapStyle DefaultLabelCaps(ConsoleCapabilities capabilities) =>
        LabelCapStyle.Rounded;

    /// <summary>Loads the file and applies its key entries to the default keymap. Never throws.</summary>
    public KeymapBuildResult LoadKeymap() => Load().Keymap;

    /// <summary>Loads the file once and builds the keymap, the theme choice and the options. Never throws.</summary>
    public StartupConfig Load()
    {
        var defaults = DefaultKeymap.Create();
        // Startup, before any UI or async context exists: blocking once here is safe and keeps the shell constructor sync.
        var loaded = new ConfigLoader(configDirectory).LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        var built = KeymapConfigurator.Apply(defaults, loaded.Document.Keys);
        var themeProblems = new List<ConfigProblem>();
        var themes = new ThemeCatalog(Path.Combine(configDirectory, ThemesFolder));
        var theme = ThemeConfigurator.Parse(loaded.Document.Theme, themeProblems, themes.Names);
        themeProblems.AddRange(themes.Problems);

        var options = OptionsConfigurator.Read(loaded.Document, homeDirectory, Directory.Exists, themeProblems, defaultLabelCaps);
        var keymap = built with { Problems = [.. loaded.Problems, .. built.Problems, .. themeProblems] };

        return new StartupConfig(keymap, theme, options, themes);
    }

    /// <summary>
    /// Posts the first few problems as warnings, newest last, so the status bar shows the first problem
    /// (with a count of the others) and <c>&lt;space&gt;m</c> shows the others.
    /// </summary>
    /// <param name="problems">The problems found while loading.</param>
    /// <param name="statuses">Where the warnings go.</param>
    public static void Report(IReadOnlyList<ConfigProblem> problems, StatusReporter statuses)
    {
        var posted = Math.Min(problems.Count, MaximumReportedProblems);
        for (var index = posted - 1; index > 0; index--)
        {
            statuses.Warning($"Config: {problems[index]}");
        }

        if (problems.Count > 0)
        {
            var others = problems.Count - 1;
            var more = others > 0 ? $" (and {others} more, see <space>m)" : string.Empty;
            statuses.Warning($"Config: {problems[0]}{more}");
        }
    }
}
