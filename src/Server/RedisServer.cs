using Microsoft.CodeAnalysis;
using Redis.Database;
using Redis.Extensions;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Redis.Server;

internal record RedisValue
{
    public string Value { get; init; } = string.Empty;
    public DateTime? Expiration { get; init; }
}

public partial class RedisServer : IDisposable
{
    private readonly Dictionary<string, RedisValue> _cache = new();
    private readonly RedisConfig _config = new();
    private readonly TcpListener _server = new(IPAddress.Any, 6379);
    private readonly List<TcpClient> _replicas = new();
    private TcpClient? Master { get; set; }
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
        int clientNumber = 0;
        while (true)
        {
            var client = await _server.AcceptTcpClientAsync();
            Console.WriteLine($"Established Tcp connection #{clientNumber}");
            Listen(client, clientNumber);
            ++clientNumber;
        }
    }

    private async void Connect(int masterPort)
    {
        try
        {
            var client = new TcpClient();
            var endpoint = new IPEndPoint(LocalhostIP, masterPort);

            await client.ConnectAsync(endpoint);

            Master = client;

            // handshake
            await Send(client, new string[] { "PING" });
            await ListenOnce(client, -1);
            await Send(client, new string[] { "REPLCONF", "listening-port", _config.Port.ToString() });
            await ListenOnce(client, -1);
            await Send(client, new string[] { "REPLCONF", "capa", "psync2" });
            await ListenOnce(client, -1);
            await Send(client, new string[] { "PSYNC", "?", "-1" }, 2);
            Console.WriteLine("Replica listening 1");
            await ListenOnce(Master, -1);
            Listen(Master, -1);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        async Task Send(TcpClient client, string[] msg, int responses = 1)
        {
            var stream = client.GetStream();
            var data = msg.AsBulkString().AsUtf8();
            Console.WriteLine($"Sending cmd: {string.Join(',', msg)} to master");
            await stream.WriteAsync(data, 0, data.Length);
        }
    }

    async void Listen(TcpClient client, int socketNumber)
    {
        while (true)
        {
            try
            {
                await ListenOnce(client, socketNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    async Task ListenOnce(TcpClient client, int socketNumber)
    {
        // receive
        var buffer = new byte[1024];
        var stream = client.GetStream();
        //await Console.Out.WriteLineAsync($"Waiting to read for client {client.Client.RemoteEndPoint}...");
        await stream.ReadAsync(buffer);

        // input bytes to string
        var bufferEnd = Array.IndexOf(buffer, (byte)0);
        if (bufferEnd == 0) { return; }
        var input = Encoding.UTF8.GetString(buffer, 0, bufferEnd);
        Console.WriteLine($"Received input: {input}");
        // parse input as RESP
        var messages = Resp.Parse(input).ToArray();
        foreach (var message in messages)
        {
            if (message is Command cmd)
            {
                Console.WriteLine(@$"Client #{socketNumber}. Received command: {cmd.Original.ReplaceLineEndings("\\r\\n")}.");
                // output string to bytes
                foreach (var output in Response(cmd.Original, client, cmd))
                {
                    // log and respond
                    Console.WriteLine($"Response: {Encoding.UTF8.GetString(output).Replace("\r\n", "\\r\\n")}");
                    await stream.WriteAsync(output);
                }
            }
            else if (message is Response response)
            {
                Console.WriteLine(@$"Client #{socketNumber}. Received response: {response.Original.ReplaceLineEndings("\\r\\n")}.");
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

    private string Set(string input, Command cmd, TcpClient client)
    {
        var key = cmd[1];
        var value = cmd[2];
        _cache[key] = cmd.Length > 4 && cmd[3].ToUpperInvariant() == "PX"
            ? new RedisValue()
            {
                Value = value,
                Expiration = DateTime.UtcNow.AddMilliseconds(int.Parse(cmd[4]))
            }
            : new RedisValue() { Value = value };
        Propagate(input);
        return "+OK\r\n";
    }

    private IEnumerable<byte[]> Response(string input, TcpClient client, Command cmd)
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
                    "SET" => Set(input, cmd, client),
                    "GET" => Get(lines[4]),
                    "CONFIG" => Config(lines),
                    "REPLCONF" => ReplConf(lines, client),
                    "INFO" => Info(lines),
                    "KEYS" => Keys(),
                    "ECHO" => lines[4].AsBulkString(),
                    "PING" => "+PONG\r\n",
                    _ => "-Unsupported request\r\n"
                };
                return client == Master
                    ? new byte[][] { }
                    : new byte[][] { response.AsUtf8() };
            }
        }
        return new byte[][] { "-Unsupported request\r\n".AsUtf8() };
    }

    public void Dispose()
    {
        foreach (var replica in _replicas)
        {
            replica.Dispose();
        }
        Master?.Dispose();
    }
}