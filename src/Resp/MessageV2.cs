using Redis;
using Redis.Extensions;

namespace Redis;

public abstract record MessageV2()
{
    public virtual byte[] ToBytes() => ToString().AsUtf8();
};
public sealed record SimpleStringMessage(string Value) : MessageV2
{
    public override string ToString() => Value.AsSimpleString();
}
public sealed record NullBulkStringMessage() : BulkStringMessage(string.Empty)
{
    public override string ToString() => $"$-1\r\n";
}
public record BulkStringMessage(string Value) : MessageV2
{
    public override string ToString() => Value.AsBulkString();
}
public record RdbFileMessage(byte[] Data) : MessageV2
{
    public override string ToString() => ToBytes().AsUtf8();
    public override byte[] ToBytes() => Data.AsRdbFile();
}
public sealed record ArrayMessage(List<MessageV2> Values) : MessageV2
{
    public ArrayMessage(params string[] values)
        : this(values
              .Select(val => new BulkStringMessage(val))
              .ToList<MessageV2>())
    { }

    public override string ToString() => Values.AsArrayString();
}