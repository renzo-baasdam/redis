using FluentAssertions;
using Redis.Database;

namespace Redis.Tests.Database;

public class LengthDecoder
{
    [TestCase((uint)0x0)]
    [TestCase((uint)0x1)]
    [TestCase((uint)0x2)]
    [TestCase((uint)0x3F)]
    public void parses_00_header(uint value)
    {
        const int index = 0;
        const uint header = 0b0000_0000;
        var first = (byte)(value & 0b11_1111 | header);
        var encoded = new byte[] { first };
        RedisDatabase.DecodeLength(index, encoded).Should().Be((value, 1));
    }

    [TestCase((uint)0x0)]
    [TestCase((uint)0x1)]
    [TestCase((uint)0x2)]
    [TestCase((uint)0x3FFF)]
    public void parses_01_header(uint value)
    {
        const int index = 0;
        const uint header = 0b0100_0000;
        var first = (byte)(value >> 8 & 0b11_1111 | header);
        var second = (byte)value;
        var encoded = new[] { first, second };
        RedisDatabase.DecodeLength(index, encoded).Should().Be((value, 2));
    }

    [TestCase((uint)0x0)]
    [TestCase((uint)0x1)]
    [TestCase((uint)0x2)]
    [TestCase(0xFFFFFFFF)]
    public void parses_10_header(uint value)
    {
        const int index = 0;
        const uint header = 0b1000_0000;
        var encoded = new[]
        {
            (byte)header,
            (byte)(value >> 24 & 0b1111_1111),
            (byte)(value >> 16 & 0b1111_1111),
            (byte)(value >> 08 & 0b1111_1111),
            (byte)(value >> 00 & 0b1111_1111),
        };
        RedisDatabase.DecodeLength(index, encoded).Should().Be((value, 5));
    }
}