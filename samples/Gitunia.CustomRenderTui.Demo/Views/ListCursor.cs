namespace Gitunia.CustomRenderTui.Demo.Views;

/// <summary>Selection and scroll offset of a vertical list; the same vim-style movement for every pane.</summary>
public sealed class ListCursor
{
    public int Sel { get; private set; }

    public int Top { get; private set; }

    public void Move(int delta, int count) => Sel = Math.Clamp(Sel + delta, 0, Math.Max(0, count - 1));

    public void ToStart() => Sel = 0;

    public void ToEnd(int count) => Sel = Math.Max(0, count - 1);

    public void Set(int sel, int count) => Sel = Math.Clamp(sel, 0, Math.Max(0, count - 1));

    /// <summary>Keeps the selection inside the viewport; call once per frame.</summary>
    public int Scroll(int rows, int count)
    {
        Sel = Math.Clamp(Sel, 0, Math.Max(0, count - 1));
        if (Sel < Top)
        {
            Top = Sel;
        }
        else if (Sel >= Top + rows)
        {
            Top = Sel - rows + 1;
        }

        return Top;
    }
}
