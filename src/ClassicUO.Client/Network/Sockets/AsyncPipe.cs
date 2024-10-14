using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ClassicUO.Network.Sockets;

#nullable enable

internal sealed class AsyncPipe : Pipe, IValueTaskSource<Memory<byte>>
{
    private ManualResetValueTaskSourceCore<Memory<byte>> _source;

    public CancellationToken Token { get; }
    public new AsyncPipe? Next { get; set; }

    public AsyncPipe(uint size, CancellationToken cancellationToken)
        : base(size)
    {
        Token = cancellationToken;

        _source.RunContinuationsAsynchronously = true;
        _source.SetResult(Memory<byte>.Empty);

        cancellationToken.Register(cancel);

        void cancel()
        {
            if (_source.GetStatus(_source.Version) == ValueTaskSourceStatus.Pending)
                _source.SetException(new OperationCanceledException());
        }
    }

    public ValueTask<Memory<byte>> GetAvailableMemoryToRead()
    {
        Memory<byte> memory = GetAvailableMemoryToReadCore();
        if (!memory.IsEmpty)
            return new(memory);

        if (_source.GetStatus(_source.Version) == ValueTaskSourceStatus.Succeeded)
            _source.Reset();

        return new(this, _source.Version);
    }

    public override void CommitWrited(int size)
    {
        if (size <= 0)
            return;

        base.CommitWrited(size);

        if (_source.GetStatus(_source.Version) == ValueTaskSourceStatus.Pending)
            _source.SetResult(GetAvailableMemoryToReadCore());
    }

    public Memory<byte> GetResult(short token)
    {
        return _source.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _source.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _source.OnCompleted(continuation, state, token, flags);
    }

    private Memory<byte> GetAvailableMemoryToReadCore()
    {
        int readIndex = (int)(_readIndex & _mask);
        int writeIndex = (int)(_writeIndex & _mask);

        if (readIndex > writeIndex)
            return _buffer.AsMemory(readIndex);

        if (Length == _buffer.Length)
            return _buffer.AsMemory(readIndex);

        return _buffer.AsMemory(readIndex..writeIndex);
    }
}
