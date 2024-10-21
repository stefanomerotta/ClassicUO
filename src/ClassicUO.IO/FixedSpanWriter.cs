using ClassicUO.Utility;
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

        if(variablePacketSize)
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
    public void WriteSpan(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return;

        value.CopyTo(_buffer[Position..]);
        Position += value.Length;
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

    [MethodImpl(IMPL_OPTION)]
    public void WriteUnicodeLE(string value)
    {
        WriteString<char>(Encoding.Unicode, value, -1);
        WriteUInt16LE(0x0000);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUnicodeLE(string value, int length)
    {
        WriteString<char>(Encoding.Unicode, value, length);
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
    public void WriteUnicodeBE(string value)
    {
        WriteString<char>(Encoding.BigEndianUnicode, value, -1);
        WriteUInt16BE(0x0000);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUnicodeBE(string value, int length)
    {
        WriteString<char>(Encoding.BigEndianUnicode, value, length);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteUTF8(string value, int length)
    {
        WriteString<byte>(Encoding.UTF8, value, length);
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteASCII(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteUInt8(0x00);
            return;
        }

        StringHelper.StringToCp1252Bytes(value, _buffer[Position..]);
        _buffer[Position] = 0x0;
    }

    [MethodImpl(IMPL_OPTION)]
    public void WriteASCII(string? value, int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteZero(Math.Max(length, 1));
            return;
        }

        if (length < 0)
        {
            StringHelper.StringToCp1252Bytes(value, _buffer[Position..], length);
        }
        else
        {
            int bytesWritten = StringHelper.StringToCp1252Bytes(value, _buffer[Position..], length);
            if (bytesWritten < length)
                _buffer.Slice(Position, length - bytesWritten).Clear();
        }
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
    public void Write(ReadOnlySpan<byte> span)
    {
        span.CopyTo(_buffer[Position..]);
        Position += span.Length;
    }

    public readonly void WritePacketLength()
    {
        _buffer[1] = (byte)(BytesWritten >> 8);
        _buffer[2] = (byte)(BytesWritten & byte.MaxValue);
    }

    // Thanks MUO :)
    private void WriteString<T>(Encoding encoding, string str, int length) where T : struct, IEquatable<T>
    {
        int sizeT = Unsafe.SizeOf<T>();

        if (sizeT > 2)
            throw new InvalidConstraintException("WriteString only accepts byte, sbyte, char, short, and ushort as a constraint");

        str ??= string.Empty;

        int byteCount = length > -1 ? length * sizeT : encoding.GetByteCount(str);
        if (byteCount == 0)
            return;

        int charLength = Math.Min(length > -1 ? length : str.Length, str.Length);
        int processed = encoding.GetBytes(str, 0, charLength, _allocatedBuffer!, Position);
        Position += processed;

        if (length > -1)
            WriteZero(length * sizeT - processed);
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
