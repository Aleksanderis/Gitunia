using Gitunia.Core.Workspaces;
using Gitunia.Tui.App;

namespace Gitunia.Tui.Tests.App;

/// <summary>A picker controller with the host and pump it runs against.</summary>
/// <param name="Picker">The controller under test.</param>
/// <param name="Host">The workspace host.</param>
/// <param name="Pump">The queue that stands in for the UI thread.</param>
public sealed record PickerHarness(PickerController Picker, WorkspaceHost Host, UiPump Pump);
