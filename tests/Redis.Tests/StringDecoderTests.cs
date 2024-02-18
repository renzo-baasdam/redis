using FluentAssertions;
using Redis.Database;
using System.Text;

namespace Redis.Tests;

public class StringDecoderTests
{
    [Theory]
    [TestCase("")]
    [TestCase("short string")]
    [TestCase("a longer string")]
    public void parses_length_prefixed(string value)
    {
        int index = 0;
        int length = value.Length;
        var encoded = new byte[256];
        encoded[0] = (byte)value.Length;
        var outputBuffer = Encoding.UTF8.GetBytes(value, 0, value.Length, encoded, 1);
        var decoded = RedisDatabase.DecodeLengthPrefixedString(ref index, encoded);
        decoded.Should().Be(value);
        index.Should().Be(value.Length + 1);
    }
}