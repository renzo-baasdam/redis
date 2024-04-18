using System.Net.Sockets;

namespace Redis.Server;

internal record RedisValue
{
    public string Value { get; init; } = string.Empty;
    public DateTime? Expiration { get; init; }
}

public partial class RedisServer : IDisposable
{
    public virtual async Task Start()
    {
        Console.WriteLine("Starting Redis...");
        LoadDatabase();
        await StartServer();
    }

    protected async Task StartServer()
    {
        _server.Start();
        Console.WriteLine($"Listing on {_server.LocalEndpoint}");
        int clientNumber = 0;
        while (true)
        {
            var client = await _server.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var parser = new RespParser(stream);
            Console.WriteLine($"Established Tcp connection #{clientNumber}");
            Listen(parser, stream, client, $"Client #{clientNumber}");

            ++clientNumber;
        }
    }

    private async void Listen(RespParser parser, NetworkStream stream, TcpClient client, string context = "default")
    {
        while (stream.CanRead)
        {
            try
            {
                await ListenOnce(parser, stream, client, context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    protected async Task<int> ListenOnce(RespParser parser, NetworkStream stream, TcpClient client, string context = "default")
    {
        var message = await parser.ReadMessage(context);
        if (message is not null)
        {
            Console.WriteLine($"{context}. Received command: {message.ToString().ReplaceLineEndings("\\r\\n")}.");
            foreach (var output in Handler(message, client))
            {
                Console.WriteLine($"{context}. Sent Response: {output.ToString().ReplaceLineEndings("\\r\\n")}");
                await stream.WriteAsync(output.ToBytes());
            }
        }
        return message?.Count ?? 0;
    }

    public virtual void Dispose()
    {
        foreach (var replica in _replicas)
        {
            replica.Dispose();
        }
    }
}