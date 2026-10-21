namespace Gitunia.Tui.Rendering.Styling;

/// <summary>A button in a pane's title row: the mouse-map view its click is resolved in, the label and the column it starts at.</summary>
/// <param name="View">The mouse-map view name of the button (for example <c>DiffButton.Wrap</c>).</param>
/// <param name="Label">What the button shows, with its key text when <paramref name="KeyLength"/> is above 0.</param>
/// <param name="Start">The first column of the label within the row.</param>
/// <param name="KeyLength">Length of the key text in the label (one column in), 0 for none.</param>
public sealed record TitleButton(string View, string Label, int Start, int KeyLength = 0)
{
    public int Width => TextWidth.Of(Label);

    /// <summary>The view of the button under <paramref name="column"/>, or null between buttons.</summary>
    public static string? ViewAt(IReadOnlyList<TitleButton> buttons, int column) =>
        buttons.FirstOrDefault(button => column >= button.Start && column < button.Start + button.Width)?.View;
}
