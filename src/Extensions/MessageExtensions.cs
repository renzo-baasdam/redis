namespace Redis.Extensions;

internal static class MessageExtensions
{
    internal static bool IsReplConf(this Message message)
        => message is ArrayMessage msg
        && msg.Values[0] is BulkStringMessage first
        && first.Value.ToLowerInvariant() == "replconf"
        && msg.Values[1] is BulkStringMessage second
        && second.Value.ToLowerInvariant() == "getack";
}