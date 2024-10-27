using ClassicUO.IO.Buffers;
using ClassicUO.IO.Encoders;
using System;
using System.Text;
using Xunit;

namespace ClassicUO.UnitTests.IO
{
    public class SpanReaderTest
    {
        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_ASCII_With_FixedLength(string str, string result)
        {
            Span<byte> data = Encoding.ASCII.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadFixedString<ASCIICP1215>(str.Length);

            Assert.Equal(s, result);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_ASCII(string str, string result)
        {
            Span<byte> data = Encoding.ASCII.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadString<ASCIICP1215>();

            Assert.Equal(s, result);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_Unicode_LittleEndian_With_FixedLength(string str, string result)
        {
            Span<byte> data = Encoding.Unicode.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadFixedString<UnicodeLE>(str.Length);

            Assert.Equal(s, result);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_Unicode_LittleEndian(string str, string result)
        {
            Span<byte> data = Encoding.Unicode.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadString<UnicodeLE>();

            Assert.Equal(s, result);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_Unicode_BigEndian_With_FixedLength(string str, string result)
        {
            Span<byte> data = Encoding.BigEndianUnicode.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadFixedString<UnicodeBE>(str.Length);

            Assert.Equal(s, result);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_Unicode_BigEndian(string str, string result)
        {
            Span<byte> data = Encoding.BigEndianUnicode.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadString<UnicodeBE>();

            Assert.Equal(s, result);
        }


        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_UTF8_With_FixedLength(string str, string result)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadFixedString<UTF8>(str.Length);

            Assert.Equal(s, result);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_UTF8(string str, string result)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadString<UTF8>();

            Assert.Equal(s, result);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_UTF8_With_FixedLength_Safe(string str, string result)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadFixedString<UTF8>(str.Length, true);

            Assert.Equal(s, result);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("ClassicUO", "ClassicUO")]
        [InlineData("ClassicUO\0", "ClassicUO")]
        [InlineData("\0ClassicUO", "")]
        [InlineData("Classic\0UO", "Classic")]
        [InlineData("Classic\0UO\0", "Classic")]
        [InlineData("Cla\0ssic\0UO\0\0\0\0\0", "Cla")]

        public void Read_UTF8_Safe(string str, string result)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            string s = reader.ReadString<UTF8>(true);

            Assert.Equal(s, result);
        }


        [Theory]
        [InlineData("classicuo\0abc", 3)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining(string str, int remains)
        {
            Span<byte> data = Encoding.ASCII.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadString<ASCIICP1215>();
            Assert.Equal(remains, reader.Remaining);

            if (remains != 0)
            {
                reader.ReadString<ASCIICP1215>();
                Assert.Equal(0, reader.Remaining);
            }
        }

        [Theory]
        [InlineData("classicuo\0abc", 0)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_FixedLength(string str, int remains)
        {
            Span<byte> data = Encoding.ASCII.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadFixedString<ASCIICP1215>(str.Length);
            Assert.Equal(reader.Remaining, remains);

            reader.ReadFixedString<ASCIICP1215>(remains);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("classicuo\0abc", 6)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_Unicode_BigEndian(string str, int remains)
        {
            Span<byte> data = Encoding.BigEndianUnicode.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadString<UnicodeBE>();
            Assert.Equal(remains, reader.Remaining);

            if (reader.Remaining > 0)
            {
                reader.ReadString<UnicodeBE>();
                Assert.Equal(0, reader.Remaining);
            }
        }

        [Theory]
        [InlineData("classicuo\0abc", 0)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_FixedLength_Unicode_BigEndian(string str, int remains)
        {
            Span<byte> data = Encoding.BigEndianUnicode.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadFixedString<UnicodeBE>(str.Length);
            Assert.Equal(reader.Remaining, remains);

            reader.ReadFixedString<UnicodeBE>(remains);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("classicuo\0abc", 6)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_Unicode_LittleEndian(string str, int remains)
        {
            Span<byte> data = Encoding.Unicode.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadString<UnicodeLE>();
            Assert.Equal(remains, reader.Remaining);

            if (reader.Remaining != 0)
            {
                reader.ReadString<UnicodeLE>();
                Assert.Equal(0, reader.Remaining);
            }
        }

        [Theory]
        [InlineData("classicuo\0abc", 0)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_FixedLength_Unicode_LittleEndian(string str, int remains)
        {
            Span<byte> data = Encoding.Unicode.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadFixedString<UnicodeLE>(str.Length);
            Assert.Equal(reader.Remaining, remains);

            reader.ReadFixedString<UnicodeLE>(remains);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("classicuo\0abc", 3)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_UTF8(string str, int remains)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadString<UTF8>();
            Assert.Equal(remains, reader.Remaining);

            if (reader.Remaining != 0)
            {
                reader.ReadString<UTF8>();
                Assert.Equal(0, reader.Remaining);
            }
        }

        [Theory]
        [InlineData("classicuo\0abc", 0)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_FixedLength_UTF8(string str, int remains)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadFixedString<UTF8>(str.Length);
            Assert.Equal(reader.Remaining, remains);

            reader.ReadFixedString<UTF8>(remains);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("classicuo\0abc", 3)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_Unicode_UTF8_Safe(string str, int remains)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadString<UTF8>(true);
            Assert.Equal(remains, reader.Remaining);

            if (reader.Remaining != 0)
            {
                reader.ReadString<UTF8>(true);
                Assert.Equal(0, reader.Remaining);
            }
        }

        [Theory]
        [InlineData("classicuo\0abc", 0)]
        [InlineData("classicuoabc", 0)]
        [InlineData("classicuoabc\0", 0)]
        public void Check_Data_Remaining_FixedLength_UTF8_Safe(string str, int remains)
        {
            Span<byte> data = Encoding.UTF8.GetBytes(str);

            SpanReader reader = new(data);

            reader.ReadFixedString<UTF8>(str.Length, true);
            Assert.Equal(reader.Remaining, remains);

            reader.ReadFixedString<UTF8>(remains, true);
            Assert.Equal(0, reader.Remaining);
        }

        [Theory]
        [InlineData("this is a very long text", 1000)]
        public void Read_More_Data_Than_Remains_ASCII(string str, int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Span<byte> data = Encoding.ASCII.GetBytes(str);
                SpanReader reader = new(data);

                string s = reader.ReadFixedString<ASCIICP1215>(length);
            });
        }

        [Theory]
        [InlineData("this is a very long text", 1000)]
        public void Read_More_Data_Than_Remains_Unicode(string str, int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Span<byte> data = Encoding.BigEndianUnicode.GetBytes(str);
                SpanReader reader = new(data);

                reader.ReadFixedString<UnicodeBE>(length);
            });
        }

        [Theory]
        [InlineData("this is a very long text", 1000)]
        public void Read_More_Data_Than_Remains_UTF8(string str, int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Span<byte> data = Encoding.UTF8.GetBytes(str);
                SpanReader reader = new(data);

                reader.ReadFixedString<UTF8>(length);
            });
        }
    }
}
