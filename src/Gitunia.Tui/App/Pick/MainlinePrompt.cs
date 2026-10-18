namespace Gitunia.Tui.App.Pick;

/// <summary>What the mainline overlay shows: the question and one row per parent of the merge commit.</summary>
/// <param name="Title">The question.</param>
/// <param name="Parents">The parents, first parent first (row n answers parent n).</param>
public sealed record MainlinePrompt(string Title, IReadOnlyList<string> Parents);
