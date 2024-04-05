using Redis.Extensions;
using System.Net.Sockets;
using System.Text;

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
                Console.WriteLine($"Propagating cmd to replica.");
                await stream.WriteAsync(msg.ToBytes());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}