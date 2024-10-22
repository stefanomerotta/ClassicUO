using ClassicUO.Utility;
using System;

namespace ClassicUO.IO.Encoders;

public sealed class ASCIICP1215 : ITextEncoder
{
    public static int CharSize { get; } = 1;
    public static int ByteShift { get; } = CharSize - 1;

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
}
