using System;
using System.Threading;

namespace ClassicUO.Network.Clients.Sockets;

#nullable enable

internal sealed class SendPipe : Pipe, IDisposable
{
    private readonly CancellationTokenRegistration _cancellationTokenRegistration;
    private readonly AutoResetEvent _event;

    public CancellationToken Token { get; }
    public SendPipe? Next { get; private set; }
    public bool Encrypted { get; }

    public SendPipe(uint size, bool encrypted, CancellationToken cancellationToken)
        : base(size)
    {
        _event = new(false);
        _cancellationTokenRegistration = cancellationToken.Register(() => _event.Set());

        Token = cancellationToken;
        Encrypted = encrypted;
    }

    public Memory<byte> GetAvailableMemoryToRead()
    {
        if (Length > 0)
            return GetAvailableMemoryToReadCore();

        if (Next is not null)
            return Memory<byte>.Empty;

        _event.WaitOne();
        return GetAvailableMemoryToReadCore();
    }

    public override void CommitWrited(int size)
    {
        if (size <= 0)
            return;

        base.CommitWrited(size);
        _event.Set();
    }

    public static void ChainPipe(ref SendPipe pipe, bool forceEncryption = false)
    {
        SendPipe old = pipe;
        pipe.Next = new((uint)pipe._buffer.Length, pipe.Encrypted || forceEncryption, pipe.Token);
        pipe = pipe.Next;

        old._event.Set();
    }

    public void Dispose()
    {
        _cancellationTokenRegistration.Unregister();
        _event.Set();
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
