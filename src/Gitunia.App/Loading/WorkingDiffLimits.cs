using Gitunia.App.Loading.Diff;

namespace Gitunia.App.Loading;

/// <summary>The sizes after which a <see cref="WorkingDiffStore"/> cuts a diff short.</summary>
public sealed record WorkingDiffLimits(
    int RowLimit = DiffStore.DefaultRowLimit,
    int FileLineLimit = DiffStore.DefaultFileLineLimit,
    int FolderFileLimit = WorkingDiffStore.DefaultFolderFileLimit);
