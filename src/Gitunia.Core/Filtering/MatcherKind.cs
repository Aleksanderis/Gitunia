namespace Gitunia.Core.Filtering;

/// <summary>Which matcher a filter list uses; every list chooses one explicitly.</summary>
public enum MatcherKind
{
    /// <summary>Characters in order, best match first (<see cref="FuzzyMatcher"/>): pickers of names and commands.</summary>
    Fuzzy,

    /// <summary>The typed text as a whole, in the list's own order (<see cref="ContainsMatcher"/>): lists the user knows by heart.</summary>
    Contains,
}
