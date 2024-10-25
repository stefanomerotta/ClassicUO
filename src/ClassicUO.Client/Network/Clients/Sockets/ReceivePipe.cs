using System;
using System.Threading;

namespace ClassicUO.Network.Clients.Sockets;

#nullable enable

internal sealed class ReceivePipe : Pipe, IDisposable
{
    private readonly CancellationTokenRegistration _cancellationTokenRegistration;
    private readonly AutoResetEvent _event;

    public ReceivePipe(uint size, CancellationToken cancellationToken)
        : base(size)
    {
        _event = new(false);
        _cancellationTokenRegistration = cancellationToken.Register(() => _event.Set());
    }

    public Memory<byte> GetAvailableMemoryToWrite()
    {
        Memory<byte> memory = GetAvailableMemoryToWriteCore();
        if (!memory.IsEmpty)
            return memory;

        _event.WaitOne();
        return GetAvailableMemoryToWrite();
    }

    public override void CommitRead(int size)
    {
        if (size == 0)
            return;

        base.CommitRead(size);
        _event.Set();
    }

    public void Dispose()
    {
        _cancellationTokenRegistration.Unregister();
        _event.Dispose();
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
}
