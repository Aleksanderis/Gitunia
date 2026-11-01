using Gitunia.App;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Overlays;

namespace Gitunia.Tui.Tests.App;

/// <summary>Workspace actions with the host and picker they act on.</summary>
/// <param name="Actions">The actions under test.</param>
/// <param name="Host">The workspace host.</param>
/// <param name="Picker">The picker controller.</param>
public sealed record WorkspaceActionsHarness(WorkspaceActions Actions, WorkspaceHost Host, PickerController Picker);
