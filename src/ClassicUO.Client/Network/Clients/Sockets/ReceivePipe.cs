using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ClassicUO.Network.Clients.Sockets;

#nullable enable

internal sealed class ReceivePipe : Pipe, IValueTaskSource<Memory<byte>>, IDisposable
{
    private readonly CancellationTokenRegistration _cancellationTokenRegistration;
    private ManualResetValueTaskSourceCore<Memory<byte>> _source;

    public ReceivePipe(uint size, CancellationToken cancellationToken)
        : base(size)
    {
        _source.RunContinuationsAsynchronously = true;
        _source.SetResult(Memory<byte>.Empty);
        _cancellationTokenRegistration = cancellationToken.Register(Cancel);
    }

    public ValueTask<Memory<byte>> GetAvailableMemoryToWrite()
    {
        Memory<byte> memory = GetAvailableMemoryToWriteCore();
        if (!memory.IsEmpty)
            return new(memory);

        if (_source.GetStatus(_source.Version) == ValueTaskSourceStatus.Succeeded)
            _source.Reset();

        return new(this, _source.Version);
    }

    public override void CommitRead(int size)
    {
        if (size == 0)
            return;

        base.CommitRead(size);

        if (_source.GetStatus(_source.Version) == ValueTaskSourceStatus.Pending)
            _source.SetResult(GetAvailableMemoryToWriteCore());
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

    public void Dispose()
    {
        _cancellationTokenRegistration.Unregister();
        Cancel();
    }

    private Memory<byte> GetAvailableMemoryToWriteCore()
    {
        int readIndex = (int)(_readIndex & _mask);
        int writeIndex = (int)(_writeIndex & _mask);

        if (readIndex > writeIndex)
            return _buffer.AsMemory(writeIndex..readIndex);

        if (Length == _buffer.Length)
            return Memory<byte>.Empty;

        return _buffer.AsMemory(writeIndex);
    }

    private void Cancel()
    {
        if (_source.GetStatus(_source.Version) == ValueTaskSourceStatus.Pending)
            _source.SetException(new OperationCanceledException());
    }
}
