using Gitunia.Core.Diff;

namespace Gitunia.App.Loading.Diff;

/// <summary>A diff read by <see cref="DiffStore"/>.</summary>
/// <param name="Document">The rows, with a closing note when the read was cut short.</param>
/// <param name="IsCut">The row or per-file limit stopped the read, so there is more.</param>
internal sealed record DiffReadResult(DiffDocument Document, bool IsCut);
