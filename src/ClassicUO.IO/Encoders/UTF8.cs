using System;
using System.Text;

namespace ClassicUO.IO.Encoders;

public sealed class UTF8 : ITextEncoder
{
    public static int CharSize => 1;
    public static int ByteShift => 0;

    private UTF8()
    { }

    public static int GetByteCount(ReadOnlySpan<char> source)
    {
        return Encoding.UTF8.GetByteCount(source);
    }

    public static int GetBytes(ReadOnlySpan<char> source, Span<byte> target)
    {
        return Encoding.UTF8.GetBytes(source, target);
    }

    public static string GetString(ReadOnlySpan<byte> source)
    {
        return Encoding.UTF8.GetString(source);
    }

    public static int GetChars(ReadOnlySpan<byte> source, Span<char> target)
    {
        return Encoding.UTF8.GetChars(source, target);
    }

    public static int GetNullTerminatorIndex(ReadOnlySpan<byte> source)
    {
        return source.IndexOf((byte)'\0');
    }
}
