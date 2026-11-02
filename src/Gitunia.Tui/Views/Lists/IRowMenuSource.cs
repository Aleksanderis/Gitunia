namespace Gitunia.Tui.Views.Lists;

/// <summary>A list source whose rows only sometimes have an actions menu (file rows yes, folder rows no).</summary>
public interface IRowMenuSource
{
    /// <summary>True when the row <paramref name="item"/> has an actions menu, so it shows the <c>...</c> marker when selected.</summary>
    /// <param name="item">The row index.</param>
    bool HasRowMenu(int item);
}
