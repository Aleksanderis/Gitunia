using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Gitunia.Core.IssueTracker.Models;
using Gitunia.Git.IssueTracker.Models;

namespace Gitunia.Core.IssueTracker;

/// <summary>
/// The compiled rules of a repository's <c>.issuetracker</c> file. Finds the references in a line of text. Uses the
/// interpreting regex engine (no runtime code generation), so it works in the Native AOT binary.
/// </summary>
public sealed class IssueTrackerLinks
{
    /// <summary>No rules: nothing is a link.</summary>
    public static IssueTrackerLinks None { get; } = new([]);

    private const string CaptureToken = "$1";
    private const string HttpPrefix = "http://";
    private const string HttpsPrefix = "https://";
    private const string UrlSafeCharacters = "-._~/:@!$&'()*+,;=?#%";
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

    private readonly List<CompiledRule> _rules = [];

    /// <summary>Compiles the rules; a rule whose regex does not compile is left out.</summary>
    public IssueTrackerLinks(IReadOnlyList<IssueTrackerRule> rules)
    {
        foreach (var rule in rules)
        {
            try
            {
                _rules.Add(new CompiledRule(new Regex(rule.Pattern, RegexOptions.CultureInvariant, MatchTimeout), rule.UrlTemplate));
            }
            catch (ArgumentException)
            {
                // An invalid regex is harmless: the rule just makes no links.
            }
        }
    }

    public bool IsEmpty => _rules.Count == 0;

    /// <summary>
    /// The references in <paramref name="line"/>, left to right. A match overlapping an earlier one (or one from an
    /// earlier rule at the same place) is dropped, as is one whose address is not http or https.
    /// </summary>
    public IReadOnlyList<IssueReference> Find(string line)
    {
        if (_rules.Count == 0 || line.Length == 0)
        {
            return [];
        }

        var found = new List<IssueReference>();
        foreach (var rule in _rules)
        {
            AddMatches(rule, line, found);
        }

        found.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        var kept = new List<IssueReference>();
        foreach (var reference in found)
        {
            if (kept.Count == 0 || reference.Start >= kept[^1].Start + kept[^1].Length)
            {
                kept.Add(reference);
            }
        }

        return kept;
    }

    private static void AddMatches(CompiledRule rule, string line, List<IssueReference> found)
    {
        try
        {
            foreach (var match in rule.Pattern.Matches(line).Where(match => match.Length > 0))
            {
                var capture = match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : match.Value;
                var url = rule.UrlTemplate.Replace(CaptureToken, Encode(capture), StringComparison.Ordinal);
                if (url.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(new IssueReference(match.Index, match.Length, url));
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // A runaway pattern makes no links rather than freezing the pane.
        }
    }

    // Keeps the characters a URL may contain; spaces, quotes and non-ASCII text are percent-encoded.
    private static string Encode(string text)
    {
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.IsAscii && (char.IsAsciiLetterOrDigit((char)rune.Value) || UrlSafeCharacters.Contains((char)rune.Value)))
            {
                builder.Append((char)rune.Value);
                continue;
            }

            foreach (var value in Encoding.UTF8.GetBytes(rune.ToString()))
            {
                builder.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private sealed record CompiledRule(Regex Pattern, string UrlTemplate);
}
