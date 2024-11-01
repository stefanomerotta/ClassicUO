using ClassicUO.Core;
using ClassicUO.IO.Encoders;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ClassicUO.IO.Buffers;

#nullable enable

public ref struct FixedSpanWriter : IDisposable
{
    private const MethodImplOptions IMPL_OPTION = MethodImplOptions.AggressiveInlining;

    private readonly Span<byte> _buffer;
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

    public FixedSpanWriter(int capacity)
    {
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(capacity);
        _allocatedBuffer = newBuffer;
        _buffer = _allocatedBuffer.AsSpan(0, capacity);
    }

    public FixedSpanWriter(Span<byte> buffer)
    {
        _buffer = buffer;
    }

    public FixedSpanWriter(byte packetId, int capacity, bool variablePacketSize = false)
    {
        Debug.Assert(variablePacketSize ? capacity > 2 : capacity > 0);

        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(capacity);
        _allocatedBuffer = newBuffer;
        _buffer = _allocatedBuffer.AsSpan(0, capacity);

        WriteUInt8(packetId);

        if (variablePacketSize)
            Seek(3, SeekOrigin.Begin);
    }

    public FixedSpanWriter(byte packetId, Span<byte> buffer, bool variablePacketSize = false)
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
            case SeekOrigin.Begin:
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position, _buffer.Length);
                Position = position;
                break;
            case SeekOrigin.Current:
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position, _buffer.Length - Position);
                ArgumentOutOfRangeException.ThrowIfLessThan(position, Position);
                Position += position;
                break;
            case SeekOrigin.End:
                ArgumentOutOfRangeException.ThrowIfLessThan(position, -_buffer.Length);
                Position -= _buffer.Length - position;
                break;
        }
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt8(byte value)
    {
        _buffer[Position++] = value;
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt8(sbyte value)
    {
        _buffer[Position++] = (byte)value;
    }

    [MethodImpl(IMPL_OPTION)]
    public unsafe void WriteBool(bool value)
    {
        _buffer[Position++] = *(byte*)&value;
    }

    /* Little Endian */

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt16LE(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer[Position..], value);
        Position += sizeof(ushort);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt16LE(short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(_buffer[Position..], value);
        Position += sizeof(short);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt32LE(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer[Position..], value);
        Position += sizeof(uint);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt32LE(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_buffer[Position..], value);
        Position += sizeof(int);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt64LE(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer[Position..], value);
        Position += sizeof(ulong);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt64LE(long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_buffer[Position..], value);
        Position += sizeof(long);
    }

    /* Big Endian */

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt16BE(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[Position..], value);
        Position += sizeof(ushort);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt16BE(short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(_buffer[Position..], value);
        Position += sizeof(short);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt32BE(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_buffer[Position..], value);
        Position += sizeof(uint);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt32BE(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_buffer[Position..], value);
        Position += sizeof(int);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUInt64BE(ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(_buffer[Position..], value);
        Position += sizeof(ulong);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteInt64BE(long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(_buffer[Position..], value);
        Position += sizeof(long);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteZero(int count)
    {
        if (count <= 0)
            return;

        _buffer.Slice(Position, count).Clear();
        Position += count;
    }

    [MethodImpl(IMPL_OPTION)]
    public void Write(scoped ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
            return;

        span.CopyTo(_buffer[Position..]);
        Position += span.Length;
    }

    [MethodImpl(IMPL_OPTION)]
    public readonly void WritePacketLength()
    {
        _buffer[1] = (byte)(BytesWritten >> 8);
        _buffer[2] = (byte)(BytesWritten & byte.MaxValue);
    }

    [MethodImpl(IMPL_OPTION)]
    public readonly Span<byte> GetSpan(int size)
    {
        return _buffer.Slice(Position, size);
    }

    [MethodImpl(IMPL_OPTION)]
    public void Advance(int count)
    {
        Position += count;
    }

    [MethodImpl(IMPL_OPTION)]
    public Span<byte> Reserve(int size)
    {
        Span<byte> span = _buffer.Slice(Position, size);
        Position += size;

        return span;
    }

    [MethodImpl(IMPL_OPTION)]
    public int WriteString<T>(ReadOnlySpan<char> value) where T : ITextEncoder
    {
        if (value.IsEmpty)
            return 0;

        int bytesWritten = T.GetBytes(value, _buffer[Position..]);
        Position += bytesWritten;

        return bytesWritten;
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteFixedString<T>(ReadOnlySpan<char> value, int charLength) where T : ITextEncoder
    {
        Debug.Assert(charLength >= 0);

        if (value.IsEmpty)
        {
            WriteZero(charLength << T.ByteShift);
            return;
        }

        int bytesLength = charLength << T.ByteShift;

        charLength = Math.Min(value.Length, charLength);
        int bytesWritten = T.GetBytes(value[..charLength], _buffer[Position..]);
        Position += bytesWritten;

        int remainingBytes = bytesLength - bytesWritten;
        WriteZero(remainingBytes);
    }

    [MethodImpl(IMPL_OPTION)]
    public int WriteString<T>(in SpanInterpolatedStringHandler text) where T : ITextEncoder
    {
        if (!text.Success)
            throw new Exception("Error while trying to write text to span");

        return WriteString<T>(text.Span);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteFixedString<T>(in SpanInterpolatedStringHandler text, int charLength) where T : ITextEncoder
    {
        if (!text.Success)
            throw new Exception("Error while trying to write text to span");

        WriteFixedString<T>(text.Span, charLength);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteSerial(Serial serial)
    {
        WriteUInt32BE(serial.Value);
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
