namespace Redis.Server;

public partial class RedisServer
{
    private string? DatabasePath
    {
        get
        {
            if (_config.Directory is { } dir && _config.Filename is { } filename)
            {
                return $"{dir}/{filename}";
            }
            return null;
        }
    }
}