namespace Gitunia.Core.Rebase;

/// <summary>The count wording the rebase and pick texts share.</summary>
internal static class RebaseCounts
{
    /// <summary>"1 file", "2 files".</summary>
    public static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
