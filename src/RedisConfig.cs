namespace Redis;

public class RedisConfig
{
    public string Role { get; set; } = "master";
    public string? MasterHost { get; set; }
    public int? MasterPort { get; set; }
    public string? MasterReplicationId { get; set; }
    public int MasterReplicationOffset { get; set; }
    public int Port { get; set; } = 6379;
    public string? Directory { get; set; }
    public string? Filename { get; set; }
}