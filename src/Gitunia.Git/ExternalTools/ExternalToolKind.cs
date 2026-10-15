namespace Gitunia.Git.ExternalTools;

/// <summary>The git commands that need the real terminal.</summary>
public enum ExternalToolKind
{
    /// <summary><c>git difftool</c> of a commit against its first parent.</summary>
    Difftool,

    /// <summary><c>git mergetool</c> for the conflicted files.</summary>
    Mergetool,

    /// <summary><c>git commit</c> on the staged index, with git's own editor ($GIT_EDITOR, core.editor, $EDITOR).</summary>
    CommitWithEditor,
}
