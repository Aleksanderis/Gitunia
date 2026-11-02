using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Blame;

/// <summary>
/// The open blame view: which file, how far the read is and the cursor. The lines and their commits are not here (they
/// grow with the file): they live in the blame store of <c>Gitunia.App</c>, which the view reads by index. Pure.
/// </summary>
/// <param name="TabId">The tab the view was opened in; showing a commit runs in that tab.</param>
/// <param name="Path">The file, relative to the repository root.</param>
/// <param name="Revision">The commit the file is read at, null for the working tree file.</param>
/// <param name="Title">What the title shows: the path and where it is read from.</param>
/// <param name="LineCount">The lines of the file; 0 until the file is read.</param>
/// <param name="BlamedLines">How many lines already know their commit.</param>
/// <param name="IsComplete">True once git finished the blame.</param>
/// <param name="Cursor">The highlighted line (zero-based).</param>
/// <param name="SearchText">The search text, empty when no search is active.</param>
/// <param name="IsEditingSearch">True while the search box takes typed text.</param>
public sealed record BlameState(
    TabId TabId,
    string Path,
    string? Revision,
    string Title,
    int LineCount = 0,
    int BlamedLines = 0,
    bool IsComplete = false,
    int Cursor = 0,
    string SearchText = "",
    bool IsEditingSearch = false)
{
    /// <summary>True when a search is active (being typed or accepted).</summary>
    public bool HasSearch => SearchText.Length > 0 || IsEditingSearch;

    /// <summary>Moves the cursor to <paramref name="index"/>, clamped to the lines.</summary>
    public BlameState MoveTo(int index) =>
        this with { Cursor = LineCount == 0 ? 0 : Math.Clamp(index, 0, LineCount - 1) };

    /// <summary>Starts a fresh search in the box.</summary>
    public BlameState OpenSearch() => this with { SearchText = string.Empty, IsEditingSearch = true };

    /// <summary>Leaves the box and keeps the search active (so <c>n</c> and <c>N</c> work).</summary>
    public BlameState AcceptSearch() => this with { IsEditingSearch = false };

    /// <summary>Drops the search.</summary>
    public BlameState ClearSearch() => this with { SearchText = string.Empty, IsEditingSearch = false };
}
