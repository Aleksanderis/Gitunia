namespace Gitunia.Tui.App.CommandLine;

/// <summary>A token that is cancelled when the user presses Ctrl-C, for command-line paths that run without the UI.</summary>
public sealed class InterruptCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();

    public InterruptCancellation()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _source.Dispose();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArguments)
    {
        // Stop the download instead of killing the process, so the temporary files are cleaned up.
        eventArguments.Cancel = true;
        _source.Cancel();
    }
}
