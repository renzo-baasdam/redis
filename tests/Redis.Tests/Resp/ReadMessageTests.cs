using FluentAssertions;
using System.Text;

namespace Redis.Tests.Resp;

public class ReadMessageTests
{
    [Test]
    public async Task RespParser_parses_multiple_messages_in_same_stream()
    {
        var bytes = new byte[1024];
        var simple = $"+One\r\n+Two\r\n+Three\r\n";
        Encoding.UTF8.GetBytes(simple, bytes);

        using var stream = new MemoryStream(bytes);
        var parser = new RespParser(stream);
        (await parser.ReadMessage()).Should().Be(new SimpleStringMessage("One"));
        (await parser.ReadMessage()).Should().Be(new SimpleStringMessage("Two"));
        (await parser.ReadMessage()).Should().Be(new SimpleStringMessage("Three"));
    }
}