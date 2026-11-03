namespace Gitunia.Core.Editing;

/// <summary>One undone change that <see cref="MessageEditor.Redo"/> can bring back.</summary>
/// <param name="Target">The text and cursor before the undo, which redo returns to.</param>
/// <param name="Expected">
/// The text and cursor right after the undo; redo only applies while the text still equals it.
/// </param>
public sealed record RedoStep(EditorSnapshot Target, EditorSnapshot Expected);
