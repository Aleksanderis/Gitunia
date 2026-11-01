namespace Gitunia.Core.Conflicts;

/// <summary>How many rows the panes of the conflict editor show at the current screen size.</summary>
/// <param name="VersionRows">Rows of the base, ours and theirs panes.</param>
/// <param name="ResultRows">Rows of the result pane.</param>
public sealed record ConflictViewport(int VersionRows, int ResultRows);
