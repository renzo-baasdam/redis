using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Redis;

public partial class RedisServer
{
    private async void Propagate(string cmd)
    {
        foreach (var port in _replicates)
        {
            using var client = new TcpClient();
            try
            {
                var endpoint = new IPEndPoint(LocalhostIP, port);

                await client.ConnectAsync(endpoint);

                // send ping
                var stream = client.GetStream();
                byte[] data = Encoding.ASCII.GetBytes(cmd);

                Console.WriteLine($"Sending cmd to replicate on port {port}.");
                await stream.WriteAsync(data, 0, data.Length);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                client.GetStream().Close();
                client.Close();
            }
        }
    }

    private string ReplConf(string[] lines)
    {
        if (lines.Length > 6 && lines[4] == "listening-port" && int.TryParse(lines[6], out var port))
        {
            _replicates.Add(port);
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