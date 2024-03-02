using System.Net;
using System.Net.Sockets;

namespace Redis;

public class ReplicateClient : TcpClient
{
    public IPAddress Ip { get; }
    public int Port { get; set; }

    public ReplicateClient(IPAddress ip, int port)
    {
        Ip = ip;
        Port = port;
    }

    public async Task ConnectAsync()
    {
        if (!Connected) await ConnectAsync(Ip, Port);
    }
}