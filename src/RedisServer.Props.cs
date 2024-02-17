namespace Redis;

public partial class RedisServer
{
    private string? PersistencePath
    {
        get
        {
            if(_config.TryGetValue(RedisConfigKeys.Directory, out var dir) && _config.TryGetValue(RedisConfigKeys.Directory, out var filename))
            {
                return $"{dir}/{filename}";
            }
            return null;
        }
    }
}