namespace Gitunia.Git.Status.Models;

/// <summary>How much of the untracked files <see cref="StatusReader"/> asks git to list (<c>--untracked-files</c>).</summary>
public enum UntrackedFilesMode
{
    /// <summary>Every untracked file individually (<c>all</c>); needed where files are listed.</summary>
    All,

    /// <summary>Untracked directories as one entry (<c>normal</c>); enough to know the tree is dirty and cheaper on big untracked trees.</summary>
    Normal,
}
