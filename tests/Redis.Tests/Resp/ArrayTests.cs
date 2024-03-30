using FluentAssertions;
using Redis.Extensions;
using System.Text;

namespace Redis.Tests.Resp;

public class ArrayTests
{
    [TestCase("0")]
    [TestCase("1")]
    [TestCase("2")]
    [TestCase("3")]
    public void RespParser_parses_BulkStrings(int count)
    {
        var testData = new string[] { "one", "two", "onehundredandninetyeight" };

        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[1024];
        var array = testData[..(count)];
        var msg = $"*{count}\r\n" + string.Join(string.Empty, array.Select(str => str.AsBulkString()));
        Encoding.UTF8.GetBytes(msg, bytes);

        (var result, int offset) = parser.ParseArray(bytes, msg.Length - 1, 0);

        ((ArrayMessage)result).Values
            .Should().BeEquivalentTo(array.Select(str => new BulkStringMessage(str)));
        offset.Should().Be(msg.Length);
    }

    [TestCase("*0\r")]
    [TestCase("*0\n")]
    [TestCase("*1\r\n$1\r")]
    public void RespParser_fails_if_ends_too_soon(string msg)
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[1024];
        Encoding.UTF8.GetBytes(msg, bytes);
        var parse = () => parser.ParseArray(bytes, msg.Length - 1, 0);
        parse.Should().Throw<InvalidOperationException>()
            .WithMessage($"Reached end of buffer before finding an \\r\\n.");
    }

    [TestCase("*a2$1\r\na\r\n")]
    [TestCase("*2a$1\r\na\r\n")]
    public void RespParser_fails_if_no_number(string msg)
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[1024];
        Encoding.UTF8.GetBytes(msg, bytes);
        var parse = () => parser.ParseArray(bytes, msg.Length - 1, 0);
        parse.Should().Throw<InvalidOperationException>()
            .WithMessage($"Array didn't start with a number.");
    }
}