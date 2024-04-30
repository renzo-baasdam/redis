using System.Diagnostics.CodeAnalysis;

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
    public List<StreamItem> Items { get; init; } = new();

    public static bool TryCreate(
        string id, 
        Dictionary<string, string> value, 
        [NotNullWhen(true)] out StreamEntry? entry,
        [NotNullWhen(false)] out ErrorMessage? msg)
    {
        entry = new StreamEntry();
        if(!entry.TryAdd(id, value, out msg))
            return false;
        return true;
    }

    public bool TryAdd(string id, Dictionary<string, string> value, [NotNullWhen(false)] out ErrorMessage? msg)
    {
        if (!TryParseId(id, out var streamId, out msg))
        {
            return false;
        }
        if (Items.Count > 0 && streamId <= Items.Last().Id)
        {
            msg = new("ERR The ID specified in XADD is equal or smaller than the target stream top item");
            return false;
        }
        Items.Add(new StreamItem(streamId.Value, value));
        return true;
    }

    private bool TryParseId(
        string id,
        [NotNullWhen(true)] out StreamId? streamId,
        [NotNullWhen(false)] out ErrorMessage? msg)
    {
        streamId = null;
        msg = null;
        if (id == "*")
        {
            streamId = new(DateTime.UtcNow.Ticks, 0);
            return true;
        }

        var split = id.Split("-");
        if (split.Length != 2 || !long.TryParse(split[0], out long ms))
        {
            msg = new("ERR Invalid stream ID specified as stream command argument");
            return false;
        }
        if (split[1] == "*")
        {
            if (Items.LastOrDefault()?.Id is { } oldId && oldId.MillisecondsTime == ms)
                streamId = new(ms, oldId.SequenceNumber + 1);
            if (ms == 0) streamId = new(ms, 1);
            else streamId = new(ms, 0);
            return true;
        }
        if (split.Length != 2 || !long.TryParse(split[0], out ms) || !long.TryParse(split[1], out long seq))
        {
            msg = new("ERR Invalid stream ID specified as stream command argument");
            return false;
        }
        if (new StreamId(ms, seq) <= new StreamId(0, 0))
        {
            msg = new("ERR The ID specified in XADD must be greater than 0-0");
            return false;
        }
        streamId = new(ms, seq);
        return true;
    }
}

public record StreamItem
{
    public StreamId Id { get; init; }
    public Dictionary<string, string> Value { get; init; } = new();
    public StreamItem(StreamId id, Dictionary<string, string> value)
    {
        Id = id;
        Value = value;
    }
}

public readonly struct StreamId
{
    public long MillisecondsTime { get; }
    public long SequenceNumber { get; }

    public StreamId(long ms, long seq)
    {
        MillisecondsTime = ms;
        SequenceNumber = seq;
    }

    public static bool operator ==(StreamId s1, StreamId s2)
        => s1.MillisecondsTime == s2.MillisecondsTime && s1.SequenceNumber == s2.SequenceNumber;
    public static bool operator !=(StreamId s1, StreamId s2)
        => s1.MillisecondsTime != s2.MillisecondsTime || s1.SequenceNumber != s2.SequenceNumber;
    public static bool operator <(StreamId s1, StreamId s2)
        => s1.MillisecondsTime < s2.MillisecondsTime || s1.MillisecondsTime == s2.MillisecondsTime && s1.SequenceNumber < s2.SequenceNumber;
    public static bool operator >(StreamId s1, StreamId s2)
        => s1.MillisecondsTime > s2.MillisecondsTime || s1.MillisecondsTime == s2.MillisecondsTime && s1.SequenceNumber > s2.SequenceNumber;
    public static bool operator >=(StreamId s1, StreamId s2)
        => s1 > s2 || s1 == s2;
    public static bool operator <=(StreamId s1, StreamId s2)
        => s1 < s2 || s1 == s2;

    public override bool Equals(object? obj)
        => obj is StreamId id && this == id;
    public override int GetHashCode()
        => MillisecondsTime.GetHashCode() ^ (SequenceNumber.GetHashCode() << 16);
}