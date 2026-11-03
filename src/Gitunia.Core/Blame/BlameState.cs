using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Blame.Models;

namespace Gitunia.Core.Blame;

/// <summary>
/// The open blame view: which file, how far the read is and the cursor. The lines and their commits are not here (they
/// grow with the file): they live in the blame store of <c>Gitunia.App</c>, which the view reads by index. Pure.
/// </summary>
/// <param name="TabId">The tab the view was opened in; showing a commit runs in that tab.</param>
/// <param name="Path">The file, relative to the repository root.</param>
/// <param name="Title">What the title shows: the path and where it is read from.</param>
/// <param name="LineCount">The lines of the file; 0 until the file is read.</param>
/// <param name="BlamedLines">How many lines already know their commit.</param>
/// <param name="IsComplete">True once git finished the blame.</param>
/// <param name="Cursor">The highlighted line (zero-based).</param>
/// <param name="SearchText">The search text, empty when no search is active.</param>
/// <param name="SearchCaret">The caret column in <see cref="SearchText"/> (see <see cref="PromptCaret"/>).</param>
/// <param name="IsEditingSearch">True while the search box takes typed text.</param>
/// <param name="Revision">The revision blamed, null for the working tree file; <c>&lt;id&gt;^</c> after a re-blame at a
/// parent.</param>
/// <param name="Options">The -w, -M and -C switches, null for none.</param>
/// <param name="IgnoreRevsFile">The value of <c>blame.ignoreRevsFile</c> when git uses one, else null.</param>
/// <param name="Back">The earlier blames; the last one is where Esc goes back to.</param>
/// <param name="Selection">The selected lines or characters, null when none; the cursor is its moving end.</param>
public sealed record BlameState(
    TabId TabId,
    string Path,
    string Title,
    int LineCount = 0,
    int BlamedLines = 0,
    bool IsComplete = false,
    int Cursor = 0,
    string SearchText = "",
    bool IsEditingSearch = false,
    string? Revision = null,
    BlameOptions? Options = null,
    string? IgnoreRevsFile = null,
    ImmutableArray<BlameStep> Back = default,
    int SearchCaret = PromptCaret.End,
    BlameSelection? Selection = null)
{
    /// <summary>The switches in force (none when not set).</summary>
    public BlameOptions ActiveOptions => Options ?? BlameOptions.None;

    /// <summary>The earlier blames, empty when the view shows the first.</summary>
    public ImmutableArray<BlameStep> Steps => Back.IsDefault ? [] : Back;

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
