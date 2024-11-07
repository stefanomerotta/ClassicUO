using ClassicUO.IO.Buffers;
using ClassicUO.Network.Clients;
using ClassicUO.Network.Encryptions;
using ClassicUO.Network.Packets;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace ClassicUO.Network;

#nullable enable

internal abstract class NetClient
{
    public static NetClient Socket { get; } = new AsyncNetClient();

    protected readonly Huffman _huffman = new();
    protected bool _isCompressionEnabled;
    protected ClientVersion _clientVersion;
    protected Encryption? _encryption;

    public EncryptionType EncryptionType { get; protected set; }
    public bool ServerDisconnectionExpected { get; set; }
    public NetStatistics Statistics { get; }

    public virtual bool IsConnected { get; protected set; }
    public virtual bool IsWebSocket { get; protected set; }
    public abstract uint LocalIP { get; }
    public ClientVersion ClientVersion => _clientVersion;

    public event EventHandler? Connected;
    public event EventHandler<SocketError>? Disconnected;

    protected NetClient()
    {
        Statistics = new NetStatistics(this);
    }

    public abstract Span<byte> CollectAvailableData();
    public abstract void CommitReadData(int size);
    public abstract void Connect(string ip, ushort port);

    public void EnableCompression()
    {
        _isCompressionEnabled = true;
        _huffman.Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Send(in FixedSpanWriter writer, bool ignorePlugin = false)
    {
        Send(writer.Buffer, ignorePlugin);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Send(in VariableSpanWriter writer, bool ignorePlugin = false)
    {
        Send(writer.Buffer, ignorePlugin);
    }

    public virtual bool Send(Span<byte> message, bool ignorePlugin = false)
    {
        if (!IsConnected || message.IsEmpty)
            return false;

        if (!ignorePlugin && !Plugin.ProcessSendPacket(ref message))
            return false;

        if (message.IsEmpty)
            return false;

        PacketLogger.Default?.Log(message, true);

        return true;
    }

    public virtual void Flush() 
    { }

    public virtual bool Disconnect(SocketError error = SocketError.Success)
    {
        if (!IsConnected)
            return false;

        IsConnected = false;
        ServerDisconnectionExpected = false;
        Statistics.Reset();
        _encryption = null;
        _isCompressionEnabled = false;

        return true;
    }

    public virtual EncryptionType Load(ClientVersion clientVersion, EncryptionType encryption)
    {
        _clientVersion = clientVersion;
        EncryptionType = encryption;

        if (encryption == EncryptionType.NONE)
            return encryption;

        EncryptionType = Encryption.GetType(clientVersion);
        Log.Trace("Calculating encryption by client version...");
        Log.Trace($"encryption: {EncryptionType}");

        if (EncryptionType != encryption)
        {
            Log.Warn($"Encryption found: {EncryptionType}");
            encryption = EncryptionType;
        }

        return encryption;
    }

    public virtual bool EnableEncryption(bool login, uint seed)
    {
        if (EncryptionType == EncryptionType.NONE)
            return false;

        _encryption = login ?
            Encryption.CreateForLogin(_clientVersion, seed)
            : Encryption.CreateForGame(EncryptionType, seed);

        return true;
    }

    public void UpdateStatistics(int receivedPacketCount)
    {
        Statistics.TotalPacketsReceived += (uint)receivedPacketCount;
        Statistics.Update();
    }

    public void SendPing()
    {
        if (!IsConnected)
            return;

        Statistics.SendPing();
    }

    public void ReceivePing(byte idx)
    {
        Statistics.PingReceived(idx);
    }

    protected void InvokeConnected()
    {
        Connected?.Invoke(this, EventArgs.Empty);
    }

    protected void InvokeDisconnected(SocketError error)
    {
        Disconnected?.Invoke(this, error);
    }
}