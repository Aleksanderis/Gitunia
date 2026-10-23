namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// A counting signal for tests that wait on a callback posted from a background thread. Releasing it after the test
/// disposed it does nothing: a plain <see cref="SemaphoreSlim"/> would throw <see cref="ObjectDisposedException"/>
/// on the pool thread, which can end the whole test host instead of failing one test.
/// The inner semaphore is deliberately not disposed (no wait handle is used), so a late wait cannot throw either.
/// </summary>
public sealed class TestSignal : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _semaphore = new(0);
    private bool _disposed;

    public int CurrentCount => _semaphore.CurrentCount;

    public void Release()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _semaphore.Release();
            }
        }
    }

    public bool Wait(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        _semaphore.Wait(timeout, cancellationToken);

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(timeout, cancellationToken);

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }
}
