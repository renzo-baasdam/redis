using Redis.Database;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace Redis.Server;

public class ReplConfEvent
{
    public int Id { get; init; }
}

public class WaitListener
{
    public DateTime Expiration { get; set; }
}

public partial class RedisServer
{
    private readonly Dictionary<string, RedisValue> _cache = new();
    protected readonly RedisConfig _config = new();
    private readonly TcpListener _server = new(IPAddress.Any, 6379);
    private readonly List<TcpClient> _replicas = new();

    private event EventHandler<ReplConfEvent> RaiseReplConfEvent;
    private readonly ConcurrentDictionary<TcpClient, WaitListener> _waiters = new();

    protected virtual void OnRaiseReplConfEvent(ReplConfEvent e)
    {
        // Make a temporary copy of the event to avoid possibility of
        // a race condition if the last subscriber unsubscribes
        // immediately after the null check and before the event is raised.
        EventHandler<ReplConfEvent> raiseEvent = RaiseReplConfEvent;

        // Event will be null if there are no subscribers
        if (raiseEvent != null)
        {
            // Format the string to send inside the CustomEventArgs parameter
            //e.Message += $" at {DateTime.Now}";

            // Call to raise the event.
            raiseEvent(this, e);
        }
    }

    private void HandleRaiseReplConfEvent(object? sender, ReplConfEvent e)
    {
        Console.WriteLine($"Handled event {e.Id}!");
    }

    private RedisDatabase? _database { get; set; }

    public RedisServer(RedisConfig config)
    {
        _config = config;
        _server = new(IPAddress.Any, _config.Port);
        RaiseReplConfEvent += HandleRaiseReplConfEvent;
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