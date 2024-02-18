using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Redis.Database;

internal partial class RedisDatabase
{
    public static RedisDatabase FromBytes(byte[] bytes)
    {
        var db = new RedisDatabase();
        int index = 0;
        do
        {
            if (!db.ExecuteOpcode(ref index, bytes)) break;
        } while (true);
        return db;
    }

    [SuppressMessage("Style", "IDE0017:Simplify object initialization", Justification = "Separate initialization from parsing.")]
    private bool ExecuteOpcode(ref int index, byte[] bytes)
    {
        if (bytes[index] == 0xFE) // parse database selector
        {
            var db = new Database();
            db.DatabaseNumber = bytes[++index];
            if (bytes[++index] != 0xFB) throw new InvalidDataException("Expected 0xFB, the Resizedb information.");
            ++index;
            db.DatabaseHashTableSize = DecodeLengthEncoding(ref index, bytes);
            db.ExpiryHashTableSize = DecodeLengthEncoding(ref index, bytes);
            int valueType = bytes[index++];
            string key = DecodeStringEncoding(ref index, bytes);
            string value = DecodeValue(ref index, bytes, valueType);
            db.Values[key] = value;
            Databases.Add(db);
        }
        else if (bytes[index] == 0xFF)
        {
            return false;
        }
        else
        {
            ++index;
        }
        return true;
    }

    internal static string DecodeValue(ref int index, byte[] bytes, int valueType)
        => valueType switch
        {
            0 => DecodeStringEncoding(ref index, bytes),
            _ => throw new NotSupportedException($"Only Value type 0 is currently supported, found {valueType}. ")
        };

    internal static string DecodeStringEncoding(ref int index, byte[] bytes)
    {
        var length = DecodeLengthEncoding(ref index, bytes);
        var decoded = Encoding.UTF8.GetString(bytes, index, (int)length);
        index += (int)length;
        return decoded;
    }

    internal static uint DecodeLengthEncoding(ref int index, byte[] bytes)
    {
        int i = index;
        var firstTwoBits = (uint)bytes[i] >> 6;
        if (firstTwoBits == 0x00)
        {
            index += 1;
            return bytes[i];
        }
        if (firstTwoBits == 0x01)
        {
            index += 2;
            return (uint)((bytes[i] & 0x111111) << 8 + bytes[i + 1]);
        }
        if (firstTwoBits == 0x11)
        {
            index += 4;
            return BitConverter.ToUInt32(bytes, i + 1);
        }
        if (firstTwoBits == 0x11)
        {
            throw new NotSupportedException("Length encoding starts with 11, indicating special format, which is currently not supported.");
        }
        throw new InvalidDataException("Reached a part of the code that should not be reachable.");
    }
}