namespace Redis.Server;

public partial class RedisServer
{
    // todo improve, guarantee order
    private async Task Propagate(Message msg)
    {
        foreach (var client in _replicas)
        {
            try
            {
                var stream = client.GetStream();
                Console.WriteLine($"Propagating cmd to replica: {msg.ToString().ReplaceLineEndings("\\r\\n")}");
                await stream.WriteAsync(msg.ToBytes());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}