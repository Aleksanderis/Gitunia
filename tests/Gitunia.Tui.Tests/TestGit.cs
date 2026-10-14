using Gitunia.Git.Execution;
using Gitunia.Tui.App;

namespace Gitunia.Tui.Tests;

/// <summary>The real git services (the git CLI behind every reader) for tests whose subject is the code on top of git.</summary>
public static class TestGit
{
    public static GitServices Services { get; } = GitServices.Create(new GitRunner());
}
