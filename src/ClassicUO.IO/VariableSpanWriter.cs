using ClassicUO.IO.Encoders;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ClassicUO.IO;

#nullable enable

public ref struct VariableSpanWriter : IDisposable
{
    private const MethodImplOptions IMPL_OPTION = MethodImplOptions.AggressiveInlining;

    private Span<byte> _buffer;
    private byte[]? _allocatedBuffer;

    public int BytesWritten { get; private set; }
    public readonly Span<byte> Buffer => _buffer[..BytesWritten];

    public int Position
    {
        [MethodImpl(IMPL_OPTION)]
        readonly get;

        [MethodImpl(IMPL_OPTION)]
        set
        {
            field = value;
            BytesWritten = Math.Max(value, BytesWritten);
        }
    }

    public VariableSpanWriter(int capacity)
    {
        EnsureSize(capacity);
    }

    public VariableSpanWriter(byte packetId, int capacity, bool variablePacketSize = false)
    {
        Debug.Assert(variablePacketSize ? capacity > 2 : capacity > 0);

        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(capacity);
        _allocatedBuffer = newBuffer;
        _buffer = _allocatedBuffer.AsSpan(0, capacity);

        WriteUInt8(packetId);

        if (variablePacketSize)
            Seek(3, SeekOrigin.Begin);
    }

    public VariableSpanWriter(byte packetId, Span<byte> buffer, bool variablePacketSize = false)
    {
        Debug.Assert(variablePacketSize ? buffer.Length > 2 : buffer.Length > 0);

        _buffer = buffer;

        WriteUInt8(packetId);

        if (variablePacketSize)
            Seek(3, SeekOrigin.Begin);
    }

    [MethodImpl(IMPL_OPTION)]
    public void Seek(int position, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin: Position = position; break;
            case SeekOrigin.Current: Position += position; break;
            case SeekOrigin.End: Position = BytesWritten + position; break;
        }

        EnsureSize(Position - _buffer.Length + 1);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt8(byte value)
    {
        EnsureSize(1);
        _buffer[Position++] = value;
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt8(sbyte value)
    {
        EnsureSize(1);
        _buffer[Position++] = (byte)value;
    }

    [MethodImpl(IMPL_OPTION)]
    public unsafe void WriteBool(bool value)
    {
        EnsureSize(1);
        _buffer[Position++] = *(byte*)&value;
    }

    /* Little Endian */

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt16LE(ushort value)
    {
        EnsureSize(sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer[Position..], value);
        Position += sizeof(ushort);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt16LE(short value)
    {
        EnsureSize(sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(_buffer[Position..], value);
        Position += sizeof(short);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt32LE(uint value)
    {
        EnsureSize(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer[Position..], value);
        Position += sizeof(uint);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt32LE(int value)
    {
        EnsureSize(sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(_buffer[Position..], value);
        Position += sizeof(int);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt64LE(ulong value)
    {
        EnsureSize(sizeof(ulong));
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer[Position..], value);
        Position += sizeof(ulong);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt64LE(long value)
    {
        EnsureSize(sizeof(long));
        BinaryPrimitives.WriteInt64LittleEndian(_buffer[Position..], value);
        Position += sizeof(long);
    }

    /* Big Endian */

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt16BE(ushort value)
    {
        EnsureSize(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[Position..], value);
        Position += sizeof(ushort);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt16BE(short value)
    {
        EnsureSize(sizeof(short));
        BinaryPrimitives.WriteInt16BigEndian(_buffer[Position..], value);
        Position += sizeof(short);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt32BE(uint value)
    {
        EnsureSize(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(_buffer[Position..], value);
        Position += sizeof(uint);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt32BE(int value)
    {
        EnsureSize(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(_buffer[Position..], value);
        Position += sizeof(int);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt64BE(ulong value)
    {
        EnsureSize(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(_buffer[Position..], value);
        Position += sizeof(ulong);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt64BE(long value)
    {
        EnsureSize(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(_buffer[Position..], value);
        Position += sizeof(long);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteZero(int count)
    {
        if (count <= 0)
            return;

        EnsureSize(count);
        _buffer.Slice(Position, count).Clear();
        Position += count;
    }

    [MethodImpl(IMPL_OPTION)]
    public void Write(scoped ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
            return;

        EnsureSize(span.Length);
        span.CopyTo(_buffer[Position..]);
        Position += span.Length;
    }

    public void WritePacketLength()
    {
        Seek(1, SeekOrigin.Begin);
        WriteUInt16BE((ushort)BytesWritten);
    }

    [MethodImpl(IMPL_OPTION)]
    public Span<byte> GetSpan(int size)
    {
        EnsureSize(Position + size);
        return _buffer.Slice(Position, size);
    }

    [MethodImpl(IMPL_OPTION)]
    public void Advance(int count)
    {
        Position += count;
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteString<T>(ReadOnlySpan<char> value) where T : ITextEncoder
    {
        if (value.IsEmpty)
            return;

        int byteLength = T.GetByteCount(value);
        EnsureSize(byteLength);

        int bytesWritten = T.GetBytes(value, _buffer[Position..]);
        Position += bytesWritten;
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteString<T>(ReadOnlySpan<char> value, StringOptions options) where T : ITextEncoder
    {
        int byteLength = T.GetByteCount(value);

        bool nullTerminated = options.HasFlag(StringOptions.NullTerminated);
        if (nullTerminated)
            byteLength++;

        if (options.HasFlag(StringOptions.PrependByteSize))
        {
            EnsureSize(byteLength + 2);
            BinaryPrimitives.WriteUInt16BigEndian(_buffer[Position..], (ushort)byteLength);
            Position += 2;
        }
        else
        {
            EnsureSize(byteLength);
        }

        T.GetBytes(value, _buffer[Position..]);
        Position += byteLength;

        if (nullTerminated)
            _buffer[Position - 1] = 0x0;
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteFixedString<T>(ReadOnlySpan<char> value, int byteLength) where T : ITextEncoder
    {
        Debug.Assert(byteLength >= 0);

        if (value.IsEmpty)
        {
            WriteZero(byteLength);
            return;
        }

        EnsureSize(byteLength);

        int stringLength = Math.Min(value.Length, byteLength >> T.ByteShift);
        int bytesWritten = T.GetBytes(value[..stringLength], _buffer[Position..]);
        Position += bytesWritten;

        int remainingBytes = (byteLength << T.ByteShift) - bytesWritten;
        _buffer.Slice(Position, remainingBytes).Clear();
        Position += remainingBytes;
    }
       
    [MethodImpl(IMPL_OPTION)]
    private void EnsureSize(int size)
    {
        int newSize = Position + size;
        if (newSize <= _buffer.Length)
            return;

        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

        if (_allocatedBuffer is not null)
        {
            _buffer[..BytesWritten].CopyTo(newBuffer);
            Return();
        }

        _buffer = _allocatedBuffer = newBuffer;
    }

    [MethodImpl(IMPL_OPTION)]
    private void Return()
    {
        if (_allocatedBuffer is null)
            return;

        ArrayPool<byte>.Shared.Return(_allocatedBuffer);
        _allocatedBuffer = null;
    }

    [MethodImpl(IMPL_OPTION)]
    public void Dispose()
    {
        Return();
    }
}
