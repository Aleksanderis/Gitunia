namespace Gitunia.Git.ExternalTools;

/// <summary>
/// Builds the git argument lists of the external tool hand-offs. The user's own configuration decides which editor
/// and which tool run (<c>diff.tool</c>, <c>merge.tool</c>, <c>core.editor</c>), so none is set here.
/// </summary>
public static class ExternalToolCommands
{
    private const string RepositoryDirectoryOption = "-C";
    private const string NoPromptOption = "--no-prompt";

    /// <summary>Arguments for <paramref name="kind"/>; <paramref name="commit"/> is required for the difftool.</summary>
    public static IReadOnlyList<string> Build(ExternalToolKind kind, string repositoryPath, string? commit = null) =>
        kind switch
        {
            ExternalToolKind.Difftool => [RepositoryDirectoryOption, repositoryPath, "difftool", NoPromptOption, $"{Require(commit)}^1", Require(commit)],
            ExternalToolKind.Mergetool => [RepositoryDirectoryOption, repositoryPath, "mergetool"],
            ExternalToolKind.CommitWithEditor => [RepositoryDirectoryOption, repositoryPath, "commit"],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    /// <summary>The name shown in messages.</summary>
    public static string NameOf(ExternalToolKind kind) =>
        kind switch
        {
            ExternalToolKind.Difftool => "difftool",
            ExternalToolKind.Mergetool => "mergetool",
            _ => "editor",
        };

    private static string Require(string? commit) =>
        commit ?? throw new ArgumentException("The difftool needs a commit.", nameof(commit));
}
