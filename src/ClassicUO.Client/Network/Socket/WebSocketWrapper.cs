using ClassicUO.Utility.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.ArrayPool<byte>;
using TcpSocket = System.Net.Sockets.Socket;

namespace ClassicUO.Network.Socket;

/// <summary>
/// Handles websocket connections to shards that support it. `ws(s)://[hostname]` as the ip in settings.json.
/// For testing see `tools/ws/README.md` 
/// </summary>
internal sealed class WebSocketWrapper : SocketWrapper
{
    private const int MAX_RECEIVE_BUFFER_SIZE = 1024 * 1024; // 1MB
    private const int WS_KEEP_ALIVE_INTERVAL = 15;           // seconds

    private ClientWebSocket _webSocket;
    private TcpSocket _rawSocket;
    private CancellationTokenSource _tokenSource;
    private CircularBuffer _receiveStream;
    private Task _webSocketClientTask;

    public override bool IsConnected => _webSocket?.State is WebSocketState.Connecting or WebSocketState.Open;
    public override EndPoint LocalEndPoint => _rawSocket?.LocalEndPoint;
    public bool IsCanceled => _tokenSource.IsCancellationRequested;

    public override void Send(byte[] buffer, int offset, int count)
    {
        _webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, true, _tokenSource.Token).Wait();
    }

    public override int Read(byte[] buffer)
    {
        lock (_receiveStream)
        {
            return _receiveStream.Dequeue(buffer, 0, buffer.Length);
        }
    }

    public override void Connect(Uri uri)
    {
        if (IsConnected)
            return;

        _receiveStream = new CircularBuffer();
        _tokenSource = new();

        try
        {
            ConnectWebSocketCore(uri, _tokenSource.Token).Wait();

            if (IsConnected)
                InvokeOnConnected();
            else
                InvokeOnError(SocketError.NotConnected);
        }
        catch (AggregateException aex) when (aex.InnerException is WebSocketException wse)
        {
            SocketError error = wse.InnerException?.InnerException switch
            {
                SocketException socketException => socketException.SocketErrorCode,
                _ => SocketError.SocketError
            };

            Log.Error($"Error {wse.GetType().Name} {error} while connecting to {uri} {wse}");
            InvokeOnError(error);
        }
        catch (Exception ex)
        {
            Log.Error($"Unknown Error {ex.GetType().Name} while connecting to {uri} {ex}");
            InvokeOnError(SocketError.SocketError);
        }
    }


    private async Task ConnectWebSocketCore(Uri uri, CancellationToken token)
    {
        // Take control of creating the raw socket, turn off Nagle, also lets us peek at `Available` bytes.
        _rawSocket = new TcpSocket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        _webSocket = new ClientWebSocket();
        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(WS_KEEP_ALIVE_INTERVAL); // ping/pong

        var httpClient = new HttpClient
        (
            new SocketsHttpHandler()
            {
                ConnectCallback = async (context, token) =>
                {
                    try
                    {
                        await _rawSocket.ConnectAsync(context.DnsEndPoint);

                        return new NetworkStream(_rawSocket, ownsSocket: true);
                    }
                    catch
                    {
                        _rawSocket?.Dispose();
                        _rawSocket = null;
                        _webSocket?.Dispose();
                        _webSocket = null;

                        throw;
                    }
                }
            }
        );

        await _webSocket.ConnectAsync(uri, httpClient, token);
        Log.Trace($"Connected WebSocket: {uri}");

        _webSocketClientTask = StartReceiveAsync(token);
    }

    private async Task StartReceiveAsync(CancellationToken token)
    {
        var socket = _webSocket;
        var rawSocket = _rawSocket;
        var buffer = Shared.Rent(4096);
        var position = 0;
        var lastState = WebSocketState.Open;

        try
        {
            while (!token.IsCancellationRequested && socket.State is WebSocketState.Open)
            {
                GrowReceiveBufferIfNeeded(ref buffer, position, position + rawSocket.Available);

                var receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, position, buffer.Length - position), token);

                // Ignoring message types:
                // 1. WebSocketMessageType.Text: shouldn't be sent by the server, though might be useful for multiplexing commands
                // 2. WebSocketMessageType.Close: will be handled by IsConnected
                if (receiveResult.MessageType == WebSocketMessageType.Binary)
                    position += receiveResult.Count;

                if (!receiveResult.EndOfMessage)
                    continue;

                lock (_receiveStream)
                {
                    _receiveStream.Enqueue(buffer, 0, position);
                }

                position = 0;
            }

            lastState = socket.State;
        }
        catch (OperationCanceledException)
        {
            Log.Trace("WebSocket OperationCanceledException on websocket " + (token.IsCancellationRequested ? "(was requested)" : "(remote cancelled)"));
        }
        catch (Exception e)
        {
            Log.Trace($"WebSocket error in StartReceiveAsync {e}");
            socket.Abort();

            InvokeOnError(SocketError.SocketError);
        }
        finally
        {
            Shared.Return(buffer);
            socket.Dispose();
        }

        if (!token.IsCancellationRequested)
        {
            if (lastState == WebSocketState.CloseReceived)
                InvokeOnDisconnected();
            else
                InvokeOnError(SocketError.ConnectionReset);
        }
    }

    // This is probably unnecessary, but WebSocket frames can be up to 2^63 bytes so we put some cap on it, yet to see packets larger than 4KB come through.
    // We peek the raw tcp socket available bytes, grow if the frame is bigger, we're naively assuming no compression.
    private void GrowReceiveBufferIfNeeded(ref byte[] buffer, int oldLength, int requiredLength)
    {
        if (requiredLength <= buffer.Length)
            return;

        if (requiredLength > MAX_RECEIVE_BUFFER_SIZE)
            throw new SocketException((int)SocketError.MessageSize, $"WebSocket message frame too large: {_rawSocket.Available} > {MAX_RECEIVE_BUFFER_SIZE}");

        Log.Trace($"WebSocket growing receive buffer {buffer.Length} bytes to {_rawSocket.Available} bytes");

        var old = buffer;

        buffer = Shared.Rent(requiredLength);
        Array.Copy(old, 0, buffer, 0, oldLength);

        Shared.Return(old);
    }

    public override void Disconnect()
    {
        _tokenSource.Cancel();

        if (_webSocketClientTask != null)
        {
            _webSocketClientTask.Wait();
            _webSocketClientTask = null;
        }
    }

    public override void Dispose()
    {
        if (_webSocketClientTask != null)
            Disconnect();
    }
}