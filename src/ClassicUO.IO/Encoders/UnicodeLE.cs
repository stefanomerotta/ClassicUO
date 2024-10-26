using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ClassicUO.IO.Encoders;

public sealed class UnicodeLE : ITextEncoder
{
    public static int CharSize => 2;
    public static int ByteShift => 1;

    private UnicodeLE()
    { }

    public static int GetByteCount(ReadOnlySpan<char> source)
    {
        return Encoding.Unicode.GetByteCount(source);
    }

    public static int GetBytes(ReadOnlySpan<char> source, Span<byte> target)
    {
        return Encoding.Unicode.GetBytes(source, target);
    }

    public static string GetString(ReadOnlySpan<byte> source)
    {
        return Encoding.Unicode.GetString(source);
    }

    public static int GetChars(ReadOnlySpan<byte> source, Span<char> target)
    {
        return Encoding.Unicode.GetChars(source, target);
    }

    public static int GetNullTerminatorIndex(ReadOnlySpan<byte> source)
    {
        int index = MemoryMarshal.Cast<byte, char>(source).IndexOf('\0');
        if (index == -1)
            return -1;

        return index << ByteShift;
    }
}
