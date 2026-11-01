namespace Gitunia.Core.Text;

/// <summary>Wording for counted things in messages.</summary>
public static class CountText
{
    /// <summary>The count with its noun, pluralised with an <c>s</c> unless it is one: <c>1 file</c>, <c>3 files</c>.</summary>
    public static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
