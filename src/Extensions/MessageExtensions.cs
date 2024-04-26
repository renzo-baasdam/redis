using Redis.Client;

namespace Redis.Extensions;

internal static class MessageExtensions
{
    internal static bool IsReplConf(this Message message)
        => message is ArrayMessage msg && msg.Values[0] is BulkStringMessage first && first.Value.ToLowerInvariant() == "replconf";
}