using Gitunia.Git;

namespace Gitunia.Core.DiffPane;

/// <summary>What the views and stores know when the diff pane may need to follow the selection.</summary>
/// <param name="GraphCommit">The commit selected in the graph, null when none.</param>
/// <param name="Target">The diff the loaded details ask for, null while they are still loading.</param>
/// <param name="StoreShowsTarget">The diff store shows (or is loading) the target already.</param>
/// <param name="StoreLoaded">The diff store holds a loaded diff.</param>
/// <param name="CurrentFilePath">The file under the diff's cursor, null when none.</param>
/// <param name="Cursor">What the Changes list's cursor is on.</param>
/// <param name="Content">What the Files tab's pane should show for its cursor.</param>
public sealed record DiffFollowFacts(
    ObjectId? GraphCommit,
    DiffTarget? Target,
    bool StoreShowsTarget,
    bool StoreLoaded,
    string? CurrentFilePath,
    DiffCursor Cursor,
    DiffContentTarget Content);
