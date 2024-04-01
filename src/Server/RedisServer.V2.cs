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
    async Task ListenV2(TcpClient client, int socketNumber, string context)
    {
        var stream = client.GetStream();
        var parser = new RespParser(stream);
        while (stream.CanRead)
        {
            try
            {
                await ListenOnceV2(parser, stream, client, socketNumber, context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    async Task ListenOnceV2(RespParser parser, NetworkStream stream, TcpClient client, int socketNumber,
        string context = "default")
    {
        var message = await parser.ReadMessage(context);
        if (message is not null)
        {
            Console.WriteLine(@$"Client #{socketNumber}. Received command: {message.ToString().ReplaceLineEndings("\\r\\n")}.");
            foreach (var output in Handler(message, client))
            {
                Console.WriteLine($"Response: {output.ToString().Replace("\r\n", "\\r\\n")}");
                await stream.WriteAsync(output.ToBytes());
            }
        }
    }

    IList<MessageV2> Handler(MessageV2 message, TcpClient client)
    {
        if (message is ArrayMessage array)
        {
            (var command, var args) = ParseCommand(array);
            var response = command switch
            {
                "GET" => new List<MessageV2>() { GetV2(args[0]) },
                "SET" => new List<MessageV2>() { SetV2(args, message) },
                "ECHO" => new List<MessageV2>() { new BulkStringMessage(args[0]) },
                "PING" => new List<MessageV2>() { new SimpleStringMessage("PONG") },
                "KEYS" => new List<MessageV2>() { KeysV2() },
                "INFO" => new List<MessageV2>() { InfoV2(args) },
                "CONFIG" => new List<MessageV2>() { ConfigV2(args) },
                "PSYNC" => PSyncV2(args),
                "REPLCONF" => ReplConfV2(args, client) is { } msg
                    ? new List<MessageV2>() { msg }
                    : new List<MessageV2>(),
                _ => new List<MessageV2>() { }
            };
            return client != Master || command == "REPLCONF"
                ? response
                : new List<MessageV2>();
        }
        return new List<MessageV2>();
    }

    private (string Command, string[] Args) ParseCommand(ArrayMessage message)
    {
        if (message.Values.Any(x => x is not BulkStringMessage)) return ("UNKNOWN", Array.Empty<string>());
        var values = message.Values.Select(x => ((BulkStringMessage)x).Value).ToArray();
        return (values[0].ToUpper(), values[1..]);
    }

    private SimpleStringMessage SetV2(string[] args, MessageV2 message)
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
        Task.Run(() => Propagate(message));
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

    private MessageV2? ReplConfV2(string[] args, TcpClient client)
    {
        if (args.Length >= 1 && args[0].ToUpper() == "GETACK")
        {
            var values = new List<MessageV2>() {
                new BulkStringMessage("REPLCONF"),
                new BulkStringMessage("ACK"),
                new BulkStringMessage("0"),
            };
            return new ArrayMessage(values);
        }
        if (args.Length >= 1 && args[0].ToLower() == "ACK")
        {
            return null;
        }
        if (args.Length >= 1 && args[0].ToLower() == "listening-port" && int.TryParse(args[1], out var port))
        {
            _replicas.Add(client);
            return new SimpleStringMessage("OK");
        }
        var second = args.Length >= 1 && args[0].ToLower() == "capa" && args[1].ToLower() == "psync2";
        if (second) return new SimpleStringMessage("OK");
        return new SimpleStringMessage("OK");
    }

    private List<MessageV2> PSyncV2(string[] args)
    {
        var bytes = Convert.FromBase64String(RedisConfig.EmptyRdb);
        if (args.Length >= 1 && int.TryParse(args[1], out int offset))
        {
            var initialResponse = new SimpleStringMessage($"FULLRESYNC {_config.MasterReplicationId} {_config.MasterReplicationOffset}");
            var rdbResponse = new RdbFileMessage(bytes);
            return new List<MessageV2>() { initialResponse, rdbResponse };
        }
        return new List<MessageV2>() { new NullBulkStringMessage() };
    }
}