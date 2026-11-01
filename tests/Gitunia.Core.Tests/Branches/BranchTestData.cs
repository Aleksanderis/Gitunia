using System.Collections.Immutable;
using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Branches;

/// <summary>Refs, contexts and assertions shared by the branch reducer tests.</summary>
public static class BranchTestData
{
    public const string Main = "1111111111111111111111111111111111111111";
    public const string Topic = "2222222222222222222222222222222222222222";
    public const string Behind = "3333333333333333333333333333333333333333";

    public static readonly TabId Tab = new(1);

    public static GitRef Local(
        string name,
        string commit,
        bool current = false,
        string? checkedOutIn = null,
        string? upstream = null,
        int? ahead = null,
        bool upstreamGone = false) => new(
            $"refs/heads/{name}",
            name,
            RefKind.LocalBranch,
            commit,
            commit,
            upstream,
            ahead,
            null,
            upstreamGone,
            current,
            checkedOutIn,
            null);

    public static GitRef Remote(string name, string commit) => new(
        $"refs/remotes/{name}",
        name,
        RefKind.RemoteBranch,
        commit,
        commit,
        null,
        null,
        null,
        false,
        false,
        null,
        null);

    public static GitRef Tag(string name, string commit) => new(
        $"refs/tags/{name}",
        name,
        RefKind.Tag,
        commit,
        commit,
        null,
        null,
        null,
        false,
        false,
        null,
        null);

    public static ImmutableArray<GitRef> Refs(params GitRef[] refs) => [.. refs];

    /// <summary>Main (current) and topic, a separate commit.</summary>
    public static ImmutableArray<GitRef> MainAndTopic() =>
        Refs(Local("main", Main, current: true), Local("topic", Topic));

    public static BranchContext Context(
        ImmutableArray<GitRef> refs,
        bool busy = false,
        OperationInfo? operation = null,
        bool active = true) => new(Tab, Location(), refs, operation, active, busy);

    public static ImmutableArray<AppEffect> Reduce(
        BranchCommand command,
        ImmutableArray<GitRef> refs,
        bool busy = false,
        OperationInfo? operation = null) =>
        BranchReducer.Reduce(command, Context(refs, busy, operation));

    public static string Warning(ImmutableArray<AppEffect> effects)
    {
        var post = Assert.Single(effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.Single(effects);

        return post.Text;
    }

    public static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-branch-tests");
        var gitDirectory = Path.Combine(root, ".git");

        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }
}
