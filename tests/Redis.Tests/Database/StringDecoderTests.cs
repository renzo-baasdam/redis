using FluentAssertions;
using Redis.Database;
using System.Text;

namespace Redis.Tests.Database;

public class StringDecoderTests
{
    [Theory]
    [TestCase("")]
    [TestCase("short string")]
    [TestCase("a longer string")]
    public void parses_length_prefixed(string value)
    {
        int index = 0;
        var encoded = new byte[256];
        encoded[0] = (byte)value.Length;
        var decoded = RedisDatabase.DecodeLengthPrefixedString(ref index, encoded);
        decoded.Should().Be(value);
        index.Should().Be(value.Length + 1);
    }
}