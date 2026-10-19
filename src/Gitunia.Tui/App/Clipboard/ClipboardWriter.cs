using Gitunia.Core.Clipboard;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>Runs <see cref="CopyToClipboardEffect"/>s: the only place that writes OSC 52 to the terminal.</summary>
public sealed class ClipboardWriter
{
    public const string Hint = "Check that your terminal or tmux allows OSC 52 clipboard writes.";

    private readonly TextWriter _output;
    private readonly Osc52Options _options;

    public ClipboardWriter(TextWriter output, Osc52Options options)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        _output = output;
        _options = options;
    }

    public ClipboardOutcome Run(CopyToClipboardEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        var result = Osc52Encoder.Encode(effect.Text, _options);
        if (result.Status != Osc52Status.Ready)
        {
            return new ClipboardOutcome(false, result.Message);
        }

        try
        {
            _output.Write(result.Sequence);
            _output.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return new ClipboardOutcome(false, $"Cannot copy: writing to the terminal failed ({exception.Message}). {Hint}");
        }

        return new ClipboardOutcome(true, $"Copied {effect.Description}");
    }
}
