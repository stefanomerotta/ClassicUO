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
    public void WriteSpan(scoped ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return;

        EnsureSize(value.Length);
        value.CopyTo(_buffer[Position..]);
        Position += value.Length;
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

        EnsureSize(value.Length + 1);
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
            EnsureSize(value.Length);
            StringHelper.StringToCp1252Bytes(value, _buffer[Position..], length);
        }
        else
        {
            EnsureSize(Math.Min(value.Length, length));
            
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

        EnsureSize(count);
        _buffer.Slice(Position, count).Clear();
        Position += count;
    }

    [MethodImpl(IMPL_OPTION)]
    public void Write(ReadOnlySpan<byte> span)
    {
        EnsureSize(span.Length);
        span.CopyTo(_buffer[Position..]);
        Position += span.Length;
    }

    public void WritePacketLength()
    {
        Seek(1, SeekOrigin.Begin);
        WriteUInt16BE((ushort)BytesWritten);
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

        EnsureSize(byteCount);

        int charLength = Math.Min(length > -1 ? length : str.Length, str.Length);
        int processed = encoding.GetBytes(str, 0, charLength, _allocatedBuffer!, Position);
        Position += processed;

        if (length > -1)
            WriteZero(length * sizeT - processed);
    }

    [MethodImpl(IMPL_OPTION)]
    private void EnsureSize(int size)
    {
        if (Position + size <= _buffer.Length)
            return;

        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(size);

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
