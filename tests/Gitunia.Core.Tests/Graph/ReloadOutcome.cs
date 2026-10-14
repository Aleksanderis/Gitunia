using Gitunia.Core.Graph;

namespace Gitunia.Core.Tests.Graph;

/// <summary>The rows after a reload and where the new rows joined the old ones, or -1 when they never did.</summary>
/// <param name="Result">The rows.</param>
/// <param name="JoinIndex">The join index.</param>
public sealed record ReloadOutcome(List<GraphEntry> Result, int JoinIndex);
