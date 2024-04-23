using Redis.Client;

namespace Redis.Extensions;

internal static class RedisClientExtensions
{
    internal static void Log(this RedisClient client, string message)
        => Console.WriteLine($"[{client.Name}] {message}");
}