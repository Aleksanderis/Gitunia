using System.Text;
using Gitunia.Git.Execution;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public class DrainingReaderTests
{
    private const string Output = "done";
    private const int PollsBeforeReadStarts = 5;

    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GivenReadStillQueuedInTheThreadPool_WhenGraceLapsesAfterExit_ThenOutputIsDelivered()
    {
        // Models #1340: the read has not reached the pipe (it waits in the pool queue) when the grace lapses.
        var stream = new LateStartStream(Output);
        var queue = new QueueProbe(stream, PollsBeforeReadStarts);
        var reader = new DrainingReader(Grace, TimeSpan.FromSeconds(10), queue.Count);
        var buffer = new byte[16];

        var count = await ReadAfterExitAsync(reader, stream, buffer);

        Assert.Equal(Output, Encoding.UTF8.GetString(buffer, 0, count));
    }

    [Fact]
    public async Task GivenNoDeferral_WhenReadIsStillQueuedAtTheGrace_ThenTheReadIsCut()
    {
        var stream = new LateStartStream(Output);
        var queue = new QueueProbe(stream, PollsBeforeReadStarts);
        var reader = new DrainingReader(Grace, TimeSpan.Zero, queue.Count);

        var count = await ReadAfterExitAsync(reader, stream, new byte[16]);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GivenPoolQueueNeverDrains_WhenReadStaysPendingAfterExit_ThenTheDeferralEndsAndTheReadIsCut()
    {
        var stream = new LateStartStream(Output);
        var reader = new DrainingReader(Grace, TimeSpan.FromMilliseconds(200), () => 1);

        var count = await ReadAfterExitAsync(reader, stream, new byte[16]);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GivenEmptyPoolQueue_WhenReadStaysPendingAfterExit_ThenTheReadIsCutAfterTheGrace()
    {
        var stream = new LateStartStream(Output);
        var reader = new DrainingReader(Grace, TimeSpan.FromSeconds(10), () => 0);

        var count = await ReadAfterExitAsync(reader, stream, new byte[16]);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GivenCallerCancels_WhenReadIsPending_ThenCancellationPropagates()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stream = new LateStartStream(Output);
        var reader = new DrainingReader(Grace, TimeSpan.FromSeconds(10), () => 0);

        var neverExits = new TaskCompletionSource().Task;
        var pending = reader.ReadChunkAsync(stream, new byte[16], neverExits, CancellationToken.None, source.Token);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(Limit, TestContext.Current.CancellationToken));
    }

    private static Task<int> ReadAfterExitAsync(DrainingReader reader, Stream stream, byte[] buffer) =>
        reader
            .ReadChunkAsync(stream, buffer, Task.CompletedTask, CancellationToken.None, TestContext.Current.CancellationToken)
            .WaitAsync(Limit, TestContext.Current.CancellationToken);

    // A read that, like a synchronous FileStream on Windows, looks at its token only when it starts, which happens
    // when the test releases it; until then it waits (the pool queue).
    private sealed class LateStartStream : Stream
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly byte[] _data;
        private bool _delivered;

        public LateStartStream(string text)
        {
            _data = Encoding.UTF8.GetBytes(text);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Start() => _started.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (_delivered)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            _delivered = true;
            _data.CopyTo(buffer);

            return _data.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Reports a non-empty queue for a number of polls, then lets the read start (still reporting it once more).
    private sealed class QueueProbe
    {
        private readonly LateStartStream _stream;
        private readonly int _pollsBeforeStart;
        private int _polls;

        public QueueProbe(LateStartStream stream, int pollsBeforeStart)
        {
            _stream = stream;
            _pollsBeforeStart = pollsBeforeStart;
        }

        public long Count()
        {
            var polls = Interlocked.Increment(ref _polls);
            if (polls == _pollsBeforeStart)
            {
                _stream.Start();
            }

            return polls <= _pollsBeforeStart ? 1 : 0;
        }
    }
}
