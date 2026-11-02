using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Group buttons that stay what the test set.</summary>
/// <param name="buttons">The buttons to report.</param>
public sealed class FixedGroupButtons(WorkingTreeButtons buttons) : IGroupButtonSource
{
    public WorkingTreeButtons Buttons() => buttons;
}
