using System.Net.Sockets;
using System.Text;

namespace Redis;

public partial class RedisServer
{
    private async void Propagate(string cmd)
    {
        foreach (var client in _replicates)
        {
            try
            {
                // send ping
                var stream = client.GetStream();
                byte[] data = Encoding.UTF8.GetBytes(cmd);

                Console.WriteLine($"Propagating cmd to replicate.");
                await stream.WriteAsync(data, 0, data.Length);
                var response = new byte[512];
                await stream.ReadAsync(response);
                Console.WriteLine($"Propagation response: {response.AsUtf8().ReplaceLineEndings("\\r\\n")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    private string ReplConf(string[] lines, TcpClient client)
    {
        if (lines.Length > 6 && lines[4] == "listening-port" && int.TryParse(lines[6], out var port))
        {
            _replicates.Add(client);
            return "+OK\r\n";
        }
        var second = lines.Length > 10 && lines[4] == "capa" && lines[6] == "eof" && lines[8] == "capa" && lines[10] == "psync2";
        if (second) return "+OK\r\n";
        return "$-1\r\n";
    }

    private byte[][] PSync(string[] lines)
    {
        var bytes = Convert.FromBase64String(RedisConfig.EmptyRdb);
        if (lines.Length > 6 && lines[4] == "?" && lines[6] == "-1")
        {
            var initialResponse = $"+FULLRESYNC {_config.MasterReplicationId} {_config.MasterReplicationOffset}\r\n".AsUtf8();
            var rdbResponse = $"${bytes.Length}\r\n".AsUtf8().Concat(bytes).ToArray();
            return new byte[][] { initialResponse, rdbResponse };
        }
        return new byte[][] { "$-1\r\n".AsUtf8() };
    }
}