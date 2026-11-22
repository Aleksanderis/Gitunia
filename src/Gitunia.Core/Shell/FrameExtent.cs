using Gitunia.Core.Operations;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Shell;

/// <summary>The part of the screen the pane layout works in: the screen less the row of the operation banner.</summary>
public static class FrameExtent
{
    /// <summary>The screen as the frame sees it while <paramref name="tab"/> shows an operation banner.</summary>
    /// <param name="screen">The reported screen size, null when it is not known.</param>
    /// <param name="tab">The tab shown, null when none.</param>
    public static ScreenExtent? Of(ScreenExtent? screen, RepositoryState? tab)
    {
        if (screen is null)
        {
            return null;
        }

        var hasBanner = OperationBannerBuilder.IsShown(tab?.Operation);

        return screen with { Height = PaneSizing.FrameHeight(screen.Height, hasBanner) };
    }
}
