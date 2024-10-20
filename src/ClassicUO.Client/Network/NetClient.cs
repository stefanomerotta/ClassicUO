#region license

// Copyright (c) 2024, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.Network.Encryption;
using ClassicUO.Network.Sockets;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EncryptionClass = ClassicUO.Network.Encryption.Encryption;

namespace ClassicUO.Network;

#nullable enable

internal sealed class NetClient
{
    private const int BUFF_SIZE = 4096;
    private const int RECV_SIZE = BUFF_SIZE * 3;

    public static NetClient Socket { get; } = new();

    private readonly byte[] _receiveBuffer = new byte[BUFF_SIZE];
    private readonly byte[] _decompressionBuffer = new byte[RECV_SIZE];
    private readonly Huffman _huffman = new();

    private ReceivePipe _receivePipe;
    private SendPipe _sendPipe;
    private bool _isCompressionEnabled;
    private uint? _localIP;
    private NetSocket? _socket;
    private CancellationTokenSource _source;
    private Task? _readLoopTask;
    private Task? _writeLoopTask;
    private ClientVersion _clientVersion;
    private SocketError _socketError;

    public bool IsConnected { get; private set; }
    public bool IsWebSocket { get; private set; }
    public NetStatistics Statistics { get; }
    public EncryptionClass? Encryption { get; private set; }
    public PacketsTable? PacketsTable { get; private set; }
    public bool ServerDisconnectionExpected { get; set; }
    public uint LocalIP => GetLocalIP();
    public EncryptionType EncryptionType { get; private set; }

    public event EventHandler? Connected;
    public event EventHandler<SocketError>? Disconnected;

    public NetClient()
    {
        Statistics = new NetStatistics(this);
        _source = new();

        _receivePipe = new(0);
        _sendPipe = new(0, false, CancellationToken.None);
    }

    public EncryptionType Load(ClientVersion clientVersion, EncryptionType encryption)
    {
        PacketsTable = new PacketsTable(clientVersion);
        _clientVersion = clientVersion;
        EncryptionType = encryption;

        if (encryption == EncryptionType.NONE)
            return encryption;

        EncryptionType = EncryptionClass.GetType(clientVersion);
        Log.Trace("Calculating encryption by client version...");
        Log.Trace($"encryption: {EncryptionType}");

        if (EncryptionType != encryption)
        {
            Log.Warn($"Encryption found: {EncryptionType}");
            encryption = EncryptionType;
        }

        return encryption;
    }

    public void Connect(string ip, ushort port)
    {
        IsWebSocket = ip.StartsWith("ws", StringComparison.InvariantCultureIgnoreCase);
        string addr = $"{(IsWebSocket ? "" : "tcp://")}{ip}:{port}";

        if (!Uri.TryCreate(addr, UriKind.RelativeOrAbsolute, out Uri? uri))
            throw new UriFormatException($"{nameof(NetClient)}::{nameof(Connect)} invalid Uri {addr}");

        Log.Trace($"Connecting to {uri}");

        bool connected = ConnectAsyncCore(uri, IsWebSocket).GetAwaiter().GetResult();

        if (connected)
        {
            IsConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Disconnected?.Invoke(this, SocketError.ConnectionReset);
        }
    }

    public bool Disconnect(SocketError error = SocketError.Success)
    {
        if (!IsConnected)
            return false;

        Cleanup();
        return true;
    }

    private void Cleanup(SocketError error = SocketError.Success)
    {
        IsConnected = false;
        _socketError = error;
        _isCompressionEnabled = false;
        _source.Cancel();

        if (_readLoopTask is { Status: TaskStatus.Running })
            _readLoopTask.Wait();

        if (_writeLoopTask is { Status: TaskStatus.Running })
            _writeLoopTask.Wait();

        _socket!.Dispose();
        _sendPipe.Dispose();
        Statistics.Reset();
        Encryption = null;
    }

    public Span<byte> CollectAvailableData()
    {
        if (!IsConnected && _socketError != SocketError.Success)
        {
            Disconnected?.Invoke(this, _socketError);
            _socketError = SocketError.Success;
        }

        Span<byte> span = _receivePipe.GetAvailableSpanToRead();
        if (!span.IsEmpty || _receivePipe.Next is null)
            return span;

        _receivePipe = _receivePipe.Next;
        return _receivePipe.GetAvailableSpanToRead();
    }

    public void CommitReadData(int size)
    {
        _receivePipe.CommitRead(size);
    }

    public void EnableCompression()
    {
        _isCompressionEnabled = true;
        _huffman.Reset();
    }

    public void EnableEncryption(bool login, uint seed)
    {
        if (EncryptionType == EncryptionType.NONE)
            return;

        Encryption = login ?
            EncryptionClass.CreateForLogin(_clientVersion, seed)
            : EncryptionClass.CreateForGame(EncryptionType, seed);

        lock (this)
        {
            SendPipe oldPipe = _sendPipe;
            SendPipe pipe = _sendPipe.Next = new(BUFF_SIZE, true, _sendPipe.Token);
            _sendPipe = pipe;
            oldPipe.CommitWrited(0);
        }
    }

    public void Send(Span<byte> message, bool ignorePlugin = false)
    {
        if (!IsConnected || message.IsEmpty)
            return;

        if (!ignorePlugin && !Plugin.ProcessSendPacket(ref message))
            return;

        if (message.IsEmpty)
            return;

        PacketLogger.Default?.Log(message, true);

        int messageLength = message.Length;

        lock (this)
        {
            Span<byte> span = getSpan();

            while (span.Length < message.Length)
            {
                message[..span.Length].CopyTo(span);
                _sendPipe.CommitWrited(span.Length);

                message = message[span.Length..];
                span = getSpan();
            }

            message.CopyTo(span);
            _sendPipe.CommitWrited(message.Length);
        }

        Statistics.TotalBytesSent += (uint)messageLength;
        Statistics.TotalPacketsSent++;

        Span<byte> getSpan()
        {
            Span<byte> span = _sendPipe.GetAvailableSpanToWrite();
            if (!span.IsEmpty)
                return span;

            _sendPipe.Next = new(BUFF_SIZE, _sendPipe.Encrypted, _sendPipe.Token);
            _sendPipe = _sendPipe.Next;

            return _sendPipe.GetAvailableSpanToWrite();
        }
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

    public void UpdateStatistics(int receivedPacketCount)
    {
        Statistics.TotalPacketsReceived += (uint)receivedPacketCount;
        Statistics.Update();
    }

    private Span<byte> ProcessCompression(Span<byte> buffer)
    {
        if (!_isCompressionEnabled)
            return buffer;

        if (_huffman.Decompress(buffer, _decompressionBuffer, out int size))
            return _decompressionBuffer.AsSpan(..size);

        return [];
    }

    private uint GetLocalIP()
    {
        if (!_localIP.HasValue)
        {
            try
            {
                byte[]? addressBytes = _socket?.LocalEndPoint?.Address.MapToIPv4().GetAddressBytes();

                if (addressBytes is { Length: > 0 })
                    _localIP = (uint)(addressBytes[0] | (addressBytes[1] << 8) | (addressBytes[2] << 16) | (addressBytes[3] << 24));

                if (!_localIP.HasValue || _localIP == 0)
                    _localIP = 0x100007f;
            }
            catch (Exception ex)
            {
                Log.Error($"error while retriving local endpoint address: \n{ex}");

                _localIP = 0x100007f;
            }
        }

        return _localIP.Value;
    }

    private async Task<bool> ConnectAsyncCore(Uri uri, bool isWebSocket)
    {
        if (IsConnected)
            Cleanup();

        ServerDisconnectionExpected = false;

        _source = new();
        _socket = isWebSocket ? new WebSocket() : new TcpSocket();

        CancellationToken token = _source.Token;

        _sendPipe = new(BUFF_SIZE, false, token);
        _receivePipe = new(RECV_SIZE);

        try
        {
            NetSocket socket = _socket;
            ReceivePipe receivePipe = _receivePipe;
            SendPipe sendPipe = _sendPipe;

            await _socket.ConnectAsync(uri, token);
            
            _readLoopTask = Task.Run(() => ReadLoop(socket, receivePipe, token));
            _writeLoopTask = Task.Run(() => WriteLoop(socket, sendPipe, token));

            return true;
        }
        catch
        {
            Cleanup();
            return false;
        }
    }

    private async Task ReadLoop(NetSocket socket, ReceivePipe pipe, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Memory<byte> buffer = _receiveBuffer.AsMemory();

                int bytesRead = await socket.ReceiveAsync(buffer, token);
                if (bytesRead == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                Span<byte> span = buffer.Span[..bytesRead];

                Statistics.TotalBytesReceived += (uint)span.Length;

                Encryption?.Decrypt(span);
                span = ProcessCompression(span);

                if (span.IsEmpty)
                    throw new Exception("Huffman decompression failed");

                Span<byte> targetSpan = getSpan(ref pipe);

                while (span.Length > targetSpan.Length)
                {
                    span[..targetSpan.Length].CopyTo(targetSpan);
                    pipe.CommitWrited(targetSpan.Length);
                    span = span[targetSpan.Length..];

                    targetSpan = getSpan(ref pipe);
                }

                span.CopyTo(targetSpan);
                pipe.CommitWrited(span.Length);
            }
        }
        catch (OperationCanceledException)
        { }
        catch (SocketException se)
        {
            if (se.SocketErrorCode != SocketError.ConnectionReset || !ServerDisconnectionExpected)
                Cleanup(se.SocketErrorCode);
        }
        catch
        {
            Cleanup(SocketError.Fault);
        }

        static Span<byte> getSpan(ref ReceivePipe pipe)
        {
            Span<byte> span = pipe.GetAvailableSpanToWrite();
            if (!span.IsEmpty)
                return span;

            ReceivePipe newPipe = pipe.Next = new(RECV_SIZE);
            pipe = newPipe;

            return pipe.GetAvailableSpanToWrite();
        }
    }

    private async Task WriteLoop(NetSocket socket, SendPipe pipe, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Memory<byte> buffer = await pipe.GetAvailableMemoryToRead();

                int bufferLength = buffer.Length;
                if (bufferLength == 0)
                {
                    if (pipe.Next is not null)
                    {
                        pipe.Dispose();
                        pipe = pipe.Next;
                    }

                    continue;
                }

                if (pipe.Encrypted)
                    Encryption?.Encrypt(buffer.Span);

                while (!buffer.IsEmpty)
                {
                    int bytesWritten = await socket!.SendAsync(buffer, token);
                    buffer = buffer[bytesWritten..];
                }

                pipe.CommitRead(bufferLength);
            }
        }
        catch (OperationCanceledException)
        { }
        catch (SocketException)
        {
            // ignored: socket errors are handled by ReadLoop
        }
        catch
        {
            Cleanup();
        }
    }

    private record ReadState(NetSocket Socket, ReceivePipe Pipe, CancellationToken Token);

    private record WriteState(NetSocket Socket, SendPipe Pipe, CancellationToken Token);
}