using System.IO;
using System.Net.Sockets;

namespace Redis.Client;

public class RedisClient
{
    public readonly Guid Id = Guid.NewGuid();
    public int Offset { get; set; }

    public string Name { get; set; }
    public TcpClient TcpClient { get; init; }
    public NetworkStream Stream { get; set; }
    public RespParser Parser { get; set; }

    public RedisClient(string name, TcpClient client, int offset) : this(name, client)
    {
        Offset = offset;
    }

    public RedisClient(string name, TcpClient client)
    {
        Name = name;
        TcpClient = client;
        Stream = TcpClient.GetStream();
        Parser = new RespParser(Stream);
    }
}