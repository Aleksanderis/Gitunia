namespace Gitunia.Core.Conflicts;

/// <summary>
/// The pane rows of the conflict editor for each set of hint chips it can show. The chips take screen rows below the
/// result, so the pane rows depend on the mode the editor is in; the terminal layer measures all four for the screen
/// and the reducer picks the one that fits the state it computed.
/// </summary>
/// <param name="Versions">The cursor is in a version pane.</param>
/// <param name="Result">The cursor is in the result pane.</param>
/// <param name="Visual">Lines are selected in a version pane.</param>
/// <param name="Editing">The result is typed in.</param>
public sealed record ConflictViewports(
    ConflictViewport Versions,
    ConflictViewport Result,
    ConflictViewport Visual,
    ConflictViewport Editing)
{
    /// <summary>The rows that apply to an editor state.</summary>
    /// <param name="state">The editor.</param>
    public ConflictViewport For(ConflictEditorState state)
    {
        if (state.IsEditing)
        {
            return Editing;
        }

        if (state.IsVisual)
        {
            return Visual;
        }

        return state.IsVersionPane ? Versions : Result;
    }
}
