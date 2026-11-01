namespace Gitunia.Core.Running;

/// <summary>One operation in flight for a tab.</summary>
/// <param name="Id">Counts up per tab, so a started operation can be told from an earlier one of the same kind.</param>
/// <param name="Kind">What it does.</param>
public sealed record RunningOperation(long Id, RunningOperationKind Kind);
