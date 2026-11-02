using Gitunia.Core.Jump.Models;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>A list source whose rows the quick jump can label.</summary>
public interface IJumpSource
{
    /// <summary>The typed text whose labels are drawn over the rows, null for none.</summary>
    string? JumpText { get; set; }

    /// <summary>The targets of <paramref name="text"/> among the rows in view, nearest the cursor first.</summary>
    /// <param name="listView">The list that shows the rows.</param>
    /// <param name="text">The typed text.</param>
    IReadOnlyList<JumpLabel> JumpLabels(ListView listView, string text);
}
