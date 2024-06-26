using Redis.Extensions;
using System.Net.Sockets;

namespace Redis.Client;

public class RedisClient
{
    #region Identifiers 
    public readonly Guid Id = Guid.NewGuid();
    private string NameId { get; }
    public string ClientType { get; set; } = "user";
    public string Name => $"client-{NameId}-{ClientType}";
    #endregion

    public readonly Stack<Message> Sent = new();

    /// <summary>
    /// Expected offset does not count the latest replconf getack command bytes sent, if it was the latest command.
    /// </summary>
    public int ExpectedOffset => Sent.Peek() is { } msg && msg.IsReplConf()
        ? SentOffset - msg.Count
        : SentOffset;
    public int AckOffset { get; set; }
    public int SentOffset { get; set; }
    public TcpClient TcpClient { get; }
    public NetworkStream Stream { get; }
    public RespParser Parser { get; }

    public RedisClient(string name, TcpClient client, int offset) : this(name, client)
    {
        SentOffset = offset;
    }

    public RedisClient(string nameId, TcpClient client)
    {
        NameId = nameId;
        TcpClient = client;
        Stream = TcpClient.GetStream();
        Parser = new RespParser(Stream);
    }

    public async Task Send(Message message)
    {
        var bytes = message.ToBytes();
        await Stream.WriteAsync(bytes);
        if (ClientType == "repl") SentOffset += bytes.Length;
        Sent.Push(message);
        Log($"Sent: {message.ToString().ReplaceLineEndings(@"\r\n")}");
    }

    public void Log(string message)
        => Console.WriteLine($"[{Name}] {message}");
}