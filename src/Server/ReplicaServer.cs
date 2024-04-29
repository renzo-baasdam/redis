using Redis.Client;
using Redis.Extensions;
using System.Net;
using System.Net.Sockets;

namespace Redis.Server;

public partial class ReplicaServer : RedisServer
{
    private int Offset { get; set; }
    private Guid? MasterId { get; set; }

    public ReplicaServer(RedisConfig config) : base(config)
    {
    }

    public override async Task Start()
    {
        Console.WriteLine("Starting Redis...");
        LoadDatabase();
        Handshake(_config.MasterPort!.Value);
        await StartServer();
    }

    private async void Handshake(int masterPort)
    {
        try
        {
            var tcpClient = new TcpClient();
            var endpoint = new IPEndPoint(LocalhostIP, masterPort);
            await tcpClient.ConnectAsync(endpoint);

            var client = new RedisClient("@", tcpClient)
            {
                ClientType = "mstr",
            };
            MasterId = client.Id;

            // handshake
            await Send(client, new ArrayMessage("PING"));
            await ListenOnce(client);
            await Send(client, new ArrayMessage("REPLCONF", "listening-port", _config.Port.ToString()));
            await ListenOnce(client);
            await Send(client, new ArrayMessage("REPLCONF", "capa", "psync2"));
            await ListenOnce(client);
            await Send(client, new ArrayMessage("PSYNC", "?", "-1"));
            Offset = 0;
            Offset += await ListenOnce(client);
            Offset += await ListenOnce(client);
            client.Log("Replica has finished handling RDB file.");
            await Send(client, new ArrayMessage("REPLCONF", "ACK", Offset.ToString()));
            ReplicaListener(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        return;

        async Task Send(RedisClient client, Message msg)
        {
            client.Log($"Sent request: {msg.ToString().Replace("\r\n", @"\r\n")}");
            client.Log($"Offset: {Offset}");
            await client.Stream.WriteAsync(msg.ToBytes());
        }
    }

    protected override async Task<IList<Message>> Handler(Message message, RedisClient client)
    {
        // Replica should not send response to Master unless it is a REPLCONF response
        var result = await base.Handler(message, client);
        return client.Id != MasterId || IsReplconfCommand(message)
            ? result
            : new List<Message>();

        bool IsReplconfCommand(Message msg)
            => msg is ArrayMessage arrayMsg && ParseCommand(arrayMsg).Command == "REPLCONF";
    }

    private async void ReplicaListener(RedisClient client)
    {
        while (client.Stream.CanRead)
        {
            try
            {
                Offset += await ListenOnce(client);
            }
            catch (Exception ex)
            {
                client.Log(ex.Message);
                break;
            }
        }
    }

    public override void Dispose()
    {
        // todo other clients
        base.Dispose();
    }
}