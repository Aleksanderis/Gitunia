namespace Gitunia.CustomRenderTui.Demo.Views;

/// <summary>Selection and scroll offset of a vertical list; the same vim-style movement for every pane.</summary>
public sealed class ListCursor
{
    public int Selection { get; private set; }

    public int Top { get; private set; }

    public void Move(int delta, int count) => Selection = Math.Clamp(Selection + delta, 0, Math.Max(0, count - 1));

    public void ToStart() => Selection = 0;

    public void ToEnd(int count) => Selection = Math.Max(0, count - 1);

    public void Set(int selection, int count) => Selection = Math.Clamp(selection, 0, Math.Max(0, count - 1));

    /// <summary>Keeps the selection inside the viewport; call once per frame.</summary>
    public int Scroll(int rows, int count)
    {
        Selection = Math.Clamp(Selection, 0, Math.Max(0, count - 1));
        if (Selection < Top)
        {
            Top = Selection;
        }
        else if (Selection >= Top + rows)
        {
            Top = Selection - rows + 1;
        }

        return Top;
    }
}
