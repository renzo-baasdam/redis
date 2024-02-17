namespace Redis;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var config = new Dictionary<string,string>();
        for (int i = 0; i < args.Length; i += 2)
        {
            if (args[i] == RedisConfigKeys.Directory) config[RedisConfigKeys.Directory] = args[i + 1];
            if (args[i] == RedisConfigKeys.Filename) config[RedisConfigKeys.Filename] = args[i + 1];
        }
        var server = new RedisServer(config);
        await server.Start();
    }
}