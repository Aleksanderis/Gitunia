namespace Gitunia.Git.Commits.Models;

/// <summary>What to commit: the staged changes with a message, and the options of the working-tree message box.</summary>
/// <param name="Message">The commit message; git cleans up whitespace, lines starting with <c>#</c> are kept.</param>
/// <param name="SignOff">Adds a <c>Signed-off-by</c> trailer (<c>-s</c>).</param>
/// <param name="NoVerify">Skips the pre-commit and commit-msg hooks (<c>--no-verify</c>).</param>
/// <param name="Amend">Replaces the last commit (<c>--amend</c>): staged changes join it and the message is the new one.</param>
public sealed record CommitRequest(string Message, bool SignOff, bool NoVerify, bool Amend);
