/// <summary>A terminal color as red, green and blue channels (0-255).</summary>
/// <param name="Red">The red channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Blue">The blue channel.</param>
internal readonly record struct Rgb(int Red, int Green, int Blue);
