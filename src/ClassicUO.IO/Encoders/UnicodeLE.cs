using System;
using System.Text;

namespace ClassicUO.IO.Encoders;

public sealed class UnicodeLE : ITextEncoder
{
    public static int CharSize { get; } = 2;
    public static int ByteShift { get; } = CharSize - 1;

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
}
