namespace Redis.Database;

internal partial class RedisDatabase
{
    public string RdbVersion { get; set; } = string.Empty;

    /// <summary>
    /// Auxiliary fields
    /// </summary>
    public string RedisVersion { get; set; } = string.Empty;
    public int RedisArchitecture { get; set; } = 0;
    public DateTime CreationTime { get; set; }
    public string UsedMemory { get; set; } = string.Empty;
    public List<Database> Databases { get; set; } = [];

    internal class Database
    {
        public uint DatabaseNumber { get; set; }
        public uint DatabaseHashTableSize { get; set; }
        public uint ExpiryHashTableSize { get; set; }

        public Dictionary<string, string> Values = [];
    }
}