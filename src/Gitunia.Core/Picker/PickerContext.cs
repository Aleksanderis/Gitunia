using System.Collections.Immutable;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Picker;

/// <summary>
/// Everything the picker needs from outside: the workspace, scan results and the base directory for relative paths. The
/// picker logic is pure: it never reads the process working directory or the file system (listing a typed path is an effect).
/// </summary>
/// <param name="Workspace">Open tabs, recents and pins.</param>
/// <param name="Discovered">Repository root paths found by the scanner so far.</param>
/// <param name="BaseDirectory">What a relative path (<c>./</c>, <c>../</c>) is resolved against: the active repository, or the home directory.</param>
/// <param name="ListBudget">The rows the picker list can show (see <see cref="PickerListPlan.Budget"/>); with the sections' caps it decides which rows are listed.</param>
public sealed record PickerContext(
    Workspace Workspace,
    ImmutableArray<string> Discovered,
    string BaseDirectory,
    int ListBudget = PickerListPlan.MaximumListRows);
