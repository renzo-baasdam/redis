using Redis.Client;
using Redis.Database;
using Redis.Entry;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Redis.Server;

public partial class RedisServer
{
    protected readonly RedisConfig _config;
    private RedisDatabase? Database { get; set; }
    private readonly Dictionary<string, RedisEntry> _cache = new();
    private readonly TcpListener _server;
    private readonly ConcurrentDictionary<Guid, RedisClient> _replicas = new();

    public RedisServer(RedisConfig config)
    {
        _config = config;
        _server = new TcpListener(IPAddress.Any, _config.Port);
    }

    private string? DatabasePath
    {
        get => _config is { Directory: { } dir, Filename: { } filename }
            ? $"{dir}/{filename}"
            : null;
    }
}