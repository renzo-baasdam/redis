using System.Net;
using System.Net.Sockets;

namespace Redis.Server;

public partial class RedisServer
{
    protected readonly IPAddress LocalhostIP = Dns.GetHostEntry("localhost").AddressList
        .First(x => x.AddressFamily == AddressFamily.InterNetwork);
}