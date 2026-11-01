using System.Collections.Immutable;

namespace Gitunia.Core.Staging;

/// <summary>The effects of a staging command and the commands to reduce right after it, in order.</summary>
/// <param name="Effects">The effects to run.</param>
/// <param name="Next">The commands that follow.</param>
public sealed record StagingUpdate(ImmutableArray<AppEffect> Effects, ImmutableArray<AppCommand> Next)
{
    /// <summary>Effects and no follow-up commands.</summary>
    public static StagingUpdate Of(params AppEffect[] effects) => new([.. effects], []);
}
