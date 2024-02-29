using Redis.Database;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace Redis;

internal record RedisValue
{
    public string Value { get; init; } = string.Empty;
    public DateTime? Expiration { get; init; }
}

public partial class RedisServer
{
    private readonly Dictionary<string, RedisValue> _cache = new();
    private readonly RedisConfig _config = new();
    private readonly TcpListener _server = new(IPAddress.Any, 6379);
    private readonly List<int> _replicates = new();
    private RedisDatabase? _database { get; set; }

    public RedisServer(RedisConfig config)
    {
        _config = config;
        _server = new(IPAddress.Any, _config.Port);
    }

    public async Task Start()
    {
        Console.WriteLine("Starting Redis...");
        if (DatabasePath is not null)
        {
            try
            {
                Console.WriteLine($"Loading database from: {DatabasePath}");
                var bytes = File.ReadAllBytes(DatabasePath);
                _database = RedisDatabase.FromBytes(bytes);
                foreach (var kv in _database.Databases[0].Values)
                    _cache[kv.Key] = kv.Value;
                Console.WriteLine($"Loaded database successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading database: {ex.Message}");
            }
        }
        if (_config.Role == "slave" && _config.MasterPort is { } masterPort)
        {
            Connect(masterPort);
        }
        _server.Start();
        Console.WriteLine($"Listing on {_server.LocalEndpoint}");
        int socketNumber = 0;
        while (true)
        {
            var socket = await _server.AcceptSocketAsync();
            Console.WriteLine($"Accepted Socket #{socketNumber}");
            Listen(socket, socketNumber);
            ++socketNumber;
        }
    }

    private async void Connect(int masterPort)
    {
        using var client = new TcpClient();
        try
        {
            var endpoint = new IPEndPoint(LocalhostIP, masterPort);

            await client.ConnectAsync(endpoint);

            // send ping
            var stream = client.GetStream();
            string message = new string[] { "PING" }.AsBulkString();
            byte[] data = Encoding.ASCII.GetBytes(message);
            Console.WriteLine("Sending PING to master");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.ReadAsync(new byte[512]);

            // send REPLCONF listening-port <port>
            stream = client.GetStream();
            message = new string[] { "REPLCONF", "listening-port", _config.Port.ToString() }.AsBulkString();
            data = Encoding.ASCII.GetBytes(message);
            Console.WriteLine("Sending first REPLCONF to master");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.ReadAsync(new byte[512]);

            // send REPLCONF capa psync2
            stream = client.GetStream();
            message = new string[] { "REPLCONF", "capa", "psync2" }.AsBulkString();
            data = Encoding.ASCII.GetBytes(message);
            Console.WriteLine("Sending second REPLCONF to master");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.ReadAsync(new byte[512]);

            // send PSYNC ? -1
            stream = client.GetStream();
            message = new string[] { "PSYNC", "?", "-1" }.AsBulkString();
            data = Encoding.ASCII.GetBytes(message);
            Console.WriteLine("Sending second PSYNC to master");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.ReadAsync(new byte[512]);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            client.GetStream().Close();
            client.Close();
        }
    }

    async void Listen(Socket socket, int socketNumber)
    {
        while (true)
        {
            try
            {
                // receive
                var buffer = new byte[1024];
                await socket.ReceiveAsync(buffer, SocketFlags.None);

                // input bytes to string
                var bufferEnd = Array.IndexOf(buffer, (byte)0);
                var input = Encoding.UTF8.GetString(buffer, 0, bufferEnd);

                // possibly multiple commands in the buffer
                var cmds = input
                    .Split("*", StringSplitOptions.RemoveEmptyEntries)
                    .Select(str => "*" + str);
                foreach (var cmd in cmds)
                {
                    // output string to bytes
                    foreach (var output in Response(cmd))
                    {
                        // log and respond
                        Console.WriteLine(@$"Socket #{socketNumber}. Received: {cmd.ReplaceLineEndings("\\r\\n")}."
                            + $"Response: {Encoding.UTF8.GetString(output).Replace("\r\n", "\\r\\n")}");
                        await socket.SendAsync(output, SocketFlags.None);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    private string Config(string[] lines)
    {
        if (lines[4].ToUpperInvariant() == "GET")
        {
            var arg = lines[6];
            var fetched = arg switch
            {
                RedisConfigKeys.Filename => _config.Filename,
                RedisConfigKeys.Directory => _config.Directory,
                _ => null
            };
            if (fetched is not null) return new string[] { lines[6], fetched }.AsBulkString();
        }
        return "$-1\r\n";
    }

    private string Info(string[] lines)
    {
        if (lines.Length > 4 && lines[4].ToUpperInvariant() == "REPLICATION")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{RedisConfigKeys.Role}:{_config.Role}");
            if (_config.MasterReplicationId is not null) sb.AppendLine($"{RedisConfigKeys.MasterReplicationId}:{_config.MasterReplicationId}");
            sb.AppendLine($"{RedisConfigKeys.MasterReplicationOffset}:{_config.MasterReplicationOffset}");

            return sb.ToString().AsBulkString();

        }
        return "$-1\r\n";
    }

    private string Keys()
    {
        return _cache
            .Where(x => x.Value.Expiration is not { } expiration || expiration > DateTime.UtcNow)
            .Select(x => x.Key)
            .ToArray()
            .AsBulkString();
    }

    private string Get(string key)
    {
        if (_cache.TryGetValue(key, out var value) && (value.Expiration is not { } expiration || expiration > DateTime.UtcNow))
            return value.Value.AsBulkString();
        return "$-1\r\n";
    }

    private string Set(string[] lines, string input)
    {
        var key = lines[4];
        var value = lines[6];
        if (lines.Length > 8 && lines[8].ToUpperInvariant() == "PX")
        {
            _cache[key] = new RedisValue()
            {
                Value = value,
                Expiration = DateTime.UtcNow.AddMilliseconds(int.Parse(lines[10]))
            };
        }
        else
        {
            _cache[key] = new RedisValue() { Value = value };
        }
        Propagate(input);
        return "+OK\r\n";
    }

    private IEnumerable<byte[]> Response(string input)
    {
        var lines = input.Split("\r\n");
        if (lines.Length > 0)
        {
            var arguments = lines[0];
            if (arguments.Length > 1
                && arguments[0] == '*'
                && int.TryParse(arguments[1..], out var numberOfArguments))
            {
                var command = lines[2].ToUpperInvariant();
                if (command == "PSYNC") return PSync(lines);
                var response = command switch
                {
                    "SET" => Set(lines, input),
                    "GET" => Get(lines[4]),
                    "CONFIG" => Config(lines),
                    "REPLCONF" => ReplConf(lines),
                    "INFO" => Info(lines),
                    "KEYS" => Keys(),
                    "ECHO" => lines[4].AsBulkString(),
                    "PING" => "+PONG\r\n",
                    _ => "Unsupported request\r\n"
                };
                return new byte[][] { response.AsUtf8() };
            }
        }
        return new byte[][] { "+Unsupported request\r\n".AsUtf8() };
    }
}