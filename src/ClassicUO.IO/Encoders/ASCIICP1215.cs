using ClassicUO.Utility;
using System;

namespace ClassicUO.IO.Encoders;

public sealed class ASCIICP1215 : ITextEncoder
{
    public static int CharSize => 1;
    public static int ByteShift => 0;

    private ASCIICP1215()
    { }

    public static int GetByteCount(ReadOnlySpan<char> source)
    {
        return source.Length;
    }

    public static int GetBytes(ReadOnlySpan<char> source, Span<byte> target)
    {
        return StringHelper.StringToCp1252Bytes(source, target);
    }

    public static string GetString(ReadOnlySpan<byte> source)
    {
        return StringHelper.Cp1252ToString(source);
    }

    public static int GetChars(ReadOnlySpan<byte> source, Span<char> target)
    {
        return StringHelper.Cp1252ToChars(source, target);
    }

    public static int GetNullTerminatorIndex(ReadOnlySpan<byte> source)
    {
        return source.IndexOf((byte)'\0');
    }
}
