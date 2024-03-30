using FluentAssertions;
using System.Text;

namespace Redis.Tests.Resp;

public class BulkStringTests
{
    [TestCase("$0\r\n\r\n", "")]
    [TestCase("$1\r\na\r\n", "a")]
    [TestCase("$2\r\nab\r\n", "ab")]
    [TestCase("$10\r\naabbccddee\r\n", "aabbccddee")]
    public void RespParser_parses_BulkStrings(string msg, string expected)
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[1024];
        Encoding.UTF8.GetBytes(msg, bytes);
        (var result, int offset) = parser.ParseBulkString(bytes, msg.Length - 1, 0);
        result.Should().Be(new BulkStringMessage(expected));
        offset.Should().Be(msg.Length);
    }

    [TestCase("$2\r\na\r\n")]
    public void RespParser_fails_if_ends_too_soon(string msg)
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[1024];
        Encoding.UTF8.GetBytes(msg, bytes);
        var parse = () => parser.ParseBulkString(bytes, msg.Length - 1, 0);
        parse.Should().Throw<InvalidOperationException>()
            .WithMessage($"Bulk string does not end with \\r\\n.");
    }

    [TestCase("$a2\r\na\r\n")]
    [TestCase("$2a\r\na\r\n")]
    [TestCase("$EOF:something\r\na\r\n")]
    public void RespParser_fails_if_no_number(string msg)
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[1024];
        Encoding.UTF8.GetBytes(msg, bytes);
        var parse = () => parser.ParseBulkString(bytes, msg.Length - 1, 0);
        parse.Should().Throw<InvalidOperationException>()
            .WithMessage($"Bulk string didn't start with a number.");
    }
}