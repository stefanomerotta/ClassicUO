using System;

namespace ClassicUO.IO;

[Flags]
public enum StringOptions
{
    None = 0x0,
    NullTerminated = 0x1,
    PrependByteSize = 0x2,
}

public static class StringOptionsExtensions
{
    public static bool HasFlag(this StringOptions options, StringOptions option)
    {
        return (options & option) != 0;
    }
}
