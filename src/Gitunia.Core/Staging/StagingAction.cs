namespace Gitunia.Core.Staging;

/// <summary>Which of the four staging keys of the file lists was pressed.</summary>
public enum StagingAction
{
    /// <summary>Stage the file, folder or Visual range under the cursor (Unstaged list).</summary>
    Stage,

    /// <summary>Stage every file of the Unstaged list.</summary>
    StageAll,

    /// <summary>Unstage the file, folder or Visual range under the cursor (Staged list).</summary>
    Unstage,

    /// <summary>Unstage every file of the Staged list.</summary>
    UnstageAll,
}
