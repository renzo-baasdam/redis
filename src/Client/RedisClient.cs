using System.Net.Sockets;

namespace Redis.Client;

public class RedisClient
{
    public readonly Guid Id = Guid.NewGuid();
    public int Offset { get; set; }
    public string ClientType { get; set; } = "user";
    private string NameId { get; }
    public string Name => $"client-{NameId}-{ClientType}";
    public TcpClient TcpClient { get; }
    public NetworkStream Stream { get; }
    public RespParser Parser { get; }

    public RedisClient(string name, TcpClient client, int offset) : this(name, client)
    {
        Offset = offset;
    }

    public RedisClient(string nameId, TcpClient client)
    {
        NameId = nameId;
        TcpClient = client;
        Stream = TcpClient.GetStream();
        Parser = new RespParser(Stream);
    }
}