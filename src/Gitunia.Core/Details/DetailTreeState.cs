using System.Collections.Immutable;
using Gitunia.Core.Editing;

namespace Gitunia.Core.Details;

/// <summary>
/// What a tab remembers about its file trees: the filter and the opened directories of the Files tab and the closed
/// folders of the Changes tab's tree style. Small values; the rows are worked out from them and the loaded tree. Not
/// saved with the session.
/// </summary>
/// <param name="Field">The text a path must contain to be listed (empty for no filter) and its caret while edited.</param>
/// <param name="IsEditingFilter">The filter box is open (typed keys edit <see cref="Field"/>).</param>
/// <param name="OpenDirectories">The directories of the Files tab the user opened (kept while the selection moves to
/// other commits, so a path stays open).</param>
/// <param name="ClosedFolders">The folders of the Changes tab's tree style the user closed.</param>
public sealed record DetailTreeState(
    TextField Field,
    bool IsEditingFilter,
    ImmutableHashSet<string> OpenDirectories,
    ImmutableHashSet<string> ClosedFolders)
{
    /// <summary>The text a path must contain to be listed, empty for no filter.</summary>
    public string Filter => Field.Text;

    /// <summary>No filter, nothing opened, nothing closed.</summary>
    public static DetailTreeState Empty { get; } = new(
        string.Empty,
        false,
        ImmutableHashSet.Create<string>(StringComparer.Ordinal),
        ImmutableHashSet.Create<string>(StringComparer.Ordinal));
}
