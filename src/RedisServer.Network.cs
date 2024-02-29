using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Redis;

public partial class RedisServer
{
      private readonly IPAddress LocalhostIP = Dns.GetHostEntry("localhost").AddressList
        .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
        .First();
}