using System.Collections.Immutable;

namespace Gitunia.Core.Details;

/// <summary>
/// What a tab remembers about its file trees: the filter and the opened directories of the Files tab and the closed
/// folders of the Changes tab's tree style. Small values; the rows are worked out from them and the loaded tree. Not
/// saved with the session.
/// </summary>
/// <param name="Filter">The text a path must contain to be listed, empty for no filter.</param>
/// <param name="IsEditingFilter">The filter box is open (typed keys edit <see cref="Filter"/>).</param>
/// <param name="OpenDirectories">The directories of the Files tab the user opened (kept while the selection moves to
/// other commits, so a path stays open).</param>
/// <param name="ClosedFolders">The folders of the Changes tab's tree style the user closed.</param>
public sealed record DetailTreeState(
    string Filter,
    bool IsEditingFilter,
    ImmutableHashSet<string> OpenDirectories,
    ImmutableHashSet<string> ClosedFolders)
{
    /// <summary>No filter, nothing opened, nothing closed.</summary>
    public static DetailTreeState Empty { get; } = new(
        string.Empty,
        false,
        ImmutableHashSet.Create<string>(StringComparer.Ordinal),
        ImmutableHashSet.Create<string>(StringComparer.Ordinal));
}
