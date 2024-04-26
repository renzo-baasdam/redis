using Redis.Client;
using Redis.Extensions;

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
            var client = new RedisClient(clientNumber.ToString(), tcpClient);
            client.Log("Connection established");
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
                client.Log($"Caught exception: {ex.Message}");
                break;
            }
        }
    }

    protected async Task<int> ListenOnce(RedisClient client)
    {
        var message = await client.Parser.ReadMessage(client.Name);
        if (message is not null)
        {
            client.Log($"Received command: {message.ToString().ReplaceLineEndings(@"\r\n")}.");
            foreach (var output in await Handler(message, client))
            {
                await client.Send(message);
            }
        }
        return message?.Count ?? 0;
    }

    public virtual void Dispose()
    {
        foreach (var replica in _replicas.Values)
        {
            replica.TcpClient.Dispose();
        }
    }
}