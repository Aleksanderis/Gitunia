using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Status;

namespace Gitunia.Tui.App.Config;

/// <summary>
/// Reads the config file at startup and builds what it configures. A bad config never stops the app: the
/// defaults apply for whatever could not be used and every problem is reported in the status history.
/// </summary>
/// <param name="configDirectory">The directory holding <c>config.json</c>.</param>
public sealed class ConfigStartup(string configDirectory)
{
    private const int MaximumReportedProblems = 5;

    /// <summary>Loads the file and applies its key entries to the default keymap. Never throws.</summary>
    public KeymapBuildResult LoadKeymap()
    {
        var defaults = DefaultKeymap.Create();
        var loaded = new ConfigLoader(configDirectory).LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        var built = KeymapConfigurator.Apply(defaults, loaded.Document.Keys);

        return built with { Problems = [.. loaded.Problems, .. built.Problems] };
    }

    /// <summary>
    /// Posts the first few problems as warnings, newest last, so the status bar shows the first problem
    /// (with a count of the others) and <c>&lt;space&gt;m</c> shows the others.
    /// </summary>
    /// <param name="problems">The problems found while loading.</param>
    /// <param name="statuses">Where the warnings go.</param>
    public static void Report(IReadOnlyList<ConfigProblem> problems, StatusChannel statuses)
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
