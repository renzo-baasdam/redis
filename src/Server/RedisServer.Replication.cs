using Redis.Extensions;
using System.Net.Sockets;
using System.Text;

namespace Redis.Server;

public partial class RedisServer
{
    private async void Propagate(MessageV2 msg)
    {
        foreach (var client in _replicas)
        {
            try
            {
                var stream = client.GetStream();
                Console.WriteLine($"Propagating cmd to replica.");
                await stream.WriteAsync(msg.ToBytes());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    private string ReplConf(string[] lines, TcpClient client)
    {
        if (lines.Length > 6 && lines[4].ToLowerInvariant() == "getack")
        {
            return new string[] { "REPLCONF", "ACK", "0" }.AsArrayString();
        }
        if (lines.Length > 6 && lines[4].ToLowerInvariant() == "ack")
        {
            return string.Empty;
        }
        if (lines.Length > 6 && lines[4] == "listening-port" && int.TryParse(lines[6], out var port))
        {
            _replicas.Add(client);
            return "+OK\r\n";
        }
        var second = lines.Length > 6 && lines[4] == "capa" && lines[6] == "psync2";
        if (second) return "+OK\r\n";
        return "$-1\r\n";
    }

    private byte[][] PSync(string[] lines)
    {
        var bytes = Convert.FromBase64String(RedisConfig.EmptyRdb);
        if (lines.Length > 6 && int.TryParse(lines[6], out int offset))
        {
            var initialResponse = $"+FULLRESYNC {_config.MasterReplicationId} {_config.MasterReplicationOffset}\r\n".AsUtf8();
            var rdbResponse = $"${bytes.Length}\r\n".AsUtf8().Concat(bytes).ToArray();
            return new byte[][] { initialResponse, rdbResponse };
        }
        return new byte[][] { "$-1\r\n".AsUtf8() };
    }
}