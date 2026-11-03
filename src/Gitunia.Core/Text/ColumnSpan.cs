namespace Gitunia.Core.Text;

/// <summary>The characters from <paramref name="First"/> to <paramref name="Last"/> of a line, both included.</summary>
/// <param name="First">The index of the first character.</param>
/// <param name="Last">The index of the last character.</param>
public readonly record struct ColumnSpan(int First, int Last);
