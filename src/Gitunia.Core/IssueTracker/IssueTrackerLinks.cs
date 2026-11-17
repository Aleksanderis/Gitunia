using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Gitunia.Core.IssueTracker.Models;
using Gitunia.Git.IssueTracker.Models;

namespace Gitunia.Core.IssueTracker;

/// <summary>
/// The compiled rules of a repository's <c>.issuetracker</c> file and git config. Finds the references in a line of text. Uses the
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
    private static readonly TimeSpan DefaultMatchTimeout = TimeSpan.FromMilliseconds(500);

    private readonly List<CompiledRule> _rules = [];

    /// <summary>
    /// Compiles the rules; a rule whose regex does not compile is left out. <paramref name="matchTimeout"/> bounds one
    /// rule on one line (default 500 ms: a cold first match on a slow machine likely stalled past the former 50 ms and
    /// silently disabled the rule, #1416); a rule that exceeds it makes no links at all.
    /// </summary>
    public IssueTrackerLinks(IReadOnlyList<IssueTrackerRule> rules, TimeSpan? matchTimeout = null)
    {
        var timeout = matchTimeout ?? DefaultMatchTimeout;

        foreach (var rule in rules)
        {
            try
            {
                var pattern = new Regex(rule.Pattern, RegexOptions.CultureInvariant, timeout);
                Warm(pattern);
                _rules.Add(new CompiledRule(pattern, rule.UrlTemplate));
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
    /// earlier rule at the same place) is dropped, as is one whose address is not http or https, or one from a rule that
    /// timed out on this line.
    /// </summary>
    public IReadOnlyList<IssueReference> Find(string line)
    {
        if (_rules.Count == 0 || line.Length == 0)
        {
            return [];
        }

        var found = new List<IssueReference>();
        foreach (var rule in _rules.Where(candidate => !candidate.IsDisabled))
        {
            AddMatches(rule, line, found);
        }

        var kept = new List<IssueReference>();

        // A stable sort, so of two matches at one place the earlier rule stays first.
        foreach (var reference in found.OrderBy(candidate => candidate.Start))
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
        // Collected apart and added only when the whole line was scanned: a timeout midway must not leave a rule's
        // partial matches behind, or a later rule would win the places the timed-out rule had not reached yet.
        var matches = new List<IssueReference>();
        try
        {
            foreach (var match in rule.Pattern.Matches(line).Where(match => match.Length > 0))
            {
                var capture = match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : match.Value;
                var url = rule.UrlTemplate.Replace(CaptureToken, Encode(capture), StringComparison.Ordinal);
                if (url.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new IssueReference(match.Index, match.Length, url));
                }
            }

            found.AddRange(matches);
        }
        catch (RegexMatchTimeoutException)
        {
            // A runaway pattern makes no links rather than freezing the pane, now or on the next line.
            rule.IsDisabled = true;
        }
    }

    private static void Warm(Regex pattern)
    {
        // One match on the loading thread, so the first real line is not the cold one on the render path.
        try
        {
            pattern.IsMatch(string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
            // Left to the first real line to disable the rule.
        }
    }

    // Keeps the characters a URL may contain; spaces, quotes and non-ASCII text are percent-encoded.
    private static string Encode(string text)
    {
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var isSafe = rune.IsAscii
                && (char.IsAsciiLetterOrDigit((char)rune.Value) || UrlSafeCharacters.Contains((char)rune.Value));
            if (isSafe)
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

    private sealed class CompiledRule(Regex pattern, string urlTemplate)
    {
        public Regex Pattern { get; } = pattern;

        public string UrlTemplate { get; } = urlTemplate;

        /// <summary>Set after the pattern timed out once: it makes no more links, so it cannot stall again.</summary>
        public bool IsDisabled { get; set; }
    }
}
