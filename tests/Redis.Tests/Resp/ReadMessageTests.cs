using FluentAssertions;
using System.Text;

namespace Redis.Tests.Resp;

public class ReadMessageTests
{
    [Test]
    public async Task RespParser_parses_multiple_messages_in_same_stream()
    {
        var bytes = new byte[1024];
        const string simple = "+One\r\n$12\r\n123456789012\r\n*2\r\n+Three\r\n$4\r\nFour\r\n";
        Encoding.UTF8.GetBytes(simple, bytes);

        using var stream = new MemoryStream(bytes);
        var parser = new RespParser(stream);
        (await parser.ReadMessage()).Should().BeEquivalentTo(new SimpleStringMessage("One"));
        (await parser.ReadMessage()).Should().BeEquivalentTo(new BulkStringMessage("123456789012"));
        (await parser.ReadMessage()).Should().BeEquivalentTo(new ArrayMessage(
            new List<Message>
            {
                new SimpleStringMessage("Three"),
                new BulkStringMessage("Four"),
            }));
    }
}