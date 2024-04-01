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
            LoadDatabase();
        if (_config.Role == "slave" && _config.MasterPort is { } masterPort)
        {
            var thread = new Thread(async () => await Connect(masterPort));
            thread.Start();
        }
        await StartServer();

        void LoadDatabase()
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

        async Task StartServer()
        {
            _server.Start();
            Console.WriteLine($"Listing on {_server.LocalEndpoint}");
            int clientNumber = 0;
            while (true)
            {
                var client = await _server.AcceptTcpClientAsync();
                await Task.Delay(100);
                Console.WriteLine($"Established Tcp connection #{clientNumber}");

                var thread = new Thread(async () => await ListenV2(client, clientNumber, $"Client no. {clientNumber}"));
                thread.Start();

                ++clientNumber;
            }
        }
    }

    private async Task Connect(int masterPort)
    {
        try
        {
            var client = new TcpClient();
            var endpoint = new IPEndPoint(LocalhostIP, masterPort);
            await client.ConnectAsync(endpoint);

            var stream = client.GetStream();
            var parser = new RespParser(stream);

            Master = client;

            // handshake
            await Send(stream, new ArrayMessage("PING"));
            await ListenOnceV2(parser, stream, client, -1);
            await Send(stream, new ArrayMessage("REPLCONF", "listening-port", _config.Port.ToString()));
            await ListenOnceV2(parser, stream, client, -1);
            await Send(stream, new ArrayMessage("REPLCONF", "capa", "psync2"));
            await ListenOnceV2(parser, stream, client, -1);
            await Send(stream, new ArrayMessage("PSYNC", "?", "-1"));
            await ListenOnceV2(parser, stream, client, -1);
            await ListenOnceV2(parser, stream, client, -1);
            Console.WriteLine("Finished handling RDB file.");
            var thread = new Thread(async () =>
            {
                await ListenOnceV2(parser, stream, client, -1);
                await ListenV2(Master!, -1, $"Master client");
            });
            thread.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        async Task Send(Stream stream, ArrayMessage msg)
        {
            Console.WriteLine($"Sending cmd to master: {msg.ToString().Replace("\r\n", "\\r\\n")}");
            await stream.WriteAsync(msg.ToBytes());
        }
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