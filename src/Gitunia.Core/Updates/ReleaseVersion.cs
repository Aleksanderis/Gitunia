using System.Globalization;

namespace Gitunia.Core.Updates;

/// <summary>
/// A release version in the <c>major.minor[.patch][-prerelease]</c> form the release tags use (a leading <c>v</c> is
/// accepted; the two-part tags of the first releases, <c>v0.9</c>, read as <c>0.9.0</c>). Ordered as semantic versioning orders it: a prerelease is older than its release, build metadata
/// (<c>+...</c>) is ignored.
/// </summary>
public sealed record ReleaseVersion
{
    private const char TagPrefix = 'v';
    private const char PrereleaseSeparator = '-';
    private const char MetadataSeparator = '+';
    private const char NumberSeparator = '.';
    private const int MinimumNumberCount = 2;
    private const int MaximumNumberCount = 3;

    private ReleaseVersion(int major, int minor, int patch, string prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>The text after the first <c>-</c>, empty for a release.</summary>
    public string Prerelease { get; }

    /// <summary>True for a development build (<c>0.0.0-dev</c>, the version a plain build carries): never a release.</summary>
    public bool IsDevelopmentBuild => Major == 0 && Minor == 0 && Patch == 0 && Prerelease.Length > 0;

    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = new ReleaseVersion(0, 0, 0, string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith(TagPrefix))
        {
            trimmed = trimmed[1..];
        }

        var metadataAt = trimmed.IndexOf(MetadataSeparator);
        if (metadataAt >= 0)
        {
            trimmed = trimmed[..metadataAt];
        }

        var prerelease = string.Empty;
        var prereleaseAt = trimmed.IndexOf(PrereleaseSeparator);
        if (prereleaseAt >= 0)
        {
            prerelease = trimmed[(prereleaseAt + 1)..];
            trimmed = trimmed[..prereleaseAt];
            if (prerelease.Length == 0)
            {
                return false;
            }
        }

        var parts = trimmed.Split(NumberSeparator);
        if (parts.Length is < MinimumNumberCount or > MaximumNumberCount || !TryParseNumbers(parts, out var numbers))
        {
            return false;
        }

        var patch = numbers.Length > MinimumNumberCount ? numbers[2] : 0;
        version = new ReleaseVersion(numbers[0], numbers[1], patch, prerelease);

        return true;
    }

    public bool IsNewerThan(ReleaseVersion other) => CompareTo(other) > 0;

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byNumbers = CompareNumbers(other);
        if (byNumbers != 0)
        {
            return byNumbers;
        }

        // A release outranks any prerelease of the same numbers.
        if (Prerelease.Length == 0 || other.Prerelease.Length == 0)
        {
            return other.Prerelease.Length.CompareTo(Prerelease.Length);
        }

        return ComparePrerelease(Prerelease, other.Prerelease);
    }

    public override string ToString()
    {
        var numbers = $"{Major}{NumberSeparator}{Minor}{NumberSeparator}{Patch}";
        if (Prerelease.Length == 0)
        {
            return numbers;
        }

        return $"{numbers}{PrereleaseSeparator}{Prerelease}";
    }

    private static bool TryParseNumbers(string[] parts, out int[] numbers)
    {
        numbers = new int[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static int ComparePrerelease(string left, string right)
    {
        var leftParts = left.Split(NumberSeparator);
        var rightParts = right.Split(NumberSeparator);
        for (var index = 0; index < Math.Min(leftParts.Length, rightParts.Length); index++)
        {
            var compared = CompareIdentifier(leftParts[index], rightParts[index]);
            if (compared != 0)
            {
                return compared;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumber = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumber = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
        if (leftIsNumber && rightIsNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        // Numeric identifiers rank below alphanumeric ones.
        if (leftIsNumber || rightIsNumber)
        {
            return leftIsNumber ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private int CompareNumbers(ReleaseVersion other)
    {
        var byMajor = Major.CompareTo(other.Major);
        if (byMajor != 0)
        {
            return byMajor;
        }

        var byMinor = Minor.CompareTo(other.Minor);
        if (byMinor != 0)
        {
            return byMinor;
        }

        return Patch.CompareTo(other.Patch);
    }
}
