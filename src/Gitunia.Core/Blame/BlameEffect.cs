using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Blame;

/// <summary>
/// Read the file and blame it. Posts <see cref="BlameLoaded"/> as soon as the lines are known, then
/// <see cref="BlameProgressed"/> while git finds the commits and <see cref="BlameFinished"/> or
/// <see cref="BlameFailed"/> at the end.
/// </summary>
/// <param name="TabId">The tab the view belongs to.</param>
/// <param name="Location">Where git runs.</param>
/// <param name="Path">The file, relative to the repository root.</param>
/// <param name="Revision">The commit to read the file at, null for the working tree file.</param>
/// <param name="Generation">The read number; the results echo it.</param>
/// <param name="Options">The -w, -M and -C switches, null for none.</param>
public sealed record StartBlameEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Path,
    string? Revision,
    long Generation,
    BlameOptions? Options = null) : AppEffect;

/// <summary>Stop a running blame and free the lines it holds (the view closed or another was asked for).</summary>
/// <param name="Generation">The read number the effect ends; a newer read is left alone.</param>
public sealed record StopBlameEffect(long Generation) : AppEffect;

/// <summary>
/// Copy the selected lines (their code text, no gutter) or characters of the blame view to the clipboard. The lines are
/// in the store, so the handler builds the text.
/// </summary>
/// <param name="Generation">The read number of the lines.</param>
/// <param name="FirstLine">The first selected line.</param>
/// <param name="LastLine">The last selected line.</param>
/// <param name="Chars">The characters of a character selection, null for whole lines.</param>
public sealed record CopyBlameSelectionEffect(long Generation, int FirstLine, int LastLine, TextSelection? Chars)
    : AppEffect;

/// <summary>
/// Look for the next line containing the text. The lines are in the store, so the handler scans them and posts
/// <see cref="BlameMatchFound"/>.
/// </summary>
/// <param name="Generation">The read number of the lines to search.</param>
/// <param name="Query">The text, matched case-insensitively.</param>
/// <param name="Start">The line the search starts from (not included).</param>
/// <param name="Forward">True to search toward the end, false toward the start; both wrap around.</param>
/// <param name="Count">How many matches to step over.</param>
public sealed record FindBlameMatchEffect(long Generation, string Query, int Start, bool Forward, int Count)
    : AppEffect;
