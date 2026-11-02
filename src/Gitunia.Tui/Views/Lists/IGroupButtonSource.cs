using Gitunia.Tui.Rendering.Diff;

namespace Gitunia.Tui.Views.Lists;

/// <summary>Tells which staging buttons the highlighted change group of the Changes view diff carries.</summary>
public interface IGroupButtonSource
{
    /// <summary>The buttons right now; read on every draw, as the side follows the selected file.</summary>
    WorkingTreeButtons Buttons();
}
