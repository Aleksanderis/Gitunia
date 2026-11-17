using Gitunia.Core.Editing;
using Gitunia.Core.Filtering;
using Gitunia.Core.Text;

namespace Gitunia.Core.Reflog;

/// <summary>
/// The ref picker of the reflog list: a filtered list of <c>HEAD</c> and the local branches, so the reflog of any
/// branch can be opened without leaving the list. A <see cref="FilterList{T}"/> that keeps the list's own order
/// (<see cref="MatcherKind.Contains"/>). Pure.
/// </summary>
/// <param name="List">The names, the typed filter and the highlighted row.</param>
public sealed record ReflogRefPicker(FilterList<string> List)
{
    /// <summary>The subject of the no-match text.</summary>
    public const string Subject = "branch";

    /// <summary>A picker over <paramref name="names"/>, <c>HEAD</c> first, then the local branches.</summary>
    public ReflogRefPicker(IReadOnlyList<string> names, TextField? field = null)
        : this(FilterLists.Of(
            [.. names.Select(name => FilterItems.Of(name, name))],
            MatcherKind.Contains,
            UiTexts.NoMatch(Subject),
            field))
    {
    }

    /// <summary>The typed filter and its caret.</summary>
    public TextField Field => List.Field;

    /// <summary>The typed filter.</summary>
    public string Text => List.Text;

    /// <summary>The index of the highlighted choice in <see cref="Candidates"/>.</summary>
    public int Index => List.Selected;

    /// <summary>The choices that match the typed text, in their original order.</summary>
    public IReadOnlyList<string> Candidates => List.Candidates;

    /// <summary>The highlighted choice, null when nothing matches.</summary>
    public string? Selected => List.TryGetSelected(out var selected) ? selected : null;

    /// <summary>The picker after an edit of the filter; a move of the caret alone keeps the highlight.</summary>
    public ReflogRefPicker WithEdit(TextField edit) => this with { List = List.WithField(edit) };

    /// <summary>Moves the highlight by <paramref name="step"/>, stopping at the ends.</summary>
    public ReflogRefPicker Move(int step) => this with { List = List.Move(step) };

    /// <summary>Highlights the choice at <paramref name="index"/> of <see cref="Candidates"/>, clamped.</summary>
    public ReflogRefPicker MoveTo(int index) => this with { List = List.MoveTo(index) };
}
