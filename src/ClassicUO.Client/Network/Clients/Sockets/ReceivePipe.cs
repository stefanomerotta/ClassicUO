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

    public override Span<byte> GetAvailableSpanToWrite()
    {
        Span<byte> span = base.GetAvailableSpanToWrite();
        if (!span.IsEmpty)
            return span;

        _event.WaitOne();
        return base.GetAvailableSpanToWrite();
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
}
