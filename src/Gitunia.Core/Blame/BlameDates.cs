using System.Globalization;

namespace Gitunia.Core.Blame;

/// <summary>Short relative dates for the blame gutter (<c>3d ago</c>). Pure: the clock is an argument.</summary>
public static class BlameDates
{
    private const int SecondsPerMinute = 60;
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int DaysPerWeek = 7;
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = 365;

    /// <summary>The text for the time between <paramref name="time"/> and <paramref name="now"/>.</summary>
    /// <param name="time">When the commit was made.</param>
    /// <param name="now">The current time.</param>
    public static string Relative(DateTimeOffset time, DateTimeOffset now)
    {
        var age = now - time;
        if (age < TimeSpan.FromSeconds(SecondsPerMinute))
        {
            return "now";
        }

        if (age.TotalMinutes < MinutesPerHour)
        {
            return Format((int)age.TotalMinutes, "m");
        }

        if (age.TotalHours < HoursPerDay)
        {
            return Format((int)age.TotalHours, "h");
        }

        var days = (int)age.TotalDays;
        if (days < DaysPerWeek)
        {
            return Format(days, "d");
        }

        if (days < DaysPerMonth)
        {
            return Format(days / DaysPerWeek, "w");
        }

        return days < DaysPerYear ? Format(days / DaysPerMonth, "mo") : Format(days / DaysPerYear, "y");
    }

    private static string Format(int amount, string unit) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount}{unit} ago");
}
