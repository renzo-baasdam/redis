using Redis.Database;

namespace Redis.Server;

public partial class RedisServer
{
    protected void LoadDatabase()
    {
        try
        {
            if (DatabasePath is null) return;
            Console.WriteLine($"Loading database from: {DatabasePath}");
            var bytes = File.ReadAllBytes(DatabasePath);
            Database = RedisDatabase.FromBytes(bytes);
            foreach (var kv in Database.Databases[0].Values)
                _cache[kv.Key] = kv.Value;
            Console.WriteLine("Loaded database successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading database: {ex.Message}");
        }
    }
}