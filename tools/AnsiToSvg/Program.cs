// Converts the ANSI text of `tmux capture-pane -p -e` (stdin) into an SVG terminal screenshot (stdout).
// Usage: AnsiToSvg [--theme dark|light] [--columns N] [--rows N] < capture.ansi > screen.svg
// Every character gets an explicit x position, so the grid stays aligned whatever monospace font the renderer picks.
using System.Globalization;
using System.Text;

var theme = "dark";
var columns = 0;
var rows = 0;
for (var index = 0; index < args.Length - 1; index++)
{
    if (args[index] == "--theme") { theme = args[index + 1]; }
    if (args[index] == "--columns") { columns = int.Parse(args[index + 1], CultureInfo.InvariantCulture); }
    if (args[index] == "--rows") { rows = int.Parse(args[index + 1], CultureInfo.InvariantCulture); }
}

var light = theme == "light";
var defaultForeground = light ? (0x38, 0x3a, 0x42) : (0xd4, 0xd4, 0xd4);
var defaultBackground = light ? (0xfa, 0xfa, 0xfa) : (0x1e, 0x1e, 0x1e);
(int Red, int Green, int Blue)[] palette =
[
    (0x00, 0x00, 0x00), (0xcd, 0x31, 0x31), (0x0d, 0xbc, 0x79), (0xe5, 0xe5, 0x10),
    (0x24, 0x72, 0xc8), (0xbc, 0x3f, 0xbc), (0x11, 0xa8, 0xcd), (0xe5, 0xe5, 0xe5),
    (0x66, 0x66, 0x66), (0xf1, 0x4c, 0x4c), (0x23, 0xd1, 0x8b), (0xf5, 0xf5, 0x43),
    (0x3b, 0x8e, 0xea), (0xd6, 0x70, 0xd6), (0x29, 0xb8, 0xdb), (0xff, 0xff, 0xff),
];

const double CellWidth = 9.6;
const double CellHeight = 20;
const double Padding = 12;

var input = Console.In.ReadToEnd().Replace("\r", string.Empty, StringComparison.Ordinal);
var lines = input.Split('\n').ToList();
while (lines.Count > 0 && lines[^1].Length == 0) { lines.RemoveAt(lines.Count - 1); }
var background = new StringBuilder();
var text = new StringBuilder();
var maxColumns = columns;

(int, int, int)? currentForeground = null;
(int, int, int)? currentBackground = null;
bool bold = false, dim = false, italic = false, underline = false, reverse = false;

for (var row = 0; row < lines.Count; row++)
{
    var line = lines[row];
    var column = 0;
    var runText = new StringBuilder();
    var runX = new StringBuilder();
    (int, int, int) runColor = default;
    var runStyle = string.Empty;

    void Flush()
    {
        if (runText.ToString().Trim().Length > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"<text x=\"{runX.ToString().Trim()}\" y=\"{Padding + (row * CellHeight) + 15:0.##}\" fill=\"{Hex(runColor)}\"{runStyle} xml:space=\"preserve\">{Escape(runText.ToString())}</text>\n");
        }

        runText.Clear();
        runX.Clear();
    }

    var position = 0;
    while (position < line.Length)
    {
        if (line[position] == '\u001b' && position + 1 < line.Length && line[position + 1] == '[')
        {
            var end = position + 2;
            while (end < line.Length && (line[end] is (>= '0' and <= '9') or ';' or ':')) { end++; }
            if (end < line.Length && line[end] == 'm')
            {
                var parameters = line[(position + 2)..end].Split(';');
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    var code = parameters[parameterIndex].Length == 0 ? 0 : int.Parse(parameters[parameterIndex].Split(':')[0], CultureInfo.InvariantCulture);
                    switch (code)
                    {
                        case 0: currentForeground = null; currentBackground = null; bold = dim = italic = underline = reverse = false; break;
                        case 1: bold = true; break;
                        case 2: dim = true; break;
                        case 3: italic = true; break;
                        case 4: underline = true; break;
                        case 7: reverse = true; break;
                        case 22: bold = dim = false; break;
                        case 23: italic = false; break;
                        case 24: underline = false; break;
                        case 27: reverse = false; break;
                        case 39: currentForeground = null; break;
                        case 49: currentBackground = null; break;
                        case >= 30 and <= 37: currentForeground = palette[code - 30]; break;
                        case >= 40 and <= 47: currentBackground = palette[code - 40]; break;
                        case >= 90 and <= 97: currentForeground = palette[code - 90 + 8]; break;
                        case >= 100 and <= 107: currentBackground = palette[code - 100 + 8]; break;
                        case 38 or 48:
                            (int, int, int)? color = null;
                            if (parameterIndex + 2 < parameters.Length && parameters[parameterIndex + 1] == "5")
                            {
                                color = Indexed(int.Parse(parameters[parameterIndex + 2], CultureInfo.InvariantCulture));
                                parameterIndex += 2;
                            }
                            else if (parameterIndex + 4 < parameters.Length && parameters[parameterIndex + 1] == "2")
                            {
                                color = (int.Parse(parameters[parameterIndex + 2], CultureInfo.InvariantCulture), int.Parse(parameters[parameterIndex + 3], CultureInfo.InvariantCulture), int.Parse(parameters[parameterIndex + 4], CultureInfo.InvariantCulture));
                                parameterIndex += 4;
                            }

                            if (code == 38) { currentForeground = color; } else { currentBackground = color; }
                            break;
                    }
                }
            }

            position = end + 1;
            continue;
        }

        var rune = Rune.GetRuneAt(line, position);
        position += rune.Utf16SequenceLength;
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            continue; // Zero width: skipped so the columns after it stay aligned
        }

        var width = IsWide(rune.Value) ? 2 : 1;
        var foregroundColor = currentForeground ?? defaultForeground;
        var backgroundColor = currentBackground ?? defaultBackground;
        var hasBackground = currentBackground is not null;
        if (reverse)
        {
            (foregroundColor, backgroundColor) = (backgroundColor, foregroundColor);
            hasBackground = true;
        }

        if (dim)
        {
            foregroundColor = Mix(foregroundColor, defaultBackground, 0.55);
        }

        var style = (bold ? " font-weight=\"bold\"" : string.Empty) + (italic ? " font-style=\"italic\"" : string.Empty) + (underline ? " text-decoration=\"underline\"" : string.Empty);
        if (hasBackground)
        {
            background.Append(CultureInfo.InvariantCulture, $"<rect x=\"{Padding + (column * CellWidth):0.##}\" y=\"{Padding + (row * CellHeight):0.##}\" width=\"{(width * CellWidth) + 0.6:0.##}\" height=\"{CellHeight + 0.6}\" fill=\"{Hex(backgroundColor)}\"/>\n");
        }

        if (runText.Length > 0 && (runColor != foregroundColor || runStyle != style))
        {
            Flush();
        }

        runColor = foregroundColor;
        runStyle = style;
        runText.Append(rune.ToString());
        runX.Append(CultureInfo.InvariantCulture, $" {Padding + (column * CellWidth):0.##}");
        column += width;
    }

    Flush();
    maxColumns = Math.Max(maxColumns, column);
}

var totalColumns = Math.Max(maxColumns, 1);
var totalRows = Math.Max(rows, lines.Count);
var svgWidth = (totalColumns * CellWidth) + (2 * Padding);
var svgHeight = (totalRows * CellHeight) + (2 * Padding);
var output = new StringBuilder();
output.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgWidth:0.##}\" height=\"{svgHeight:0.##}\" viewBox=\"0 0 {svgWidth:0.##} {svgHeight:0.##}\">\n");
output.Append(CultureInfo.InvariantCulture, $"<rect width=\"100%\" height=\"100%\" fill=\"{Hex(defaultBackground)}\"/>\n");
output.Append(background);
output.Append("<g font-family=\"JetBrainsMono Nerd Font Mono, JetBrains Mono, DejaVu Sans Mono, Liberation Mono, monospace\" font-size=\"16\">\n");
output.Append(text);
output.Append("</g>\n</svg>\n");
Console.Out.Write(output.ToString());
return 0;

string Hex((int Red, int Green, int Blue) color) => $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}";

string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

(int, int, int) Mix((int Red, int Green, int Blue) first, (int Red, int Green, int Blue) second, double weight) =>
    ((int)((first.Red * weight) + (second.Red * (1 - weight))), (int)((first.Green * weight) + (second.Green * (1 - weight))), (int)((first.Blue * weight) + (second.Blue * (1 - weight))));

(int, int, int) Indexed(int number)
{
    if (number < 16) { return palette[number]; }
    if (number >= 232) { var level = 8 + ((number - 232) * 10); return (level, level, level); }
    var cube = number - 16;
    return (CubeLevel(cube / 36), CubeLevel(cube / 6 % 6), CubeLevel(cube % 6));
}

int CubeLevel(int value) => value == 0 ? 0 : 55 + (value * 40);

bool IsWide(int codePoint) =>
    codePoint is (>= 0x1100 and <= 0x115f) or (>= 0x2e80 and <= 0xa4cf) or (>= 0xac00 and <= 0xd7a3) or (>= 0xf900 and <= 0xfaff) or (>= 0xfe30 and <= 0xfe6f) or (>= 0xff00 and <= 0xff60) or (>= 0xffe0 and <= 0xffe6) or (>= 0x1f300 and <= 0x1f64f) or (>= 0x1f680 and <= 0x1f6ff) or (>= 0x1f900 and <= 0x1f9ff) or (>= 0x20000 and <= 0x3fffd);
