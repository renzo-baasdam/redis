using Microsoft.CodeAnalysis;
using Redis.Database;
using Redis.Extensions;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Redis.Server;
public partial class RedisServer : IDisposable
{
    async void ListenV2(TcpClient client, int socketNumber)
    {
        var stream = client.GetStream();
        var parser = new RespParser(stream);
        while (true)
        {
            try
            {
                await ListenOnceV2(parser, stream, socketNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    async Task ListenOnceV2(RespParser parser, NetworkStream stream, int socketNumber)
    {
        // parse input as RESP
        var message = await parser.ReadMessage();
        if (message is not null)
        {
            Console.WriteLine(@$"Client #{socketNumber}. Received command: {message.ToString().ReplaceLineEndings("\\r\\n")}.");
            // output string to bytes
            foreach (var output in Handler(message))
            {
                // log and respond
                Console.WriteLine($"Response: {output.ToString().Replace("\r\n", "\\r\\n")}");
                await stream.WriteAsync(Encoding.UTF8.GetBytes(output.ToString()));
            }
        }
        /*else if (message is Response response)
        {
            Console.WriteLine(@$"Client #{socketNumber}. Received response: {response.Original.ReplaceLineEndings("\\r\\n")}.");
        }*/
    }

    IList<MessageV2> Handler(MessageV2 message)
    {
        if (message is ArrayMessage array)
        {
            (var command, var args) = ParseCommand(array);
            return command switch
            {
                "SET" => new List<MessageV2>() { SetV2(args) },
                _ => new List<MessageV2>() { }
            };
        }
        return new List<MessageV2>();
    }

    private (string Command, string[] Args) ParseCommand(ArrayMessage message)
    {
        if (message.Values.Any(x => x is not BulkStringMessage)) return ("UNKNOWN", Array.Empty<string>());
        var values = message.Values.Select(x => ((BulkStringMessage)x).Value).ToArray();
        return (values[0].ToUpper(), values[1..]);
    }

    private SimpleStringMessage SetV2(string[] args)
    {
        var key = args[0];
        var value = args[1];
        _cache[key] = args.Length >= 4 && args[3].ToUpper() == "PX"
            ? new RedisValue()
            {
                Value = value,
                Expiration = DateTime.UtcNow.AddMilliseconds(int.Parse(args[4]))
            }
            : new RedisValue() { Value = value };
        //Propagate(args);
        return new SimpleStringMessage("OK");
    }
}