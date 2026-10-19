using Gitunia.Core.Keymap;

namespace Gitunia.Core.Config.Models;

/// <summary>The keymap after applying the user's entries, and the entries that were refused.</summary>
/// <param name="Table">Defaults plus every accepted entry; never has conflicts the defaults did not have.</param>
/// <param name="Problems">One per refused entry.</param>
public sealed record KeymapBuildResult(KeyBindingTable Table, IReadOnlyList<ConfigProblem> Problems);
