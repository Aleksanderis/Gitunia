using System.Collections.Immutable;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Picker;

/// <summary>
/// Everything the picker needs from outside: the workspace, scan results, the home and base directories for paths and the
/// file system questions. Injected so the picker logic is pure and never reads the process working directory.
/// </summary>
/// <param name="Workspace">Open tabs, recents and pins.</param>
/// <param name="Discovered">Repository root paths found by the scanner so far.</param>
/// <param name="HomeDirectory">What <c>~</c> expands to.</param>
/// <param name="BaseDirectory">What a relative path (<c>./</c>, <c>../</c>) is resolved against: the active repository, or the home directory.</param>
/// <param name="ListDirectories">The full paths of the subdirectories of a directory (empty when it cannot be read).</param>
/// <param name="IsRepository">Whether a directory has a <c>.git</c> entry.</param>
public sealed record PickerContext(
    Workspace Workspace,
    ImmutableArray<string> Discovered,
    string HomeDirectory,
    string BaseDirectory,
    Func<string, IReadOnlyList<string>> ListDirectories,
    Func<string, bool> IsRepository);
