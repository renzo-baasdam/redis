using FluentAssertions;
using System.Text;

namespace Redis.Tests.Resp;

public class SimpleStringTests
{
    [TestCase("a")]
    [TestCase("OK")]
    [TestCase("OK\rOK\nOK")]
    public void RespParser_parses_SimpleStrings(string msg)
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[1024];
        var simple = $"+{msg}\r\n";
        Encoding.UTF8.GetBytes(simple, bytes);
        (var result, int offset) = parser.ParseSimpleString(bytes, simple.Length - 1, 0);
        result.Should().Be(new SimpleStringMessage(msg));
        offset.Should().Be(simple.Length);
    }

    [TestCase("a\r")]
    [TestCase("a\n")]
    [TestCase("\na\r")]
    [TestCase("\n\r")]
    [TestCase("aabbccdd")]
    public void RespParser_fails_if_no_line_end(string msg)
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var bytes = new byte[9]; // 9 so that the aabbccdd case fills the whole array
        var simple = $"+{msg}";
        Encoding.UTF8.GetBytes(simple, bytes);
        var parse = () => parser.ParseSimpleString(bytes, simple.Length - 1, 0);
        parse.Should().Throw<InvalidOperationException>()
            .WithMessage(@"Reached end of buffer before finding an \r\n.");
    }

    [Test]
    public void RespParser_fails_if_empty()
    {
        using var stream = new MemoryStream();
        var parser = new RespParser(stream);
        var parse = () => parser.ParseSimpleString(Array.Empty<byte>(), -1, 0);
        parse.Should().Throw<ArgumentOutOfRangeException>();
    }
}