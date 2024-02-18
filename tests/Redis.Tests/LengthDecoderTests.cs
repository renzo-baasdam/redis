using FluentAssertions;
using Redis.Database;

namespace Redis.Tests;

public class Length_decoder
{
    [Theory]
    [TestCase((uint)0x0)]
    [TestCase((uint)0x1)]
    [TestCase((uint)0x2)]
    [TestCase((uint)0x3F)]
    public void parses_00_header(uint value)
    {
        int index = 0;
        uint header = 0b0000_0000;
        var first = (byte)(value & 0b11_1111 | header);
        var encoded = new byte[] { first };
        var decoded = RedisDatabase.DecodeLength(ref index, encoded);
        decoded.Should().Be(value);
        index.Should().Be(1);
    }

    [Theory]
    [TestCase((uint)0x0)]
    [TestCase((uint)0x1)]
    [TestCase((uint)0x2)]
    [TestCase((uint)0x3FFF)]
    public void parses_01_header(uint value)
    {
        int index = 0;
        uint header = 0b0100_0000;
        var first = (byte)((value >> 8) & 0b11_1111 | header);
        var second = (byte)value;
        var encoded = new byte[] { first, second };
        var decoded = RedisDatabase.DecodeLength(ref index, encoded);
        decoded.Should().Be(value);
        index.Should().Be(2);
    }

    [Theory]
    [TestCase((uint)0x0)]
    [TestCase((uint)0x1)]
    [TestCase((uint)0x2)]
    [TestCase((uint)0xFFFFFFFF)]
    public void parses_10_header(uint value)
    {
        int index = 0;
        uint header = 0b1000_0000;
        var encoded = new byte[] {
            (byte)header,
            (byte)(value >> 24 & 0b1111_1111),
            (byte)(value >> 16 & 0b1111_1111),
            (byte)(value >> 08 & 0b1111_1111),
            (byte)(value >> 00 & 0b1111_1111),
        };
        var decoded = RedisDatabase.DecodeLength(ref index, encoded);
        decoded.Should().Be(value);
        index.Should().Be(5);
    }
}