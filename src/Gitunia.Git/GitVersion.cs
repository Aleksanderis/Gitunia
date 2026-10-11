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
        string trimmed = text.Trim();
        int start = trimmed.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        string[] parts = trimmed[(start + Prefix.Length)..].Split(' ')[0].Split('.');
        if (parts.Length < 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor))
        {
            return false;
        }

        int patch = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
        version = new GitVersion(major, minor, patch);
        return true;
    }

    public bool IsAtLeast(GitVersion other) => CompareTo(other) >= 0;

    private int CompareTo(GitVersion other)
    {
        int byMajor = Major.CompareTo(other.Major);
        if (byMajor != 0)
        {
            return byMajor;
        }

        int byMinor = Minor.CompareTo(other.Minor);
        return byMinor != 0 ? byMinor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
