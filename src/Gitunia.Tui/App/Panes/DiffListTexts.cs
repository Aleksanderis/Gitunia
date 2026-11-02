namespace Gitunia.Tui.App.Panes;

/// <summary>The words a diff list puts in its notices.</summary>
/// <param name="SideBySideHint">Appended to the notice when side by side is on but the pane is too narrow.</param>
/// <param name="AnchorName">What the next and previous anchors are called: "hunk", or "change" in the Changes view diff.</param>
public sealed record DiffListTexts(string SideBySideHint, string AnchorName = "hunk");
