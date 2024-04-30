using Redis.Client;

namespace Redis.Entry;

internal record RedisEntry
{
    public virtual bool IsExpired => false;
}

internal record StringEntry : RedisEntry
{
    public string Value { get; init; } = string.Empty;
    public DateTime? Expiration { get; init; }
    public override bool IsExpired => DateTime.UtcNow > Expiration;
}

internal record StreamEntry : RedisEntry
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Value { get; init; } = new();
}
