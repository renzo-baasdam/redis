using Microsoft.CodeAnalysis;
using Redis.Database;
using Redis.Extensions;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Redis.Server;

public partial class RedisServer
{
    public void LoadDatabase()
    {
        try
        {
            if (DatabasePath is null) return;
            Console.WriteLine($"Loading database from: {DatabasePath}");
            var bytes = File.ReadAllBytes(DatabasePath);
            _database = RedisDatabase.FromBytes(bytes);
            foreach (var kv in _database.Databases[0].Values)
                _cache[kv.Key] = kv.Value;
            Console.WriteLine($"Loaded database successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading database: {ex.Message}");
        }
    }
}