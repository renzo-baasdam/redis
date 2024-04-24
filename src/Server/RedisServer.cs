using Redis.Client;
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
            var tcpClient = await _server.AcceptTcpClientAsync();
            var stream = tcpClient.GetStream();
            var parser = new RespParser(stream);
            Console.WriteLine($"Established Tcp connection #{clientNumber}");
            var client = new RedisClient($"client-{clientNumber}-user", tcpClient);

            Listen(client);

            ++clientNumber;
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private async void Listen(RedisClient client)
    {
        while (client.Stream.CanRead)
        {
            try
            {
                await ListenOnce(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    protected async Task<int> ListenOnce(RedisClient client)
    {
        var message = await client.Parser.ReadMessage(client.Name);
        if (message is not null)
        {
            Console.WriteLine($"{client.Name}. Received command: {message.ToString().ReplaceLineEndings("\\r\\n")}.");
            foreach (var output in Handler(message, client.TcpClient))
            {
                Console.WriteLine($"{client.Name}. Sent Response: {output.ToString().ReplaceLineEndings("\\r\\n")}");
                await client.Stream.WriteAsync(output.ToBytes());
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