using System;

namespace ClassicUO.IO.Encoders;

public interface ITextEncoder
{
    public abstract static int CharSize { get; }
    public abstract static int ByteShift { get; }

    public abstract static int GetByteCount(ReadOnlySpan<char> source);
    public abstract static int GetBytes(ReadOnlySpan<char> source, Span<byte> target);
}
