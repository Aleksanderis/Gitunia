using Xunit;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>Tests that change process-wide environment variables join this group so they run alone (#299).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AmbientEnvironmentGroup
{
    public const string Name = "Ambient git environment";
}
