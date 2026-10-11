using Gitunia.Core.Workspaces;
using Gitunia.Git;

namespace Gitunia.Core.Tests.Persistence;

public static class SessionFixtures
{
    public static string AbsolutePath(string name) => Path.Combine(Path.GetTempPath(), "gitunia-fixture", name);

    public static RepositoryLocation Location(string name) =>
        new(AbsolutePath(name), Path.Combine(AbsolutePath(name), ".git"), Path.Combine(AbsolutePath(name), ".git"), false, false);

    public static Workspace Open(Workspace workspace, params string[] names)
    {
        foreach (var name in names)
        {
            workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(Location(name))).Workspace;
        }

        return workspace;
    }
}
