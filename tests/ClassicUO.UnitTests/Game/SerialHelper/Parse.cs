using System;
using ClassicUO.Game.Data;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.SerialHelper
{
    public class Parse
    {
        [Theory]
        [InlineData("1")]
        [InlineData("0x1F")]
        [InlineData("-23")]
        public void Parse_Should_Return_Legal_Number(string input)
        {
            Serial.Parse(input).Value
                .Should().BePositive();
        }

        [Theory]
        [InlineData("0XF")]
        [InlineData("XF")]
        [InlineData("1F")]
        public void Parse_Should_Not_Return_Legal_Number(string input)
        {
            Action act = () => Serial.Parse(input);

            act.Should().Throw<FormatException>();
        }
    }
}