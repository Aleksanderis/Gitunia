using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Rebase;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.Core.Tests.Rebase;

/// <summary>Tabs, plans and assertions shared by the rebase reducer tests.</summary>
public static class RebaseTestData
{
    public const string Tip = "1111111111111111111111111111111111111111";
    public const string Target = "2222222222222222222222222222222222222222";
    public const string Older = "3333333333333333333333333333333333333333";

    public static readonly TabId Tab = new(1);

    public static RepositoryState Repository(
        HeadState? head = null,
        OperationInfo? operation = null,
        ImmutableArray<GitRef>? refs = null,
        bool autoStash = false,
        params StatusEntry[] entries)
    {
        var tab = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(Location())).State.Workspace.Tabs[0];
        var branch = new StatusBranch(head ?? OnBranch(), null, null, null);

        return tab with
        {
            Id = Tab,
            Head = head ?? OnBranch(),
            Operation = operation,
            Refs = refs ?? [],
            RebaseAutoStash = autoStash,
            WorkingTree = new WorkingTreeState(new StatusSnapshot(branch, entries, []), null),
        };
    }

    public static RebaseContext Context(
        RepositoryState? tab = null,
        bool active = true,
        bool busy = false,
        bool overlay = false) => new(tab ?? Repository(), active, busy, overlay);

    public static HeadState OnBranch(string name = "main", string commit = Tip) => new(HeadKind.Branch, name, commit);

    public static StatusEntry Modified(string path) => new(
        StatusEntryKind.Ordinary,
        path,
        null,
        FileChangeKind.Unmodified,
        FileChangeKind.Modified,
        null,
        ConflictKind.None,
        null);

    public static StatusEntry Untracked(string path) => new(
        StatusEntryKind.Untracked,
        path,
        null,
        FileChangeKind.Unmodified,
        FileChangeKind.Unmodified,
        null,
        ConflictKind.None,
        null);

    public static GitRef Branch(string name, string commit, string? upstream = null, int? ahead = null) => new(
        $"refs/heads/{name}",
        name,
        RefKind.LocalBranch,
        commit,
        commit,
        upstream,
        ahead,
        null,
        false,
        true,
        null,
        null);

    public static QuickRebasePlan Plan(
        QuickRebaseKind kind,
        string head = Tip,
        string message = "Add feature\n\nBody",
        int later = 1)
    {
        List<RebaseTodoCommit> range = [new(Older, "Older"), new(Target, "Add feature")];
        for (var index = 0; index < later; index++)
        {
            range.Add(new RebaseTodoCommit($"{index + 4}{index + 4}{index + 4}{index + 4}", "later"));
        }

        var into = kind == QuickRebaseKind.Fixup ? new RebaseTodoCommit(Older, "Older") : null;

        return new QuickRebasePlan(kind, Target, message, RebaseBase.OntoCommit(Older), range, head, into);
    }

    public static RebaseTarget OntoTarget(string label = "topic") => new(Tab, Target, label, "main", Tip);

    public static string Warning(ImmutableArray<AppEffect> effects)
    {
        var post = Assert.Single(effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.DoesNotContain(effects, effect => effect is OperationEffect);

        return post.Text;
    }

    public static OperationInfo Stopped(RepositoryOperation kind) =>
        new(kind, null, null, null, null, null, null, null);

    public static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-rebase-tests");
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
