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
    public List<StreamItem> Items { get; } = new();
    public StreamId? LastId => Items.LastOrDefault()?.Id;

    public static bool TryCreate(
        string id,
        Dictionary<string, string> value,
        [NotNullWhen(true)] out StreamEntry? entry,
        [NotNullWhen(false)] out ErrorMessage? msg)
    {
        entry = new StreamEntry();
        return entry.TryAdd(id, value, out msg);
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

    public List<StreamItem> GetRange(StreamRangeCondition streamRangeCondition)
    {
        var range = new List<StreamItem>();
        // todo binary search
        for (int i = 0; i < Items.Count; i++)
        {
            if (streamRangeCondition.IsSatisfiedBy(Items[i].Id))
            {
                range.Add(Items[i]);
            }
        }
        return range;
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
            streamId = new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0);
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
            else if (ms == 0) streamId = new(ms, 1);
            else streamId = new(ms, 0);
            return true;
        }
        if (!long.TryParse(split[1], out long seq))
        {
            msg = new("ERR Invalid stream ID specified as stream command argument");
            return false;
        }
        if (new StreamId(ms, seq) <= StreamId.MinValue)
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
    public StreamId Id { get; }
    public Dictionary<string, string> Value { get; init; } = new();

    public StreamItem(StreamId id, Dictionary<string, string> value)
    {
        Id = id;
        Value = value;
    }

    public Message AsMessage()
    {
        var kvs = Value
            .SelectMany(x => new string[] { x.Key, x.Value })
            .Select(x => new BulkStringMessage(x))
            .ToList<Message>();
        return new ArrayMessage(new List<Message>
        {
            new BulkStringMessage(Id.ToString()),
            new ArrayMessage(kvs)
        });
    }
}

public class StreamRangeCondition
{
    private Func<StreamId, bool> LowerBoundCondition { get; set; }
    private Func<StreamId, bool> UpperBoundCondition { get; set; }

    public bool IsSatisfiedBy(StreamId id) => LowerBoundCondition(id) && UpperBoundCondition(id);

    public static bool TryCreate(
        string lb,
        string ub,
        [NotNullWhen(true)] out StreamRangeCondition? range,
        [NotNullWhen(false)] out ErrorMessage? msg)
    {
        range = null;
        msg = null;
        var lbOpen = false;
        var ubOpen = false;
        Func<StreamId, bool> lowerBoundCondition;
        Func<StreamId, bool> upperBoundCondition;
        if (lb[0] == '(')
        {
            lbOpen = true;
            lb = lb[1..];
        }
        if (ub[0] == '(')
        {
            ubOpen = true;
            ub = ub[1..];
        }
        if (lb == "-") lowerBoundCondition = _ => true;
        else if (long.TryParse(lb, out long lbMs))
        {
            lowerBoundCondition = lbOpen
                ? id => new StreamId(lbMs, 0) < id
                : id => new StreamId(lbMs, 0) <= id;
        }
        else if (lb.Split('-') is { Length: 2 } split
                 && long.TryParse(split[0], out lbMs)
                 && long.TryParse(split[1], out long lbSeq))
        {
            lowerBoundCondition = lbOpen
                ? id => new StreamId(lbMs, lbSeq) < id
                : id => new StreamId(lbMs, lbSeq) <= id;
        }
        else
        {
            msg = new("ERR Invalid stream ID specified as stream command argument");
            return false;
        }
        if (ub == "+") upperBoundCondition = _ => true;
        else if (long.TryParse(ub, out long ubMs))
        {
            upperBoundCondition = ubOpen
                ? id => id < new StreamId(ubMs, long.MaxValue)
                : id => id <= new StreamId(ubMs, long.MaxValue);
        }
        else if (ub.Split('-') is { Length: 2 } split
                 && long.TryParse(split[0], out ubMs)
                 && long.TryParse(split[1], out long ubSeq))
        {
            upperBoundCondition = ubOpen
                ? id => id < new StreamId(ubMs, ubSeq)
                : id => id <= new StreamId(ubMs, ubSeq);
        }
        else
        {
            msg = new("ERR Invalid stream ID specified as stream command argument");
            return false;
        }
        range = new StreamRangeCondition(lowerBoundCondition, upperBoundCondition);
        return true;
    }

    private StreamRangeCondition(Func<StreamId, bool> lb, Func<StreamId, bool> ub)
    {
        LowerBoundCondition = lb;
        UpperBoundCondition = ub;
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

    public static StreamId MinValue => new StreamId(0, 0);
    public static StreamId MaxValue => new StreamId(long.MaxValue, long.MaxValue);

    private static bool TryParse(
        string id,
        [NotNullWhen(true)] out StreamId? streamId,
        [NotNullWhen(false)] out ErrorMessage? msg)
    {
        streamId = null;
        msg = null;

        var split = id.Split("-");
        if (split[0][0] == '(')
        {
            split[0] = split[0][1..];
        }
        if (split.Length != 2
            || !long.TryParse(split[0], out long ms)
            || !long.TryParse(split[1], out long seq)
            || new StreamId(ms, seq) < StreamId.MinValue)
        {
            msg = new("ERR Invalid stream ID specified as stream command argument");
            return false;
        }
        streamId = new(ms, seq);
        return true;
    }

    public override string ToString() => $"{MillisecondsTime}-{SequenceNumber}";

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
    public static StreamId operator +(StreamId s1, StreamId s2)
        => new(s1.MillisecondsTime + s2.MillisecondsTime, s1.SequenceNumber + s2.SequenceNumber);
    public static StreamId operator -(StreamId s1, StreamId s2)
        => new(s1.MillisecondsTime - s2.MillisecondsTime, s1.SequenceNumber - s2.SequenceNumber);

    public override bool Equals(object? obj)
        => obj is StreamId id && this == id;
    public override int GetHashCode()
        => MillisecondsTime.GetHashCode() ^ (SequenceNumber.GetHashCode() << 16);
}