namespace Gitunia.Core.Search;

/// <summary>The result of a search transition.</summary>
/// <param name="State">The next state.</param>
/// <param name="Effects">What the runner must do.</param>
public sealed record GraphSearchUpdate(GraphSearchState State, IReadOnlyList<GraphSearchEffect> Effects);
