using System.Text;

namespace ClassicUO.IO.Encoders;

public sealed class UnicodeBE : ITextEncoder
{
    public static Encoding Encoding { get; } = Encoding.BigEndianUnicode;
    public static int CharSize { get; } = 2;
    public static int ByteShift { get; } = CharSize - 1;

    private UnicodeBE()
    { }
}
