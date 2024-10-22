using System.Text;

namespace ClassicUO.IO.Encoders;

public sealed class UTF8 : ITextEncoder
{
    public static Encoding Encoding { get; } = Encoding.UTF8;
    public static int CharSize { get; } = 1;
    public static int ByteShift { get; } = CharSize - 1;

    private UTF8()
    { }
}
