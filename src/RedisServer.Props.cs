namespace Redis;

public partial class RedisServer
{
    private string? DatabasePath
    {
        get
        {
            if (_config.TryGetValue(RedisConfigKeys.Directory, out var dir) && _config.TryGetValue(RedisConfigKeys.Filename, out var filename))
            {
                return $"{dir}/{filename}";
            }
            return null;
        }
    }
}