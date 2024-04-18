using System.Diagnostics.CodeAnalysis;

namespace Redis;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RedisConfig
{
    public const string EmptyRdb = "UkVESVMwMDEx+glyZWRpcy12ZXIFNy4yLjD6CnJlZGlzLWJpdHPAQPoFY3RpbWXCbQi8ZfoIdXNlZC1tZW3CsMQQAPoIYW9mLWJhc2XAAP/wbjv+wP9aog==";

    public string Role { get; set; } = "master";
    public string? MasterHost { get; set; }
    public int? MasterPort { get; set; }
    public string? MasterReplicationId { get; init; }
    public int MasterReplicationOffset { get; init; }
    public int Port { get; set; } = 6379;
    public string? Directory { get; set; }
    public string? Filename { get; set; }
}