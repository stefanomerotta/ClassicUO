﻿using System;
using System.Text;

namespace ClassicUO.IO.Encoders;

public sealed class UTF8 : ITextEncoder
{
    public static int CharSize { get; } = 1;
    public static int ByteShift { get; } = CharSize - 1;

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
}
