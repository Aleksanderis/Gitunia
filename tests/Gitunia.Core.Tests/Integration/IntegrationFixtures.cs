using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Integration;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Integration;

// Shared setup of the reset, merge and operation reducer tests.
internal static class IntegrationFixtures
{
    public const string Tip = "1111111111111111111111111111111111111111";

    public static readonly TabId Tab = new(1);

    public static readonly HeadState OnMain = new(HeadKind.Branch, "main", Tip);

    public static IntegrationContext Context(
        HeadState? head = null,
        OperationInfo? operation = null,
        int conflicts = 0,
        bool busy = false) =>
        new(Tab, Location(), head ?? OnMain, operation, conflicts, busy);

    public static OperationInfo Info(RepositoryOperation kind, string? commit = null, string? original = null) =>
        new(kind, "topic", null, null, null, commit, "a subject", original);

    public static RepositoryLocation Location()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-integration-tests");
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    public static string Warning(ImmutableArray<AppEffect> effects)
    {
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(effects));
        Assert.Equal(StatusSeverity.Warning, post.Severity);

        return post.Text;
    }
}
