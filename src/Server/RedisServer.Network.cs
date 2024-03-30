using System.Net;
using System.Net.Sockets;

namespace Redis.Server;

public partial class RedisServer
{
    private readonly IPAddress LocalhostIP = Dns.GetHostEntry("localhost").AddressList
      .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
      .First();
}