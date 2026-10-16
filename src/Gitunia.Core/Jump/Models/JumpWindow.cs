namespace Gitunia.Core.Jump.Models;

/// <summary>The part of a list that is on screen, in row numbers of its <see cref="IJumpRows"/>.</summary>
/// <param name="FirstRow">The first row in view.</param>
/// <param name="RowCount">How many rows are in view.</param>
/// <param name="CursorRow">The row under the cursor; the nearest matches get labels first.</param>
public readonly record struct JumpWindow(int FirstRow, int RowCount, int CursorRow);
