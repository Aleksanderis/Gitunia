using Gitunia.Git.Operations.Models;

namespace Gitunia.Git.Rebase;

/// <summary>
/// The result of starting an interactive rebase. A stop on conflicts is a result; other failures throw.
/// </summary>
/// <param name="Outcome">Completed, or stopped on conflicts (continue, skip and abort take over from there).</param>
/// <param name="Head">The commit HEAD points at afterwards.</param>
/// <param name="OriginalHead">
/// HEAD before the rebase started: the undo is <c>git reset --hard &lt;OriginalHead&gt;</c>, and
/// <c>git rebase --abort</c> returns there too. Git also keeps it in <c>ORIG_HEAD</c> and the reflog.
/// </param>
public sealed record InteractiveRebaseResult(OperationOutcome Outcome, string? Head, string OriginalHead);
