using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Gitunia.Core.Tests.Conventions;

/// <summary>
/// Source-scanning guards for the filter list (docs/internal/ui-consistency.md, sections b, c3 and e): one fuzzy and one
/// contains matcher, and the move keys of a picker live in the keymap, not in a reducer or a controller.
/// </summary>
public sealed partial class FilterListLintTests
{
    private static readonly string[] SourceProjects = ["Gitunia.Core", "Gitunia.App", "Gitunia.Tui"];

    // Folders of the surfaces that show a filter list: their keys come from the keymap (filter.next, filter.previous).
    private static readonly string[] FilterSurfaceFolders =
    [
        "Gitunia.Core/Prompts/", "Gitunia.Core/Reflog/", "Gitunia.Core/Search/", "Gitunia.Core/Overlays/",
        "Gitunia.Tui/Search/", "Gitunia.Tui/App/Prompts/", "Gitunia.Tui/App/Reflog/",
    ];

    // Searches in text (the graph, a diff, a blame), not filters of a list.
    private static readonly string[] TextSearches = ["GraphSearchMatcher.cs", "DiffSearch.cs", "BlameSearch.cs"];

    [GeneratedRegex(@"\bclass\s+(Fuzzy|Contains)Matcher\b")]
    private static partial Regex MatcherClass();

    [GeneratedRegex(@"\.Contains\([^)]*StringComparison\.OrdinalIgnoreCase\)")]
    private static partial Regex CaseInsensitiveContains();

    [GeneratedRegex(@"KeyNames\.(Up|Down|Tab)\b|\bPickerKeys\b")]
    private static partial Regex PickerKey();

    [Fact]
    public void GivenTheSources_WhenScanningForMatchers_ThenThereIsOneFuzzyAndOneContainsMatcher()
    {
        var declared = Lines(_ => true)
            .Where(line => MatcherClass().IsMatch(line.Text))
            .Select(line => line.Where[..line.Where.IndexOf(':', StringComparison.Ordinal)])
            .Order()
            .ToList();

        Assert.Equal(["ContainsMatcher.cs", "FuzzyMatcher.cs"], declared);
    }

    [Fact]
    public void GivenTheSources_WhenScanningForCaseInsensitiveContains_ThenOnlyTheContainsMatcherFiltersAList()
    {
        var offenders = Lines(file => !IsAllowedContains(file))
            .Where(line => CaseInsensitiveContains().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Use ContainsMatcher (or a FilterList): {string.Join("; ", offenders)}");
    }

    [Fact]
    public void GivenTheFilterSurfaces_WhenScanningForMoveKeys_ThenNoPickerKeyIsHardCoded()
    {
        var offenders = Lines(file => FilterSurfaceFolders.Any(folder => file.Replace('\\', '/').Contains(folder)))
            .Where(line => PickerKey().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Bind the key in keymap.default.json instead: {string.Join("; ", offenders)}");
    }

    private static bool IsAllowedContains(string file)
    {
        var name = Path.GetFileName(file);
        var isOtherLayer = file.Contains("Gitunia.Git", StringComparison.Ordinal)
            || file.Contains("Gitunia.Platform", StringComparison.Ordinal)
            || file.Contains("CreationEffectHandler", StringComparison.Ordinal);

        return isOtherLayer || name == "ContainsMatcher.cs" || name == "FuzzyMatcher.cs" || TextSearches.Contains(name);
    }

    private static IEnumerable<SourceLine> Lines(Func<string, bool> fileFilter)
    {
        var root = typeof(FilterListLintTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepoRoot")?.Value ?? string.Empty;
        var separator = Path.DirectorySeparatorChar;
        var generated = new[] { $"{separator}obj{separator}", $"{separator}bin{separator}" };
        foreach (var project in SourceProjects)
        {
            var directory = Path.Combine(root, "src", project);
            Assert.True(Directory.Exists(directory), $"Source folder not found: {directory}");
            var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(file => !generated.Any(file.Contains) && fileFilter(file));
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (!lines[index].TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        yield return new SourceLine($"{Path.GetFileName(file)}:{index + 1}", lines[index]);
                    }
                }
            }
        }
    }

    private sealed record SourceLine(string Where, string Text);
}
