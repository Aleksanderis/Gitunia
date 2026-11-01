namespace Gitunia.Core.DiffPane;

/// <summary>What the views know when a file of the Changes tab is opened in the diff pane.</summary>
/// <param name="Target">The diff the loaded details ask for, null while they are still loading.</param>
/// <param name="IsChangesTab">The detail pane shows the Changes tab.</param>
/// <param name="Cursor">What the Changes list's cursor is on.</param>
public sealed record DiffOpenFacts(DiffTarget? Target, bool IsChangesTab, DiffCursor Cursor);
