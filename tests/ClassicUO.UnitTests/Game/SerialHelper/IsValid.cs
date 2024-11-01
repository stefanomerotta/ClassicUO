﻿using ClassicUO.Core;
using ClassicUO.Game.Data;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.SerialHelper
{
    public class IsValid
    {
        [Theory]
        [InlineData(1)]
        [InlineData(0x1FFFFFFF)]
        [InlineData(0x7FFFFFFF)]
        public void IsValid_Serial_Should_Be_Legal(uint serial)
        {
            new Serial(serial).IsEntity
                .Should()
                .BeTrue();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(0x80000000)]
        public void IsValid_Serial_Should_Not_Be_Legal(uint serial)
        {
            new Serial(serial).IsEntity
                .Should()
                .BeFalse();
        }
    }
}