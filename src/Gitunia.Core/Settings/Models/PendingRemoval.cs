namespace Gitunia.Core.Settings.Models;

/// <summary>A folder of a path list waiting for the answer to the question before it is removed.</summary>
/// <param name="SettingId">The path list.</param>
/// <param name="ItemIndex">The folder's position in the list.</param>
/// <param name="Path">The folder as written in the file.</param>
public sealed record PendingRemoval(string SettingId, int ItemIndex, string Path);
