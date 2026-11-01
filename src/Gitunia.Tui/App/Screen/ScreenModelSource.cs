using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Screen;

namespace Gitunia.Tui.App.Screen;

/// <summary>Builds the model of the whole screen from the state and the facts only the views know.</summary>
/// <param name="parts">What the model is built from.</param>
public sealed class ScreenModelSource(ScreenModelParts parts)
{
    /// <summary>The model of the screen as of now.</summary>
    public ScreenModel Model()
    {
        var host = parts.Host;
        var session = host.Workspace.ActiveTabId is { } id ? parts.Sessions.GetValueOrDefault(id) : null;
        var model = ScreenModelFactory.Create(
            host.Workspace,
            session,
            parts.Keys.Mode,
            parts.Keys.PendingText,
            parts.Messages.Text(),
            parts.Panes.Graph.SelectedItem,
            TabLabelFactory.Create(
                host.Workspace,
                tabId => parts.Sessions.GetValueOrDefault(tabId) is { IsLoading: true, IsPending: false },
                parts.Config.Options.ShowBranchInTab),
            parts.Picker.State,
            parts.Config.HomeDirectory);

        return model with
        {
            RemoteBusy = parts.Messages.IsRemoteBusy(),
            Keys = parts.Panes.Content.Keys,
            SelectedName = FocusedSidebarName(),
            MessageSeverity = parts.Messages.Severity(),
            MessageIsFresh = parts.Messages.IsFresh,
            Hints = parts.Lines.CurrentHints(),
            RepeatPane = parts.Lines.Repeat.Active?.Pane,
            SearchLine = parts.Lines.SearchLine(),
        };
    }

    // The full name of the sidebar row under the cursor while the sidebar has focus (rows show short names).
    private string FocusedSidebarName()
    {
        var source = parts.Panes.Content.SidebarSource;
        if (parts.Host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.Sidebar } tab && !source.Rows.IsEmpty)
        {
            return source.Rows[SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, source.Rows.Length)].FullName;
        }

        return string.Empty;
    }
}
