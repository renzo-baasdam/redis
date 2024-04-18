using Redis.Extensions;

namespace Redis;

public abstract record Message
{
    public int Count => ToBytes().Length;
    public virtual byte[] ToBytes() => ToString().AsUtf8();
}

public sealed record SimpleStringMessage(string Value) : Message
{
    public override string ToString() => Value.AsSimpleString();
}

public sealed record NullBulkStringMessage() : BulkStringMessage(string.Empty)
{
    public override string ToString() => "$-1\r\n";
}

public record BulkStringMessage(string Value) : Message
{
    public override string ToString() => Value.AsBulkString();
}

public record RdbFileMessage(byte[] Data) : Message
{
    public override string ToString() => ToBytes().AsUtf8();
    public override byte[] ToBytes() => Data.AsRdbFile();
}

public sealed record ArrayMessage(List<Message> Values) : Message
{
    public ArrayMessage(params string[] values)
        : this(values
            .Select(val => new BulkStringMessage(val))
            .ToList<Message>())
    {
    }

    public override string ToString() => Values.AsArrayString();
}

public sealed record IntegerMessage(int Value) : Message
{
    public override string ToString() => Value.AsInteger();
}