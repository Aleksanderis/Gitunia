using System.Text;

namespace Gitunia.Core.Clipboard;

/// <summary>Builds OSC 52 "set clipboard" sequences. Pure: no console, no environment.</summary>
public static class Osc52Encoder
{
    private const string Escape = "\u001b";
    private const string StringTerminator = "\u001b\\";
    private const int ScreenChunkLength = 76;

    /// <summary>Build the sequence that sets the system clipboard to <paramref name="text"/>.</summary>
    /// <param name="text">The text to copy, sent as UTF-8.</param>
    /// <param name="options">Delivery settings from <see cref="Osc52Environment.Detect"/>.</param>
    public static Osc52Result Encode(string text, Osc52Options options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode == Osc52Mode.Unsupported)
        {
            return Failure(Osc52Status.Unsupported, $"Cannot copy: {options.UnsupportedReason ?? "the terminal does not support OSC 52"}.");
        }

        if (text.Length == 0)
        {
            return Failure(Osc52Status.Empty, "Nothing to copy.");
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var encodedLength = (bytes.Length + 2) / 3 * 4;
        if (encodedLength > options.MaximumEncodedLength)
        {
            return Failure(Osc52Status.TooLarge, $"Too large to copy ({bytes.Length} bytes, limit about {options.MaximumEncodedLength / 4 * 3} bytes).");
        }

        // Screen chunks the sequence into DCS strings, so the OSC must end with BEL: an ESC \ terminator could be cut in half.
        var terminator = options.Mode == Osc52Mode.Screen ? "\a" : StringTerminator;
        var sequence = $"{Escape}]52;c;{Convert.ToBase64String(bytes)}{terminator}";
        return new Osc52Result(Osc52Status.Ready, Wrap(sequence, options.Mode), string.Empty);
    }

    private static Osc52Result Failure(Osc52Status status, string message) => new(status, string.Empty, message);

    private static string Wrap(string sequence, Osc52Mode mode) => mode switch
    {
        Osc52Mode.Tmux => $"{Escape}Ptmux;{sequence.Replace(Escape, Escape + Escape, StringComparison.Ordinal)}{StringTerminator}",
        Osc52Mode.Screen => WrapForScreen(sequence),
        _ => sequence,
    };

    private static string WrapForScreen(string sequence)
    {
        var builder = new StringBuilder(sequence.Length + sequence.Length / ScreenChunkLength * 4 + 8);
        for (var start = 0; start < sequence.Length; start += ScreenChunkLength)
        {
            var length = Math.Min(ScreenChunkLength, sequence.Length - start);
            builder.Append('\u001b').Append('P').Append(sequence, start, length).Append(StringTerminator);
        }

        return builder.ToString();
    }
}
