using System;
using System.Text;

namespace ClassicUO.IO.Encoders;

public sealed class UnicodeBE : ITextEncoder
{
    public static int CharSize { get; } = 2;
    public static int ByteShift { get; } = CharSize - 1;

    private UnicodeBE()
    { }

    public static int GetByteCount(ReadOnlySpan<char> source)
    {
        return Encoding.BigEndianUnicode.GetByteCount(source);
    }

    public static int GetBytes(ReadOnlySpan<char> source, Span<byte> target)
    {
        return Encoding.BigEndianUnicode.GetBytes(source, target);
    }
}
