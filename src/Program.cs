namespace Redis;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var config = new Dictionary<string, string>();
        config[RedisConfigKeys.Role] = "master";
        for (int i = 0; i < args.Length; i += 2)
        {
            if (args[i] == RedisConfigKeys.Port.AsArgumentString()) config[RedisConfigKeys.Port] = args[i + 1];
            if (args[i] == RedisConfigKeys.Directory.AsArgumentString()) config[RedisConfigKeys.Directory] = args[i + 1];
            if (args[i] == RedisConfigKeys.Filename.AsArgumentString()) config[RedisConfigKeys.Filename] = args[i + 1];
            if (args[i] == RedisConfigKeys.Replica.AsArgumentString())
            {
                config[RedisConfigKeys.Role] = "slave";
                config[RedisConfigKeys.MasterHost] = args[i + 1];
                config[RedisConfigKeys.MasterPort] = args[i + 2];
                ++i;
            }
        }
        var server = new RedisServer(config);
        await server.Start();
    }
}