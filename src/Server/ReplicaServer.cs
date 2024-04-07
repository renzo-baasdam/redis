using System;
using System.Net;
using System.Net.Sockets;

namespace Redis.Server;

public partial class ReplicaServer : RedisServer
{
    private int Offset { get; set; }
    private TcpClient? Master { get; set; }

    public ReplicaServer(RedisConfig config) : base(config)
    {
    }

    private async void Handshake(int masterPort)
    {
        try
        {
            var client = new TcpClient();
            var endpoint = new IPEndPoint(LocalhostIP, masterPort);
            await client.ConnectAsync(endpoint);

            var stream = client.GetStream();
            var parser = new RespParser(stream);

            Master = client;

            // handshake
            await Send(stream, new ArrayMessage("PING"));
            await ListenOnce(parser, stream, client, "Master client");
            await Send(stream, new ArrayMessage("REPLCONF", "listening-port", _config.Port.ToString()));
            await ListenOnce(parser, stream, client, "Master client");
            await Send(stream, new ArrayMessage("REPLCONF", "capa", "psync2"));
            await ListenOnce(parser, stream, client, "Master client");
            await Send(stream, new ArrayMessage("PSYNC", "?", "-1"));
            await ListenOnce(parser, stream, client, "Master client");
            await ListenOnce(parser, stream, client, "Master client");
            Console.WriteLine("Replica has finished handling RDB file.");
            Offset = 0;
            ReplicaListener(parser, stream, client, "Master client");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        async Task Send(Stream stream, ArrayMessage msg)
        {
            Console.WriteLine($"Master client. Sent request: {msg.ToString().Replace("\r\n", "\\r\\n")}");
            await stream.WriteAsync(msg.ToBytes());
        }
    }

    protected override IList<Message> Handler(Message message, TcpClient client)
    {
        // Replica should not send response to Master unless it is a REPLCONF response
        return client != Master || IsReplconfCommand(message)
                ? base.Handler(message, client)
                : new List<Message>();

        bool IsReplconfCommand(Message msg)
            => msg is ArrayMessage arrayMsg && ParseCommand(arrayMsg).Command == "REPLCONF";
    }

    async void ReplicaListener(RespParser parser, NetworkStream stream, TcpClient client, string context = "default")
    {
        while (stream.CanRead)
        {
            try
            {
                Offset += await ListenOnce(parser, stream, client, context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    public override void Dispose()
    {
        Master?.Dispose();
        base.Dispose();
    }
}