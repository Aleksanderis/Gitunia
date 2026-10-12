using System.Collections.Immutable;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Picker;

/// <summary>Builds the picker list: open, pinned, recent and discovered repositories, filtered and ranked by the fuzzy query, or the directories of a typed path.</summary>
public static class PickerRowBuilder
{
    private const int NameWeight = 2;

    public static ImmutableArray<PickerRow> Build(string query, PickerContext context)
    {
        if (PathQuery.IsPath(query))
        {
            return [.. PathCompleter.Candidates(query, context)];
        }

        var entries = Collect(context);
        if (query.Length == 0)
        {
            return [.. entries.Select(entry => entry.Row)];
        }

        var scored = new List<(PickerRow Row, int Score, int Order)>();
        foreach (var entry in entries)
        {
            var nameScore = FuzzyMatcher.Score(query, entry.Row.Name);
            var pathScore = FuzzyMatcher.Score(query, entry.Row.Path);
            var score = nameScore is { } name ? Math.Max(name * NameWeight, pathScore ?? int.MinValue) : pathScore;
            if (score is { } matched)
            {
                scored.Add((entry.Row, matched, entry.Order));
            }
        }

        return [.. scored.OrderByDescending(item => item.Score).ThenBy(item => item.Order).Select(item => item.Row)];
    }

    // Open tabs first, then pins, recents (most recent first) and discovered repositories; a repository appears once with all its tags.
    private static List<(PickerRow Row, int Order)> Collect(PickerContext context)
    {
        var tagsByPath = new Dictionary<string, PickerTags>(RepositoryPathComparer.Instance);
        var order = new List<string>();
        void Add(string path, PickerTags tag)
        {
            if (tagsByPath.TryGetValue(path, out var existing))
            {
                tagsByPath[path] = existing | tag;
                return;
            }

            tagsByPath[path] = tag;
            order.Add(path);
        }

        foreach (var tab in context.Workspace.Tabs)
        {
            Add(tab.Location.RootPath, PickerTags.Open);
        }

        foreach (var path in context.Workspace.Pins)
        {
            Add(path, PickerTags.Pinned);
        }

        foreach (var path in context.Workspace.Recents)
        {
            Add(path, PickerTags.Recent);
        }

        foreach (var path in context.Discovered)
        {
            Add(path, PickerTags.Discovered);
        }

        // Pinned and open repositories also show their later tags; the stable sort keeps recency inside each group.
        var rows = order
            .Select((path, index) => (Row: new PickerRow(path, NameOf(path), tagsByPath[path]), Order: index))
            .OrderBy(item => Rank(item.Row.Tags))
            .ThenBy(item => item.Order)
            .Select((item, index) => (item.Row, Order: index))
            .ToList();
        return rows;
    }

    private static int Rank(PickerTags tags) =>
        tags.HasFlag(PickerTags.Open) ? 0 : tags.HasFlag(PickerTags.Pinned) ? 1 : tags.HasFlag(PickerTags.Recent) ? 2 : 3;

    private static string NameOf(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        var name = Path.GetFileName(trimmed);
        return name.Length == 0 ? trimmed : name;
    }
}
