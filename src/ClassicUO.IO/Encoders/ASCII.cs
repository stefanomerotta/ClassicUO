using System.Text;

namespace ClassicUO.IO.Encoders
{
    public sealed class ASCII : ITextEncoder
    {
        public static Encoding Encoding { get; } = Encoding.ASCII;
        public static int CharSize { get; } = 1;
        public static int ByteShift { get; } = 0;

        private ASCII()
        { }
    }
}
