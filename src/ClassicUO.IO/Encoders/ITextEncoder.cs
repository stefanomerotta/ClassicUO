using System.Text;

namespace ClassicUO.IO.Encoders;

public interface ITextEncoder
{
    public abstract static Encoding Encoding { get; }
    public abstract static int CharSize { get; }
    public abstract static int ByteShift { get; }
}
