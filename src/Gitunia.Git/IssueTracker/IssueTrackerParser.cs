using Gitunia.Git.IssueTracker.Models;

namespace Gitunia.Git.IssueTracker;

/// <summary>Turns the output of <c>git config --list -z</c> (with or without <c>--file .issuetracker</c>) into rules.</summary>
public static class IssueTrackerParser
{
    private const string SectionPrefix = "issuetracker.";
    private const string RegexSuffix = ".regex";
    private const string UrlSuffix = ".url";
    private const char EntrySeparator = '\0';
    private const char KeyValueSeparator = '\n';

    /// <summary>
    /// The sections that have both a <c>regex</c> and a <c>url</c>, in order of first appearance; the last value of a
    /// repeated key wins. Other keys and incomplete sections are ignored.
    /// </summary>
    public static IReadOnlyList<IssueTrackerRule> Parse(string output)
    {
        var order = new List<string>();
        var patterns = new Dictionary<string, string>();
        var urls = new Dictionary<string, string>();
        foreach (var entry in output.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var split = entry.IndexOf(KeyValueSeparator, StringComparison.Ordinal);
            if (split < 0 || !entry.StartsWith(SectionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var key = entry[..split];
            var value = entry[(split + 1)..];

            // A section without a subname (`[issuetracker]`) has no name to link the two keys by.
            if (key.Length > SectionPrefix.Length + RegexSuffix.Length
                && key.EndsWith(RegexSuffix, StringComparison.Ordinal))
            {
                Store(key[SectionPrefix.Length..^RegexSuffix.Length], value, patterns, order);
            }
            else if (key.Length > SectionPrefix.Length + UrlSuffix.Length
                && key.EndsWith(UrlSuffix, StringComparison.Ordinal))
            {
                Store(key[SectionPrefix.Length..^UrlSuffix.Length], value, urls, order);
            }
        }

        return
        [
            .. order
                .Where(name => patterns.ContainsKey(name) && urls.ContainsKey(name))
                .Select(name => new IssueTrackerRule(name, patterns[name], urls[name])),
        ];
    }

    private static void Store(string name, string value, Dictionary<string, string> target, List<string> order)
    {
        if (name.Length == 0 || value.Length == 0)
        {
            return;
        }

        if (!order.Contains(name))
        {
            order.Add(name);
        }

        target[name] = value;
    }
}
