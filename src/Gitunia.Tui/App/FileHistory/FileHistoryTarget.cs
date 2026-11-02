namespace Gitunia.Tui.App.FileHistory;

/// <summary>The file whose history an action opens and where the history starts.</summary>
/// <param name="Path">The repository-relative path of the file.</param>
/// <param name="Revision">The shown commit, or <c>HEAD</c> for a file of the Changes view.</param>
public sealed record FileHistoryTarget(string Path, string Revision);
