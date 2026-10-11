using System.Globalization;

namespace Gitunia.Git;

/// <summary>A git release number; vendor suffixes such as <c>.windows.1</c> are ignored.</summary>
public readonly record struct GitVersion(int Major, int Minor, int Patch)
{
    /// <summary>Oldest supported release; see docs/git-layer.md.</summary>
    public static readonly GitVersion Minimum = new(2, 43, 0);

    /// <summary>First release with relative worktree paths.</summary>
    public static readonly GitVersion RelativeWorktrees = new(2, 48, 0);

    /// <summary>Parses the output of <c>git --version</c> (for example <c>git version 2.43.0.windows.1</c>).</summary>
    public static bool TryParse(string text, out GitVersion version)
    {
        version = default;
        const string Prefix = "git version ";
        var trimmed = text.Trim();
        var start = trimmed.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var parts = trimmed[(start + Prefix.Length)..].Split(' ')[0].Split('.');
        if (parts.Length < 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        var patch = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        version = new GitVersion(major, minor, patch);
        return true;
    }

    public bool IsAtLeast(GitVersion other) => CompareTo(other) >= 0;

    private int CompareTo(GitVersion other)
    {
        var byMajor = Major.CompareTo(other.Major);
        if (byMajor != 0)
        {
            return byMajor;
        }

        var byMinor = Minor.CompareTo(other.Minor);
        return byMinor != 0 ? byMinor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
