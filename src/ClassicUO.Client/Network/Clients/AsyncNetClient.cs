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

using ClassicUO.Network.Clients.Sockets;
using ClassicUO.Utility.Logging;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.Network.Clients;

#nullable enable

internal sealed class AsyncNetClient : NetClient
{
    private const int BUFF_SIZE = 4096;
    private const int SEND_SIZE = BUFF_SIZE;
    private const int RECV_ZIP_SIZE = BUFF_SIZE;
    private const int RECV_UNZIP_SIZE = BUFF_SIZE * 3;

    private readonly byte[] _receiveBuffer = new byte[RECV_ZIP_SIZE];
    private readonly byte[] _decompressionBuffer = new byte[RECV_UNZIP_SIZE];

    private ReceivePipe _receivePipe;
    private SendPipe _sendPipe;
    private uint? _localIP;
    private NetSocket? _socket;
    private CancellationTokenSource _source;
    private Task? _readLoopTask;
    private Task? _writeLoopTask;
    private SocketError _socketError;

    public override uint LocalIP => GetLocalIP();

    public AsyncNetClient()
    {
        _source = new();
        _receivePipe = new(0, CancellationToken.None);
        _sendPipe = new(0, false, CancellationToken.None);
    }

    public override void Connect(string ip, ushort port)
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
            InvokeConnected();
        }
        else
            InvokeDisconnected(SocketError.ConnectionReset);
    }

    public override bool Disconnect(SocketError error = SocketError.Success)
    {
        if (!base.Disconnect(error))
            return false;

        _socketError = error;
        _source.Cancel();

        if (_readLoopTask is { Status: TaskStatus.Running })
            _readLoopTask.Wait();

        if (_writeLoopTask is { Status: TaskStatus.Running })
            _writeLoopTask.Wait();

        _socket!.Dispose();
        _sendPipe.Dispose();
        _receivePipe.Dispose();

        return true;
    }

    public override Span<byte> CollectAvailableData()
    {
        if (!IsConnected && _socketError != SocketError.Success)
        {
            InvokeDisconnected(_socketError);
            _socketError = SocketError.Success;
        }

        return _receivePipe.GetAvailableSpanToRead();
    }

    public override void CommitReadData(int size)
    {
        _receivePipe.CommitRead(size);
    }

    public override bool EnableEncryption(bool login, uint seed)
    {
        if (!base.EnableEncryption(login, seed))
            return false;

        lock (this)
        {
            SendPipe.ChainPipe(ref _sendPipe, true);
        }

        return true;
    }

    public override bool Send(Span<byte> message, bool ignorePlugin = false)
    {
        if(!base.Send(message, ignorePlugin))
            return false;

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

        return true;

        Span<byte> getSpan()
        {
            Span<byte> span = _sendPipe.GetAvailableSpanToWrite();
            if (!span.IsEmpty)
                return span;

            SendPipe.ChainPipe(ref _sendPipe);

            return _sendPipe.GetAvailableSpanToWrite();
        }
    }

    private Memory<byte> Decompress(Memory<byte> buffer)
    {
        if (!_isCompressionEnabled)
            return buffer;

        if (_huffman.Decompress(buffer.Span, _decompressionBuffer, out int size))
            return _decompressionBuffer.AsMemory(..size);

        throw new Exception("Huffman decompression failed");
    }

    private uint GetLocalIP()
    {
        if (!_localIP.HasValue)
            try
            {
                byte[]? addressBytes = _socket?.LocalEndPoint?.Address.MapToIPv4().GetAddressBytes();

                if (addressBytes is { Length: > 0 })
                    _localIP = (uint)(addressBytes[0] | addressBytes[1] << 8 | addressBytes[2] << 16 | addressBytes[3] << 24);

                if (!_localIP.HasValue || _localIP == 0)
                    _localIP = 0x100007f;
            }
            catch (Exception ex)
            {
                Log.Error($"error while retriving local endpoint address: \n{ex}");

                _localIP = 0x100007f;
            }

        return _localIP.Value;
    }

    private async Task<bool> ConnectAsyncCore(Uri uri, bool isWebSocket)
    {
        if (IsConnected)
            Disconnect();

        _source = new();
        _socket = isWebSocket ? new WebSocket() : new TcpSocket();

        CancellationToken token = _source.Token;

        _sendPipe = new(SEND_SIZE, false, token);
        _receivePipe = new(RECV_UNZIP_SIZE, token);

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
            Disconnect();
            return false;
        }
    }

    private async Task ReadLoop(NetSocket socket, ReceivePipe pipe, CancellationToken token)
    {
        int receiveBufferPosition = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                Memory<byte> buffer = _receiveBuffer.AsMemory(receiveBufferPosition);

                int bytesRead = await socket.ReceiveAsync(buffer, token);
                if (bytesRead == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                Statistics.TotalBytesReceived += (uint)bytesRead;

                _encryption?.Decrypt(buffer.Span[..bytesRead]);
                Memory<byte> chunk = Decompress(_receiveBuffer.AsMemory(0, bytesRead + receiveBufferPosition));

                if (chunk.IsEmpty)
                {
                    receiveBufferPosition += bytesRead;
                    continue;
                }

                receiveBufferPosition = 0;

                Memory<byte> target = pipe.GetAvailableMemoryToWrite();

                while (chunk.Length > target.Length)
                {
                    chunk[..target.Length].CopyTo(target);
                    pipe.CommitWrited(target.Length);
                    chunk = chunk[target.Length..];

                    target = pipe.GetAvailableMemoryToWrite();
                }

                chunk.CopyTo(target);
                pipe.CommitWrited(chunk.Length);
            }
        }
        catch (OperationCanceledException)
        { }
        catch (SocketException se)
        {
            if (se.SocketErrorCode != SocketError.ConnectionReset || !ServerDisconnectionExpected)
                Disconnect(se.SocketErrorCode);
        }
        catch
        {
            Disconnect(SocketError.Fault);
        }
    }

    private async Task WriteLoop(NetSocket socket, SendPipe pipe, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                Memory<byte> buffer = pipe.GetAvailableMemoryToRead();

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
                    _encryption?.Encrypt(buffer.Span);

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
            Disconnect();
        }
    }
}