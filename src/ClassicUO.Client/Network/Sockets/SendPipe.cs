using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ClassicUO.Network.Sockets;

#nullable enable

internal sealed class SendPipe : Pipe, IValueTaskSource<Memory<byte>>, IDisposable
{
    private readonly CancellationTokenRegistration _cancellationTokenRegistration;
    private ManualResetValueTaskSourceCore<Memory<byte>> _source;
    private SendPipe? _next;
    
    public CancellationToken Token { get; }
    public SendPipe? Next { get => _next; set => SetNextPipe(value); }
    public bool Encrypted { get; }

    public SendPipe(uint size, bool encrypted, CancellationToken cancellationToken)
        : base(size)
    {
        Token = cancellationToken;
        Encrypted = encrypted;

        _source.RunContinuationsAsynchronously = true;
        _source.SetResult(Memory<byte>.Empty);

        _cancellationTokenRegistration = cancellationToken.Register(Cancel);
    }

    public ValueTask<Memory<byte>> GetAvailableMemoryToRead()
    {
        Memory<byte> memory = GetAvailableMemoryToReadCore();
        if (!memory.IsEmpty || Next is not null)
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

    private void SetNextPipe(SendPipe? pipe)
    {
        _next = pipe;

        if (pipe is not null && _source.GetStatus(_source.Version) == ValueTaskSourceStatus.Pending)
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

    public void Dispose()
    {
        _cancellationTokenRegistration.Unregister();
        Cancel();
    }

    private void Cancel()
    {
        if (_source.GetStatus(_source.Version) == ValueTaskSourceStatus.Pending)
            _source.SetException(new OperationCanceledException());
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
