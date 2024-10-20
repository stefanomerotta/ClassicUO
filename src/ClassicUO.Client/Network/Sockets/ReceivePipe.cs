namespace ClassicUO.Network.Sockets;

#nullable enable

internal sealed class ReceivePipe : Pipe
{
    public ReceivePipe? Next { get; set; }

    public ReceivePipe(uint size)
        : base(size)
    { }
}
