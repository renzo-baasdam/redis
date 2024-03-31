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
                "GET" => new List<MessageV2>() { GetV2(args[0]) },
                "SET" => new List<MessageV2>() { SetV2(args) },
                "ECHO" => new List<MessageV2>() { new BulkStringMessage(args[0]) },
                "PING" => new List<MessageV2>() { new SimpleStringMessage("PONG") },
                "KEYS" => new List<MessageV2>() { KeysV2() },
                "INFO" => new List<MessageV2>() { InfoV2(args) },
                "CONFIG" => new List<MessageV2>() { ConfigV2(args) },
                //"REPLCONF" => ReplConf(lines, client),
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
        _cache[key] = args.Length >= 3 && args[2].ToUpper() == "PX"
            ? new RedisValue()
            {
                Value = value,
                Expiration = DateTime.UtcNow.AddMilliseconds(int.Parse(args[3]))
            }
            : new RedisValue() { Value = value };
        //Propagate(args);
        return new SimpleStringMessage("OK");
    }

    private BulkStringMessage GetV2(string key)
    {
        if (_cache.TryGetValue(key, out var value) && (value.Expiration is not { } expiration || expiration > DateTime.UtcNow))
            return new BulkStringMessage(value.Value);
        return new NullBulkStringMessage();
    }

    private ArrayMessage KeysV2()
    {
        var keys = _cache
            .Where(x => x.Value.Expiration is not { } expiration || expiration > DateTime.UtcNow)
            .Select(x => new BulkStringMessage(x.Key))
            .ToList<MessageV2>();
        return new ArrayMessage(keys);
    }

    private BulkStringMessage InfoV2(string[] args)
    {
        if (args.Length >= 1 && args[0].ToLower() == "replication")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{RedisConfigKeys.Role}:{_config.Role}");
            if (_config.MasterReplicationId is not null) sb.AppendLine($"{RedisConfigKeys.MasterReplicationId}:{_config.MasterReplicationId}");
            sb.AppendLine($"{RedisConfigKeys.MasterReplicationOffset}:{_config.MasterReplicationOffset}");
            return new BulkStringMessage(sb.ToString());
        }
        return new NullBulkStringMessage();
    }

    private ArrayMessage ConfigV2(string[] args)
    {
        if (args[0].ToUpper() == "GET")
        {
            var key = args[1];
            var value = key switch
            {
                RedisConfigKeys.Filename => _config.Filename,
                RedisConfigKeys.Directory => _config.Directory,
                _ => null
            };
            if (value is not null)
            {
                var pair = new List<MessageV2>() {
                    new BulkStringMessage(key),
                    new BulkStringMessage(value)
                };
                return new ArrayMessage(pair);
            }
        }
        return new ArrayMessage(new List<MessageV2>());
    }
}