namespace Gitunia.Core.Search;

/// <summary>A ref jump candidate with the sort keys used to rank it.</summary>
/// <param name="Candidate">The candidate.</param>
/// <param name="Score">Fuzzy match score; higher sorts first.</param>
/// <param name="Order">Rank of the ref kind; lower sorts first.</param>
public readonly record struct RankedRefJump(RefJumpCandidate Candidate, int Score, int Order);
