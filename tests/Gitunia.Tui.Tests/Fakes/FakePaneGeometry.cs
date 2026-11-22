using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>A pane geometry on a fixed screen, with or without an operation banner row and the Changes view's unstaged list focused.</summary>
public static class FakePaneGeometry
{
    public static PaneGeometry Of(IPaneChrome chrome, bool hasBanner = false) =>
        new(chrome, new FixedArrangement(hasBanner), new FixedFocus());

    private sealed class FixedArrangement(bool hasBanner) : IPaneArrangement
    {
        public PaneArrangement Arrangement() => PaneArrangement.Normal;

        public bool HasBanner() => hasBanner;
    }

    private sealed class FixedFocus : IWorkingTreeFocus
    {
        public PaneKind FocusedPane() => PaneKind.WorkingUnstaged;

        public string DiffView() => "WorkingUnstagedDiff";

        public bool IsDiffZoomed() => false;

        public int? ListWidth() => null;

        public WorkingTreeButtons DiffButtons() => WorkingTreeButtons.None;
    }
}
