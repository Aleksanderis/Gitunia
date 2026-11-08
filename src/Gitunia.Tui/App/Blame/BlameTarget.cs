namespace Gitunia.Tui.App.Blame;

/// <summary>The file the blame action stands for, or the reason there is none.</summary>
/// <param name="Path">The file relative to the repository root, null when there is none.</param>
/// <param name="Revision">The commit to blame at, null for the working tree file.</param>
/// <param name="Notice">Why there is no file; empty when there is one.</param>
public sealed record BlameTarget(string? Path, string? Revision, string Notice)
{
    public static BlameTarget Of(string path, string? revision) => new(path, revision, string.Empty);

    public static BlameTarget Refused(string notice) => new(null, null, notice);
}
