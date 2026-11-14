using System.Text;
using Gitunia.Git.Execution;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public class DrainingReaderTests
{
    private const string Output = "done";
    private const int PollsBeforeReadStarts = 5;
    private const int BufferSize = 16;

    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan LongDeferral = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GivenReadStillQueuedInTheThreadPool_WhenGraceLapsesAfterExit_ThenOutputIsDelivered()
    {
        // Models #1340: the read has not reached the pipe (it waits in the pool queue) when the grace lapses. The
        // probe lets the read start after a few polls past the grace, before the token is cancelled.
        var stream = new QueuedReadStream(Output);
        var queue = new QueueProbe(stream, PollsBeforeReadStarts);
        var reader = new DrainingReader(Grace, LongDeferral, queue.Count);
        var buffer = new byte[BufferSize];

        var count = await ReadAfterExitAsync(reader, stream, buffer);

        Assert.Equal(Output, Encoding.UTF8.GetString(buffer, 0, count));
    }

    [Fact]
    public async Task GivenNoDeferral_WhenReadIsStillQueuedAtTheGrace_ThenTheReadIsCut()
    {
        var stream = new QueuedReadStream(Output);
        var reader = new DrainingReader(Grace, TimeSpan.Zero);
        var started = stream.StartOnceCancelledAsync();

        var count = await ReadAfterExitAsync(reader, stream, new byte[BufferSize]);
        await started;

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GivenPoolQueueNeverDrains_WhenReadStaysPendingAfterExit_ThenTheDeferralEndsAndTheReadIsCut()
    {
        var stream = new QueuedReadStream(Output);
        var reader = new DrainingReader(Grace, TimeSpan.FromMilliseconds(200), () => 1);
        var started = stream.StartOnceCancelledAsync();

        var count = await ReadAfterExitAsync(reader, stream, new byte[BufferSize]);
        await started;

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GivenEmptyPoolQueue_WhenReadStaysPendingAfterExit_ThenTheReadIsCutAfterTheGrace()
    {
        var stream = new QueuedReadStream(Output);
        var reader = new DrainingReader(Grace, LongDeferral, () => 0);
        var started = stream.StartOnceCancelledAsync();

        var count = await ReadAfterExitAsync(reader, stream, new byte[BufferSize]);
        await started;

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GivenAbsoluteCapLapses_WhenPoolQueueNeverDrains_ThenTheReadIsCutWithoutDeferral()
    {
        using var cap = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stream = new QueuedReadStream(Output);
        var reader = new DrainingReader(Grace, LongDeferral, () => 1);
        var started = stream.StartOnceCancelledAsync();
        var neverExits = new TaskCompletionSource().Task;

        var pending = reader.ReadChunkAsync(
            stream,
            new byte[BufferSize],
            neverExits,
            cap.Token,
            TestContext.Current.CancellationToken);
        await cap.CancelAsync();
        var count = await pending.WaitAsync(Limit, TestContext.Current.CancellationToken);
        await started;

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GivenCallerCancels_WhenCutIsDeferred_ThenCancellationPropagates()
    {
        using var hard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stream = new QueuedReadStream(Output);
        var queue = new QueueProbe(stream, int.MaxValue);
        var reader = new DrainingReader(Grace, LongDeferral, queue.Count);
        var started = stream.StartOnceCancelledAsync();

        var pending = reader.ReadChunkAsync(
            stream,
            new byte[BufferSize],
            Task.CompletedTask,
            CancellationToken.None,
            hard.Token);
        await queue.Polled.WaitAsync(Limit, TestContext.Current.CancellationToken);
        await hard.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(Limit, TestContext.Current.CancellationToken));
        await started;
    }

    private static Task<int> ReadAfterExitAsync(DrainingReader reader, Stream stream, byte[] buffer) =>
        reader
            .ReadChunkAsync(
                stream,
                buffer,
                Task.CompletedTask,
                CancellationToken.None,
                TestContext.Current.CancellationToken)
            .WaitAsync(Limit, TestContext.Current.CancellationToken);

    // A read that, like a synchronous FileStream on Windows, looks at its token only when it starts (after a hop
    // through the pool queue); the test decides when that is, so nothing depends on scheduling.
    private sealed class QueuedReadStream : Stream
    {
        private readonly TaskCompletionSource<int> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancelObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly byte[] _data;
        private Memory<byte> _buffer;
        private CancellationToken _token;

        public QueuedReadStream(string text)
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

        /// <summary>Runs the queued read now: it fails when its token is already cancelled, else it reads.</summary>
        public void Start()
        {
            if (_token.IsCancellationRequested)
            {
                _result.TrySetCanceled(_token);
            }
            else
            {
                _data.CopyTo(_buffer);
                _result.TrySetResult(_data.Length);
            }
        }

        /// <summary>Starts the read after its token was cancelled, as a pool starved past the cut would.</summary>
        public async Task StartOnceCancelledAsync()
        {
            await _cancelObserved.Task.WaitAsync(Limit);
            Start();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _buffer = buffer;
            _token = cancellationToken;
            cancellationToken.Register(_cancelObserved.SetResult);

            return new ValueTask<int>(_result.Task);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Reports a non-empty queue for a number of polls, starting the read at the last of them, then an empty queue.
    private sealed class QueueProbe
    {
        private readonly QueuedReadStream _stream;
        private readonly int _pollsBeforeStart;
        private readonly TaskCompletionSource _polled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _polls;

        public QueueProbe(QueuedReadStream stream, int pollsBeforeStart)
        {
            _stream = stream;
            _pollsBeforeStart = pollsBeforeStart;
        }

        /// <summary>Completes at the first poll.</summary>
        public Task Polled => _polled.Task;

        public long Count()
        {
            _polled.TrySetResult();
            var polls = Interlocked.Increment(ref _polls);
            if (polls == _pollsBeforeStart)
            {
                _stream.Start();
            }

            return polls <= _pollsBeforeStart ? 1 : 0;
        }
    }
}
