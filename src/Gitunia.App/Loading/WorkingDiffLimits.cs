using Gitunia.Core.Keymap;
using Gitunia.App.Loading.Diff;

namespace Gitunia.App.Loading;

/// <summary>The sizes after which a <see cref="WorkingDiffStore"/> cuts a diff short; <c>Keymap</c> is where its notes read the key to load all; <c>Tasks</c> remembers the running reads so the shutdown can wait for them.</summary>
public sealed record WorkingDiffLimits(
    int RowLimit = DiffStore.DefaultRowLimit,
    int FileLineLimit = DiffStore.DefaultFileLineLimit,
    int FolderFileLimit = WorkingDiffStore.DefaultFolderFileLimit,
    KeyBindingTable? Keymap = null,
    ShutdownTasks? Tasks = null);
