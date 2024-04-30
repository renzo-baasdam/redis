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

    public StreamEntry(StreamItem initial)
    {
        Items.Add(initial);
    }

    public bool TryAdd(StreamItem item, [NotNullWhen(false)] out ErrorMessage? msg)
    {
        msg = null;
        if (item.Id <= Items.Last().Id)
        {
            msg = new ErrorMessage("The ID specified in XADD is equal or smaller than the target stream top item");
            return false;
        }
        Items.Add(item);
        return true;
    }
}

public record StreamItem
{
    public StreamId Id { get; init; }
    public Dictionary<string, string> Value { get; init; } = new();
}

public struct StreamId
{
    public ulong MillisecondsTime { get; init; }
    public ulong SequenceNumber { get; init; }

    public static bool TryParse(
        string id,
        [NotNullWhen(true)] out StreamId? streamId,
        [NotNullWhen(false)] out ErrorMessage? msg)
    {
        streamId = null;
        msg = null;
        var split = id.Split("-");
        if (split.Length != 2 || !ulong.TryParse(split[0], out ulong ms) || !ulong.TryParse(split[0], out ulong seq))
        {
            msg = new("ERR Invalid stream ID specified as stream command argument");
            return false;
        }
        if (ms == 0 && seq == 0)
        {
            msg = new("ERR The ID specified in XADD must be greater than 0-0");
            return false;
        }
        streamId = new(ms, seq);
        return true;
    }

    public StreamId(ulong ms, ulong seq)
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
        => (s1 > s2) || (s1 == s2);
    public static bool operator <=(StreamId s1, StreamId s2)
        => (s1 < s2) || (s1 == s2);

    public override bool Equals(object? obj) 
        => obj is StreamId id && this == id;
    public override int GetHashCode()
        => MillisecondsTime.GetHashCode() ^ (SequenceNumber.GetHashCode() << 16);
}