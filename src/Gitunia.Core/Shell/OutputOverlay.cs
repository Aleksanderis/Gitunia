using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Shell;

/// <summary>The output of a failed command (a failing commit hook) shown in a scrollable box.</summary>
/// <param name="Title">The box title.</param>
/// <param name="Heading">The heading of the text section.</param>
/// <param name="Lines">The output, one entry per line.</param>
/// <param name="ReturnMode">The key mode to restore when the box closes.</param>
public sealed record OutputOverlay(string Title, string Heading, IReadOnlyList<string> Lines, KeyMode ReturnMode);
