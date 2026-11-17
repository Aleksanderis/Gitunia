using System.Globalization;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Text;

/// <summary>
/// How values are written on every screen (docs/internal/ui-consistency.md, decision 6): one absolute date format
/// for lists, one with the zone for the detail header, one relative format, one status letter per change, one
/// rename text and one ahead and behind text. The abbreviated hash is <see cref="ShortHash"/>.
/// </summary>
public static class ValueFormat
{
    /// <summary>Absolute date of a narrow list column: <c>2026-11-17</c>.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>Absolute date and time of lists (graph when wide, file history): <c>2026-11-17 09:30</c>.</summary>
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Absolute date, time and zone of the detail header: <c>2026-11-17 09:30 +01:00</c>.</summary>
    public const string DateTimeZoneFormat = "yyyy-MM-dd HH:mm zzz";

    /// <summary>The text between the old and the new path of a rename: <c>old -&gt; new</c>.</summary>
    public const string RenameArrow = " -> ";

    /// <summary>The compact rename suffix of a path row that already shows the new path: <c>&lt;- old</c>.</summary>
    public const string RenamedFromArrow = " <- ";

    private const int SecondsPerMinute = 60;
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int DaysPerWeek = 7;
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = 365;

    /// <summary>The date of a list column that has room for the date only.</summary>
    public static string Date(DateTimeOffset time) => time.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>The date and time of a list.</summary>
    public static string DateTime(DateTimeOffset time) => time.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>The date, time and zone of the detail header.</summary>
    public static string DateTimeZone(DateTimeOffset time) =>
        time.ToString(DateTimeZoneFormat, CultureInfo.InvariantCulture);

    /// <summary>The time between <paramref name="time"/> and <paramref name="now"/> in short words: <c>3d ago</c>.</summary>
    /// <param name="time">When it happened.</param>
    /// <param name="now">The current time (an argument, so the text is pure).</param>
    public static string Relative(DateTimeOffset time, DateTimeOffset now)
    {
        var age = now - time;
        if (age < TimeSpan.FromSeconds(SecondsPerMinute))
        {
            return "now";
        }

        if (age.TotalMinutes < MinutesPerHour)
        {
            return Ago((int)age.TotalMinutes, "m");
        }

        if (age.TotalHours < HoursPerDay)
        {
            return Ago((int)age.TotalHours, "h");
        }

        var days = (int)age.TotalDays;
        if (days < DaysPerWeek)
        {
            return Ago(days, "d");
        }

        if (days < DaysPerMonth)
        {
            return Ago(days / DaysPerWeek, "w");
        }

        return days < DaysPerYear ? Ago(days / DaysPerMonth, "mo") : Ago(days / DaysPerYear, "y");
    }

    /// <summary>The letter of a change: <c>A</c>, <c>D</c>, <c>R</c>, <c>C</c>, <c>T</c>, <c>U</c>, else <c>M</c>.</summary>
    public static string StatusLetter(FileChangeKind kind) => kind switch
    {
        FileChangeKind.Added => FileChangeLetters.Added.ToString(),
        FileChangeKind.Deleted => FileChangeLetters.Deleted.ToString(),
        FileChangeKind.Renamed => FileChangeLetters.Renamed.ToString(),
        FileChangeKind.Copied => FileChangeLetters.Copied.ToString(),
        FileChangeKind.TypeChanged => FileChangeLetters.TypeChanged.ToString(),
        FileChangeKind.UpdatedButUnmerged => FileChangeLetters.Unmerged.ToString(),
        _ => FileChangeLetters.Modified.ToString(),
    };

    /// <summary>A rename as text, old path first: <c>old.txt -&gt; new.txt</c>.</summary>
    public static string RenameText(string oldPath, string newPath) => $"{oldPath}{RenameArrow}{newPath}";

    /// <summary>
    /// The commits a branch is ahead of and behind its upstream, each with its glyph and left out when zero:
    /// <c>↑2↓1</c>. Empty when both are zero or unknown.
    /// </summary>
    public static string AheadBehind(int? ahead, int? behind, string aheadGlyph, string behindGlyph)
    {
        var aheadText = ahead is > 0 ? $"{aheadGlyph}{ahead.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        var behindText = behind is > 0
            ? $"{behindGlyph}{behind.Value.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;

        return aheadText + behindText;
    }

    private static string Ago(int amount, string unit) => string.Create(CultureInfo.InvariantCulture, $"{amount}{unit} ago");
}
