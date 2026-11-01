using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Pick;
using Gitunia.Core.Running;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Pick;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Pick;

/// <summary>Commits, contexts and assertions shared by the pick reducer tests.</summary>
public static class PickTestData
{
    public const string First = "1111111111111111111111111111111111111111";
    public const string Second = "2222222222222222222222222222222222222222";
    public const string Third = "3333333333333333333333333333333333333333";

    public static readonly TabId Tab = new(1);

    public static PickCommit Plain(string hash, string subject = "plain") => new(hash, subject, []);

    public static PickCommit Merge(string hash, string subject = "merge") => new(
        hash,
        subject,
        [new PickParent(First, "main two"), new PickParent(Second, "topic two")]);

    public static HeadState OnBranch(string name = "main") => new(HeadKind.Branch, name, First);

    public static PickContext Context(
        HeadState? head = null,
        OperationInfo? operation = null,
        bool active = true,
        bool busy = false,
        MainlineQuestion? question = null) =>
        new(Tab, Location(), head ?? OnBranch(), operation, active, busy, question);

    public static MainlineQuestion Question(params PickCommit[] commits)
    {
        var merges = commits.Where(commit => commit.IsMerge).ToList();
        var count = merges.Min(commit => commit.Parents.Count);
        var usable = commits.Any(commit => !commit.IsMerge) ? 1 : count;

        return new MainlineQuestion(Tab, PickKind.Revert, [.. commits], count, usable);
    }

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
        var root = Path.Combine(Path.GetTempPath(), "gitunia-pick-tests");
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
