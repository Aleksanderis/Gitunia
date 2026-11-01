namespace Gitunia.Git;

/// <summary>The abbreviated commit hash shown to the user: a fixed length, git's default abbreviation.</summary>
public static class ShortHash
{
    public const int Length = 7;

    /// <summary>
    /// The first <see cref="Length"/> characters of <paramref name="hash"/>, all of a shorter one, or an empty string
    /// for none.
    /// </summary>
    public static string Of(string? hash)
    {
        if (hash is null)
        {
            return string.Empty;
        }

        return hash.Length > Length ? hash[..Length] : hash;
    }
}
