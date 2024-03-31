using Redis;
using Redis.Extensions;

namespace Redis;

public abstract record MessageV2();
public sealed record SimpleStringMessage(string Value) : MessageV2
{
    public override string ToString() => Value.AsSimpleString();
}
public sealed record BulkStringMessage(string Value) : MessageV2
{
    public override string ToString() => Value.AsBulkString();
}
public sealed record ArrayMessage(List<MessageV2> Values) : MessageV2
{
    public override string ToString() => Values.AsArrayString();
}