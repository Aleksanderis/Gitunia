using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Settings;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The editors and the settings screen that open over the screen.</summary>
/// <param name="Rebase">The interactive rebase editor.</param>
/// <param name="Conflict">The conflict editor.</param>
/// <param name="Settings">The settings screen.</param>
public sealed record EditorOverlays(RebaseEditorInput Rebase, ConflictEditorInput Conflict, SettingsScreenInput Settings);
