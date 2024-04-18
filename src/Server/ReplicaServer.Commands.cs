using System.Net.Sockets;

namespace Redis.Server;

public partial class ReplicaServer
{
    protected override Message? ReplConf(string[] args, TcpClient client)
    {
        if (args.Length >= 1 && args[0].ToUpper() == "GETACK")
        {
            var values = new List<Message>()
            {
                new BulkStringMessage("REPLCONF"),
                new BulkStringMessage("ACK"),
                new BulkStringMessage(Offset.ToString())
            };
            return new ArrayMessage(values);
        }
        return base.ReplConf(args, client);
    }
}