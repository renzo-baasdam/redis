using Redis.Extensions;

namespace Redis.Server;

public partial class RedisServer
{
    // todo improve, guarantee order
    private async Task Propagate(Message msg)
    {
        foreach (var client in _replicas.Values)
        {
            try
            {
                client.Log($"Propagating cmd to replica: {msg.ToString().ReplaceLineEndings(@"\r\n")}");
                await client.Send(msg);
            }
            catch (Exception ex)
            {
                client.Log(ex.Message);
            }
        }
    }
}