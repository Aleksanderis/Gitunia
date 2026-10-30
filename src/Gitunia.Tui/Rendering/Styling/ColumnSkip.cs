namespace Gitunia.Tui.Rendering.Styling;

/// <summary>Where text continues after some columns were skipped (<see cref="TextWidth.SkipColumns"/>).</summary>
/// <param name="Index">The index of the first character that is kept.</param>
/// <param name="Overshoot">Columns the skipped part took beyond the request (a wide character cut in half).</param>
public readonly record struct ColumnSkip(int Index, int Overshoot);
