using ClassicUO.Core;
using ClassicUO.IO.Encoders;
using ClassicUO.Utility;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ClassicUO.IO.Buffers;

#nullable enable

public unsafe ref struct SpanReader
{
    private const MethodImplOptions IMPL_OPTION = MethodImplOptions.AggressiveInlining;

    [SuppressMessage("Style", "IDE0032:Use auto property", Justification = "Explicit field access")]
    private readonly ReadOnlySpan<byte> _data;

    public int Position { get; private set; }
    public long Length { get; }
    public readonly int Remaining => (int)(Length - Position);
    public readonly nint StartAddress => (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_data));
    public readonly nint PositionAddress => (nint)((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_data)) + Position);
    public readonly byte this[int index] => _data[index];
    public readonly ReadOnlySpan<byte> Buffer => _data;

    public SpanReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        Length = data.Length;
        Position = 0;
    }

    [MethodImpl(IMPL_OPTION)]
    public void Seek(long p)
    {
        Position = (int)p;
    }

    [MethodImpl(IMPL_OPTION)]
    public void Skip(int count)
    {
        Position += count;
    }

    [MethodImpl(IMPL_OPTION)]
    public byte ReadUInt8()
    {
        Debug.Assert(Position + sizeof(byte) <= Length);

        return _data[Position++];
    }

    [MethodImpl(IMPL_OPTION)]
    public sbyte ReadInt8()
    {
        Debug.Assert(Position + sizeof(sbyte) <= Length);

        return (sbyte)_data[Position++];
    }

    [MethodImpl(IMPL_OPTION)]
    public bool ReadBool()
    {
        return ReadUInt8() != 0;
    }

    [MethodImpl(IMPL_OPTION)]
    public ushort ReadUInt16LE()
    {
        Debug.Assert(Position + sizeof(ushort) <= Length);

        ushort v = BinaryPrimitives.ReadUInt16LittleEndian(_data[Position..]);
        Position += sizeof(ushort);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public short ReadInt16LE()
    {
        Debug.Assert(Position + sizeof(short) <= Length);

        short v = BinaryPrimitives.ReadInt16LittleEndian(_data[Position..]);
        Position += sizeof(short);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public uint ReadUInt32LE()
    {
        Debug.Assert(Position + sizeof(uint) <= Length);

        uint v = BinaryPrimitives.ReadUInt32LittleEndian(_data[Position..]);
        Position += sizeof(uint);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public int ReadInt32LE()
    {
        Debug.Assert(Position + sizeof(int) <= Length);

        int v = BinaryPrimitives.ReadInt32LittleEndian(_data[Position..]);
        Position += sizeof(int);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public ulong ReadUInt64LE()
    {
        Debug.Assert(Position + sizeof(ulong) <= Length);

        ulong v = BinaryPrimitives.ReadUInt64LittleEndian(_data[Position..]);
        Position += sizeof(ulong);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public long ReadInt64LE()
    {
        Debug.Assert(Position + sizeof(long) <= Length);

        long v = BinaryPrimitives.ReadInt64LittleEndian(_data[Position..]);
        Position += sizeof(long);

        return v;
    }

    public int Read(Span<byte> buffer)
    {
        Debug.Assert(Position + buffer.Length <= Length);

        _data.Slice(Position, buffer.Length).CopyTo(buffer);
        Position += buffer.Length;

        return buffer.Length;
    }

    [MethodImpl(IMPL_OPTION)]
    public T Read<T>() where T : unmanaged
    {
        Unsafe.SkipInit(out T v);

        Span<byte> p = new(&v, sizeof(T));
        Read(p);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public ushort ReadUInt16BE()
    {
        Debug.Assert(Position + sizeof(ushort) <= Length);

        ushort v = BinaryPrimitives.ReadUInt16BigEndian(_data[Position..]);
        Position += sizeof(ushort);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public short ReadInt16BE()
    {
        Debug.Assert(Position + sizeof(short) <= Length);

        short v = BinaryPrimitives.ReadInt16BigEndian(_data[Position..]);
        Position += sizeof(short);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public uint ReadUInt32BE()
    {
        Debug.Assert(Position + sizeof(uint) <= Length);

        uint v = BinaryPrimitives.ReadUInt32BigEndian(_data[Position..]);
        Position += sizeof(uint);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public int ReadInt32BE()
    {
        Debug.Assert(Position + sizeof(int) <= Length);

        int v = BinaryPrimitives.ReadInt32BigEndian(_data[Position..]);
        Position += sizeof(int);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public ulong ReadUInt64BE()
    {
        Debug.Assert(Position + sizeof(ulong) <= Length);

        ulong v = BinaryPrimitives.ReadUInt32BigEndian(_data[Position..]);
        Position += sizeof(ulong);

        return v;
    }

    [MethodImpl(IMPL_OPTION)]
    public long ReadInt64BE()
    {
        Debug.Assert(Position + sizeof(long) <= Length);

        long v = BinaryPrimitives.ReadInt64BigEndian(_data[Position..]);
        Position += sizeof(long);

        return v;
    }

    public string ReadString<T>(bool safe = false) where T : ITextEncoder
    {
        Debug.Assert(Position + T.CharSize <= Length);

        int remaining = Remaining;
        int byteLength = remaining - (remaining & T.CharSize - 1);

        ReadOnlySpan<byte> bytes = _data.Slice(Position, byteLength);

        int terminationIndex = T.GetNullTerminatorIndex(bytes);
        if (terminationIndex >= 0)
        {
            byteLength = terminationIndex;
            bytes = bytes[..byteLength];
        }

        string result;

        if (safe)
        {
            char[]? buffer = null;
            scoped Span<char> chars;

            if (byteLength > 256)
            {
                buffer = ArrayPool<char>.Shared.Rent(byteLength >> T.ByteShift);
                chars = buffer;
            }
            else
            {
                chars = stackalloc char[byteLength >> T.ByteShift];
            }

            int charsWritten = T.GetChars(bytes, chars);
            chars = chars[..charsWritten];

            ReadOnlySpan<char> chunk = chars;

            int position = 0;
            int unsafeCharIndex = chunk.IndexOfAnyExceptInRange(StringHelper.FIRST_SAFE_CHAR, StringHelper.LAST_SAFE_CHAR);

            while (unsafeCharIndex >= 0)
            {
                chunk[..unsafeCharIndex].CopyTo(chars[position..]);
                position += unsafeCharIndex;
                chunk = chunk[unsafeCharIndex..];

                unsafeCharIndex = chunk.IndexOfAnyExceptInRange(StringHelper.FIRST_SAFE_CHAR, StringHelper.LAST_SAFE_CHAR);
            }

            if (chunk.Length != chars.Length)
            {
                if (!chunk.IsEmpty)
                {
                    chunk.CopyTo(chars[position..]);
                    position += chunk.Length;
                }

                result = new(chars[..position]);
            }
            else
            {
                result = new(chars);
            }

            if (buffer is not null)
                ArrayPool<char>.Shared.Return(buffer);
        }
        else
        {
            result = T.GetString(bytes[..byteLength]);
        }

        Position += byteLength + (terminationIndex >= 0 ? T.CharSize : 0);

        return result;
    }

    public string ReadFixedString<T>(int charLength, bool safe = false) where T : ITextEncoder
    {
        if (charLength == 0)
            return "";

        ArgumentOutOfRangeException.ThrowIfGreaterThan(charLength, Remaining >> T.ByteShift);

        int totalByteLength = charLength << T.ByteShift;
        int byteLength = totalByteLength;

        ReadOnlySpan<byte> bytes = _data.Slice(Position, byteLength);

        int terminationIndex = T.GetNullTerminatorIndex(bytes);
        if (terminationIndex >= 0)
        {
            byteLength = terminationIndex;
            bytes = bytes[..byteLength];
        }

        string result;

        if (safe)
        {
            char[]? buffer = null;
            scoped Span<char> chars;

            if (byteLength > 256)
            {
                buffer = ArrayPool<char>.Shared.Rent(byteLength >> T.ByteShift);
                chars = buffer.AsSpan();
            }
            else
            {
                chars = stackalloc char[byteLength >> T.ByteShift];
            }

            int charsWritten = T.GetChars(bytes, chars);
            chars = chars[..charsWritten];

            ReadOnlySpan<char> chunk = chars;

            int position = 0;
            int unsafeCharIndex = chunk.IndexOfAnyExceptInRange(StringHelper.FIRST_SAFE_CHAR, StringHelper.LAST_SAFE_CHAR);

            while (unsafeCharIndex >= 0)
            {
                chunk[..unsafeCharIndex].CopyTo(chars[position..]);
                position += unsafeCharIndex;
                chunk = chunk[unsafeCharIndex..];

                unsafeCharIndex = chunk.IndexOfAnyExceptInRange(StringHelper.FIRST_SAFE_CHAR, StringHelper.LAST_SAFE_CHAR);
            }

            if (chunk.Length != chars.Length)
            {
                if (!chunk.IsEmpty)
                {
                    chunk.CopyTo(chars[position..]);
                    position += chunk.Length;
                }

                result = new(chars[..position]);
            }
            else
            {
                result = new(chars);
            }

            if (buffer is not null)
                ArrayPool<char>.Shared.Return(buffer);
        }
        else
        {
            result = T.GetString(bytes[..byteLength]);
        }

        Position += totalByteLength;

        return result;
    }

    [MethodImpl(IMPL_OPTION)]
    public Serial ReadSerial()
    {
        return new(ReadUInt32BE());
    }

    [MethodImpl(IMPL_OPTION)]
    private static int GetIndexOfZero(ReadOnlySpan<byte> span, int sizeT)
    {
        return sizeT switch
        {
            2 => MemoryMarshal.Cast<byte, char>(span).IndexOf('\0') * 2,
            4 => MemoryMarshal.Cast<byte, uint>(span).IndexOf((uint)0) * 4,
            _ => span.IndexOf((byte)0),
        };
    }
}
