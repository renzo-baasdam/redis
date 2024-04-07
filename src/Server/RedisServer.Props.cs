using Redis.Database;
using System.Net.Sockets;
using System.Net;

namespace Redis.Server;

public partial class RedisServer
{
    private readonly Dictionary<string, RedisValue> _cache = new();
    protected readonly RedisConfig _config = new();
    private readonly TcpListener _server = new(IPAddress.Any, 6379);
    private readonly List<TcpClient> _replicas = new();
    private RedisDatabase? _database { get; set; }

    public RedisServer(RedisConfig config)
    {
        _config = config;
        _server = new(IPAddress.Any, _config.Port);
    }

    private string? DatabasePath
    {
        get
        {
            if (_config.Directory is { } dir && _config.Filename is { } filename)
            {
                return $"{dir}/{filename}";
            }
            return null;
        }
    }
}