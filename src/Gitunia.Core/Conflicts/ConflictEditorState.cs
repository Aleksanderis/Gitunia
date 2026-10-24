using System.Text;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// The open conflict editor (ux.md, workflow 4, "Conflict editor"): a conflicted file as common text and hunks, the
/// pick made for each hunk and the hunk under the cursor. Pure: it never reads or writes a file;
/// <see cref="ToText"/> is what the save writes. A hunk without a pick keeps its conflict markers in the result, so a
/// partial save leaves the file conflicted exactly where the user did not decide.
/// </summary>
/// <param name="TabId">The tab the editor was opened in; the save runs there.</param>
/// <param name="Path">The conflicted path, relative to the working tree root.</param>
/// <param name="Parts">The file in order: common text and hunks.</param>
/// <param name="Hunks">The hunks of <paramref name="Parts"/> in order.</param>
/// <param name="Picks">The pick of each hunk, parallel to <paramref name="Hunks"/>.</param>
/// <param name="SelectedIndex">The hunk under the cursor.</param>
/// <param name="IsDiscardPending">The first <c>Esc</c> on an edited file was pressed; the next one closes it.</param>
public sealed record ConflictEditorState(
    TabId TabId,
    string Path,
    IReadOnlyList<ConflictPart> Parts,
    IReadOnlyList<ConflictHunk> Hunks,
    IReadOnlyList<ConflictPick> Picks,
    int SelectedIndex = 0,
    bool IsDiscardPending = false)
{
    /// <summary>The state for a freshly read file: nothing is picked and the first hunk is selected.</summary>
    public static ConflictEditorState Create(TabId tabId, string path, IReadOnlyList<ConflictPart> parts)
    {
        var hunks = parts.Where(part => part.Hunk is not null).Select(part => part.Hunk!).ToList();

        var picks = Enumerable.Repeat(ConflictPick.None, hunks.Count).ToList();

        return new ConflictEditorState(tabId, path, parts, hunks, picks);
    }

    /// <summary>The hunk under the cursor, null for a file with none.</summary>
    public ConflictHunk? Selected => Hunks.Count == 0 ? null : Hunks[Index];

    /// <summary>The pick of the hunk under the cursor.</summary>
    public ConflictPick SelectedPick => Hunks.Count == 0 ? ConflictPick.None : Picks[Index];

    /// <summary>Hunks with a pick.</summary>
    public int ResolvedCount => Picks.Count(pick => pick != ConflictPick.None);

    /// <summary>Hunks still without a pick.</summary>
    public int UnresolvedCount => Hunks.Count - ResolvedCount;

    /// <summary>True when every hunk has a pick.</summary>
    public bool IsComplete => UnresolvedCount == 0;

    /// <summary>True when at least one pick was made, which closing would throw away.</summary>
    public bool IsModified => ResolvedCount > 0;

    /// <summary>The cursor clamped to the hunks.</summary>
    public int Index => Hunks.Count == 0 ? 0 : Math.Clamp(SelectedIndex, 0, Hunks.Count - 1);

    /// <summary>Moves to the next hunk, wrapping from the last to the first.</summary>
    public ConflictEditorState Next() => MoveTo(Hunks.Count == 0 ? 0 : (Index + 1) % Hunks.Count);

    /// <summary>Moves to the previous hunk, wrapping from the first to the last.</summary>
    public ConflictEditorState Previous() => MoveTo(Hunks.Count == 0 ? 0 : (Index + Hunks.Count - 1) % Hunks.Count);

    /// <summary>Moves to <paramref name="index"/>, clamped to the hunks.</summary>
    public ConflictEditorState MoveTo(int index) => this with
    {
        SelectedIndex = Hunks.Count == 0 ? 0 : Math.Clamp(index, 0, Hunks.Count - 1),
        IsDiscardPending = false,
    };

    /// <summary>
    /// Takes <paramref name="pick"/> for the hunk under the cursor (<see cref="ConflictPick.None"/> undoes it).
    /// </summary>
    public ConflictEditorState Pick(ConflictPick pick)
    {
        if (Hunks.Count == 0 || Picks[Index] == pick)
        {
            return this;
        }

        var picks = Picks.ToList();
        picks[Index] = pick;

        return this with { Picks = picks, IsDiscardPending = false };
    }

    /// <summary>Remembers that the user asked to close an edited file; the next close really closes.</summary>
    public ConflictEditorState AskDiscard() => this with { IsDiscardPending = true };

    /// <summary>The text hunk <paramref name="index"/> gets in the result: the pick, or its conflict markers.</summary>
    public string ResultOf(int index) => TextFor(Hunks[index], Picks[index]);

    /// <summary>The file as it was read, every hunk with its conflict markers.</summary>
    public string OriginalText => TextOf(Parts);

    /// <summary>The text of parts as they stand in the file: common text and each hunk with its markers.</summary>
    public static string TextOf(IReadOnlyList<ConflictPart> parts) =>
        string.Concat(parts.Select(part => part.Common ?? part.Hunk!.Raw));

    /// <summary>The whole file as it would be saved.</summary>
    public string ToText()
    {
        var builder = new StringBuilder();
        var hunk = 0;
        foreach (var part in Parts)
        {
            builder.Append(part.Common ?? ResultOf(hunk++));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Up to <paramref name="count"/> lines of unconflicted text right before (<paramref name="before"/>) or after the
    /// hunk <paramref name="index"/>; none when another hunk touches it on that side.
    /// </summary>
    public IReadOnlyList<string> ContextAround(int index, bool before, int count)
    {
        var position = PartIndexOf(index);
        var neighbour = before ? position - 1 : position + 1;
        if (count <= 0 || neighbour < 0 || neighbour >= Parts.Count || Parts[neighbour].Common is not { } common)
        {
            return [];
        }

        var lines = Lines(common);

        return before ? [.. lines.Skip(Math.Max(0, lines.Count - count))] : [.. lines.Take(count)];
    }

    /// <summary>Splits text into lines without their terminators; a trailing terminator adds no empty line.</summary>
    public static IReadOnlyList<string> Lines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (text[^1] == '\n')
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string TextFor(ConflictHunk hunk, ConflictPick pick) => pick switch
    {
        ConflictPick.Ours => hunk.Ours,
        ConflictPick.Theirs => hunk.Theirs,
        ConflictPick.Both => $"{hunk.Ours}{hunk.Theirs}",
        _ => hunk.Raw,
    };

    private int PartIndexOf(int hunkIndex)
    {
        var seen = -1;
        for (var position = 0; position < Parts.Count; position++)
        {
            if (Parts[position].Hunk is not null && ++seen == hunkIndex)
            {
                return position;
            }
        }

        return -1;
    }
}
