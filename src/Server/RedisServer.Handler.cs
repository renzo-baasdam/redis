using Redis.Client;
using System.Text;

namespace Redis.Server;

public partial class RedisServer
{
    protected virtual async Task<IList<Message>> Handler(Message message, RedisClient client)
    {
        if (message is ArrayMessage array)
        {
            (string command, string[] args) = ParseCommand(array);
            var response = command switch
            {
                "GET" => new List<Message> { Get(args[0]) },
                "SET" => new List<Message> { await Set(args, message) },
                "ECHO" => new List<Message> { new BulkStringMessage(args[0]) },
                "PING" => new List<Message> { new SimpleStringMessage("PONG") },
                "KEYS" => new List<Message> { Keys() },
                "INFO" => new List<Message> { Info(args) },
                "WAIT" => new List<Message> { await Wait(args) },
                "CONFIG" => new List<Message> { Config(args) },
                "PSYNC" => PSync(args),
                "REPLCONF" => ReplConf(args, client) is { } msg
                    ? new List<Message> { msg }
                    : new List<Message>(),
                _ => new List<Message>()
            };
            return response;
        }
        return new List<Message>();
    }

    protected static (string Command, string[] Args) ParseCommand(ArrayMessage message)
    {
        if (message.Values.Any(x => x is not BulkStringMessage)) return ("UNKNOWN", Array.Empty<string>());
        var values = message.Values.Select(x => ((BulkStringMessage)x).Value).ToArray();
        return (values[0].ToUpper(), values[1..]);
    }

    private async Task<SimpleStringMessage> Set(string[] args, Message message)
    {
        var key = args[0];
        var value = args[1];
        _cache[key] = args.Length >= 3 && args[2].ToUpper() == "PX"
            ? new RedisValue
            {
                Value = value,
                Expiration = DateTime.UtcNow.AddMilliseconds(int.Parse(args[3]))
            }
            : new RedisValue { Value = value };
        // todo don't wait for propagation, but still ensure order
        await Propagate(message);
        return new SimpleStringMessage("OK");
    }

    private BulkStringMessage Get(string key)
    {
        if (_cache.TryGetValue(key, out var value) && (value.Expiration is not { } expiration || expiration > DateTime.UtcNow))
            return new BulkStringMessage(value.Value);
        return new NullBulkStringMessage();
    }

    private ArrayMessage Keys()
    {
        var keys = _cache
            .Where(x => x.Value.Expiration is not { } expiration || expiration > DateTime.UtcNow)
            .Select(x => new BulkStringMessage(x.Key))
            .ToList<Message>();
        return new ArrayMessage(keys);
    }

    private BulkStringMessage Info(string[] args)
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

    private async Task<IntegerMessage> Wait(string[] args)
    {
        // ReSharper disable once UnusedVariable
        // ReSharper disable once NotAccessedVariable
        if (args.Length >= 1 && int.TryParse(args[0], out int numreplicas) && int.TryParse(args[1], out int timeout))
        {
            return new IntegerMessage(_replicas.Count);
        }
        // send replconf getack * to replica
        // replica sends replconf ack [offset] <- we know all previous set commands have been processed
        // handle response (update offset on host server for this replica)
        if (args.Length >= 1 && int.TryParse(args[0], out numreplicas) && int.TryParse(args[1], out timeout))
        {
            await Task.Delay(timeout);
        }
        return new IntegerMessage(0);
    }

    private ArrayMessage Config(string[] args)
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
                var pair = new List<Message>
                {
                    new BulkStringMessage(key),
                    new BulkStringMessage(value)
                };
                return new ArrayMessage(pair);
            }
        }
        return new ArrayMessage(new List<Message>());
    }

    protected virtual Message? ReplConf(string[] args, RedisClient client)
    {
        if (args.Length >= 1 && args[0].ToLower() == "ack")
        {
            //OnRaiseReplConfEvent(new ReplConfEvent());
            return null;
        }
        if (args.Length >= 1 && args[0].ToLower() == "listening-port" && int.TryParse(args[1], out var _))
        {
            _replicas.TryAdd(client.Id, client);
            return new SimpleStringMessage("OK");
        }
        var second = args.Length >= 1 && args[0].ToLower() == "capa" && args[1].ToLower() == "psync2";
        if (second) return new SimpleStringMessage("OK");
        return new SimpleStringMessage("OK");
    }

    private List<Message> PSync(string[] args)
    {
        var bytes = Convert.FromBase64String(RedisConfig.EmptyRdb);
        if (args.Length >= 1 && int.TryParse(args[1], out int _))
        {
            var initialResponse = new SimpleStringMessage($"FULLRESYNC {_config.MasterReplicationId} {_config.MasterReplicationOffset}");
            var rdbResponse = new RdbFileMessage(bytes);
            return new List<Message> { initialResponse, rdbResponse };
        }
        return new List<Message> { new NullBulkStringMessage() };
    }
}