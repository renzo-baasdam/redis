namespace Redis;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var conf = new RedisConfig();
        conf.MasterReplicationId = "8371b4fb1155b71f4a04d3e1bc3e18c4a990aeeb";
        conf.MasterReplicationOffset = 0;
        for (int i = 0; i < args.Length; i += 2)
        {
            if (args[i] == RedisConfigKeys.Port.AsArgumentString()) conf.Port = int.Parse(args[i + 1]);
            if (args[i] == RedisConfigKeys.Directory.AsArgumentString()) conf.Directory = args[i + 1];
            if (args[i] == RedisConfigKeys.Filename.AsArgumentString()) conf.Filename = args[i + 1];
            if (args[i] == RedisConfigKeys.Replica.AsArgumentString())
            {
                conf.Role = "slave";
                conf.MasterHost = args[i + 1];
                conf.MasterPort = int.Parse(args[i + 2]);
                ++i;
            }
        }
        var server = new RedisServer(conf);
        await server.Start();
    }
}