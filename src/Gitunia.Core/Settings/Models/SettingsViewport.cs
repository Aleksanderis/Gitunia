namespace Gitunia.Core.Settings.Models;

/// <summary>The room the settings screen has, as the terminal layer measured it.</summary>
/// <param name="PageRows">Rows the lists show, for page motions.</param>
/// <param name="IsCollapsed">The categories collapse into the settings list (a narrow terminal).</param>
public sealed record SettingsViewport(int PageRows, bool IsCollapsed);
