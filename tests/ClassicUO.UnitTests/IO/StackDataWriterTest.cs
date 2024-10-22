using ClassicUO.IO;
using ClassicUO.IO.Encoders;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace ClassicUO.UnitTests.IO
{
    public class StackDataWriterTest
    {
        [Fact]
        public void Write_BigEndian_String_No_Fixed_Size()
        {
            VariableSpanWriter writer = new VariableSpanWriter(32);

            string str = new string('a', 128);

            if (BitConverter.IsLittleEndian)
                writer.WriteString<UnicodeLE>(str);
            else
                writer.WriteString<UnicodeBE>(str);

            Span<char> span = stackalloc char[str.Length + 1]; // '\0'
            str.AsSpan().CopyTo(span);

            Assert.True(MemoryMarshal.AsBytes(span).SequenceEqual(writer.Buffer.Slice(0, writer.BytesWritten)));

            writer.Dispose();
        }


        [Fact]
        public void Write_BigEndian_String_Greater_Fixed_Size_Than_RealString()
        {
            VariableSpanWriter writer = new VariableSpanWriter(32);

            string str = "aaaa";
            int size = 256;

            if (BitConverter.IsLittleEndian)
                writer.WriteFixedString<UnicodeLE>(str, size);
            else
                writer.WriteFixedString<UnicodeBE>(str, size);

            Span<char> span = stackalloc char[size];
            str.AsSpan().CopyTo(span);

            Assert.True(MemoryMarshal.AsBytes(span).SequenceEqual(writer.Buffer.Slice(0, writer.BytesWritten)));

            writer.Dispose();
        }

        [Fact]
        public void Write_BigEndian_String_Less_Fixed_Size_Than_RealString()
        {
            VariableSpanWriter writer = new VariableSpanWriter(32);

            string str = new string('a', 255);
            int size = 239;

            if (BitConverter.IsLittleEndian)
                writer.WriteFixedString<UnicodeLE>(str, size);
            else
                writer.WriteFixedString<UnicodeBE>(str, size);

            Span<char> span = stackalloc char[size];
            str.AsSpan(0, size).CopyTo(span);

            Assert.True(MemoryMarshal.AsBytes(span).SequenceEqual(writer.Buffer.Slice(0, writer.BytesWritten)));

            writer.Dispose();
        }

        [Theory]
        [InlineData("ClassicUO", new byte[] { 0x43, 0x6C, 0x61, 0x73, 0x73, 0x69, 0x63, 0x55, 0x4F, 0x00 })]
        [InlineData("ÀÁÂÃÄÅ", new byte[] { 0xC0, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0x00 })]
        public void Write_CP1252String(string a, byte[] b)
        {
            VariableSpanWriter writer = new VariableSpanWriter();

            writer.WriteString<ASCIICP1215>(a, StringOptions.NullTerminated);

            Assert.True(b.AsSpan().SequenceEqual(writer.Buffer.Slice(0, writer.BytesWritten)));

            writer.Dispose();
        }
    }
}
