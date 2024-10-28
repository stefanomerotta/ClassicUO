using ClassicUO.Utility.Logging;
using System;
using System.Net;
using System.Net.Sockets;

namespace ClassicUO.Network.Clients
{
    internal sealed class SimpleNetClient : NetClient
    {
        private const int BUFF_SIZE = 4096;
        private const int SEND_SIZE = BUFF_SIZE;
        private const int RECV_ZIP_SIZE = BUFF_SIZE;
        private const int RECV_UNZIP_SIZE = BUFF_SIZE * 3;

        private readonly byte[] _sendBuffer = new byte[SEND_SIZE];
        private readonly byte[] _receiveBuffer = new byte[RECV_ZIP_SIZE];
        private readonly byte[] _decompressionBuffer = new byte[RECV_UNZIP_SIZE];

        private Socket _socket;
        private uint _localIP;
        private int _receiveSize;
        private int _sendPosition;

        public override uint LocalIP => GetLocalIP();

        public SimpleNetClient()
        {
            _socket = new(SocketType.Stream, ProtocolType.Tcp);
        }

        public override Span<byte> CollectAvailableData()
        {
            if (!IsConnected)
                return [];

            if (_socket.Available == 0)
            {
                if (_socket.Poll(1, SelectMode.SelectRead) && _socket.Available == 0)
                    Disconnect(ServerDisconnectionExpected ? SocketError.Success : SocketError.ConnectionReset);

                return [];
            }

            int bytesRead = _socket.Receive(_receiveBuffer.AsSpan());
            if (bytesRead == 0)
            {
                Disconnect(SocketError.ConnectionReset);
                return [];
            }

            Span<byte> span = _receiveBuffer.AsSpan(0, bytesRead);

            _encryption?.Decrypt(span);
            span = _receiveBuffer.AsSpan(0, bytesRead);

            if (_isCompressionEnabled)
            {
                _huffman.Decompress(span, _decompressionBuffer, out int size);
                span = _decompressionBuffer.AsSpan(..size);
            }

            _receiveSize = span.Length;

            return span;
        }

        public override void CommitReadData(int size)
        {
            if (size != _receiveSize)
                throw new NotSupportedException();

            _receiveSize = 0;
        }

        public override void Connect(string ip, ushort port)
        {
            if (ip.StartsWith("ws", StringComparison.InvariantCultureIgnoreCase))
                throw new NotSupportedException($"{nameof(SimpleNetClient)} does not support WebSocket address");

            if (IsConnected)
                return;

            Log.Trace($"Connecting to {ip}:{port}");

            try
            {
                _socket = new(SocketType.Stream, ProtocolType.Tcp);
                _socket.Connect(ip, port);

                IsConnected = true;
                InvokeConnected();
            }
            catch
            {
                IsConnected = false;
                InvokeDisconnected(SocketError.ConnectionReset);
            }
        }

        public override bool Disconnect(SocketError error = SocketError.Success)
        {
            if (!base.Disconnect(error))
                return false;

            _socket.Disconnect(false);
            _receiveSize = 0;
            _sendPosition = 0;
            InvokeDisconnected(error);

            return true;
        }

        public override bool Send(Span<byte> message, bool ignorePlugin = false)
        {
            if (!base.Send(message, ignorePlugin))
                return false;

            _encryption?.Encrypt(message);

            lock (this)
            {
                if (message.Length + _sendPosition > _sendBuffer.Length)
                    Flush();

                message.CopyTo(_sendBuffer.AsSpan(_sendPosition));
                _sendPosition += message.Length;
            }

            Statistics.TotalBytesSent += (uint)message.Length;
            Statistics.TotalPacketsSent++;

            return true;
        }

        public override void Flush()
        {
            try
            {
                Span<byte> span = _sendBuffer.AsSpan(0, _sendPosition);

                while (!span.IsEmpty)
                {
                    int bytesWritten = _socket.Send(span);
                    span = span[bytesWritten..];
                }
            }
            catch (SocketException se)
            {
                Log.Error("socket error when sending:\n" + se);
                Disconnect(se.SocketErrorCode);
            }
            catch (Exception e) when (e.InnerException is SocketException se)
            {
                Log.Error("main exception:\n" + e);
                Log.Error("socket error when sending:\n" + se);

                Disconnect(se.SocketErrorCode);
            }
            catch (Exception e)
            {
                Log.Error("fatal error when sending:\n" + e);

                Disconnect(SocketError.SocketError);
                throw;
            }

            _sendPosition = 0;
        }

        private uint GetLocalIP()
        {
            if (_localIP != 0)
                return _localIP;

            const uint LOCAL_IP = 0x100007f;

            if (_socket.LocalEndPoint is not IPEndPoint ip)
                return LOCAL_IP;

            byte[] bytes = ip.Address.MapToIPv4().GetAddressBytes();
            if (bytes.Length == 0)
                return LOCAL_IP;

            return _localIP = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        }
    }
}
