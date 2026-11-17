using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Filtering;

namespace Gitunia.Core.Search.Models;

/// <summary>
/// The ref jump prompt (<c>&lt;space&gt;f</c>): a <see cref="FilterList{T}"/> of the refs (fuzzy, best match first) behind
/// the commits found by hash prefix, with the typed text as its query.
/// </summary>
/// <param name="List">The candidates, the typed text and which one <c>Enter</c> picks.</param>
public sealed record RefJumpState(FilterList<RefJumpCandidate> List)
{
    /// <summary>The typed text and its caret.</summary>
    public TextField Field => List.Field;

    /// <summary>The typed text.</summary>
    public string Text => List.Text;

    /// <summary>Best match first.</summary>
    public ImmutableArray<RefJumpCandidate> Candidates => List.Candidates;

    /// <summary>The highlighted candidate.</summary>
    public int SelectedIndex => List.Selected;

    /// <summary>The candidate <c>Enter</c> picks, null when nothing matches.</summary>
    public RefJumpCandidate? Selected => List.TryGetSelected(out var selected) ? selected : null;

    /// <summary>Moves the highlight by <paramref name="delta"/> candidates, stopping at the ends.</summary>
    public RefJumpState Move(int delta) => this with { List = List.Move(delta) };
}
