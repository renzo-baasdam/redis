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
            if (!db.ParseOpcode(ref index, bytes)) break;
        } while (true);
        return db;
    }

    internal bool ParseOpcode(ref int index, byte[] bytes)
    {
        if (bytes[index] == 0xFE) // parse database selector
        {
            var db = new Database();
            db.DatabaseNumber = bytes[++index];
            if (bytes[++index] != 0xFB) throw new InvalidDataException("Expected 0xFB, the Resizedb information.");
            ++index;
            db.DatabaseHashTableSize = DecodeLength(ref index, bytes);
            db.ExpiryHashTableSize = DecodeLength(ref index, bytes);
            int valueType = bytes[index++];
            string key = DecodeLengthPrefixedString(ref index, bytes);
            string value = DecodeValue(ref index, bytes, valueType);
            db.Values[key] = new() { Value = value };
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
            0 => DecodeLengthPrefixedString(ref index, bytes),
            _ => throw new NotSupportedException($"Only Value type 0 is currently supported, found {valueType}. ")
        };

    internal static string DecodeLengthPrefixedString(ref int index, byte[] bytes)
    {
        var length = DecodeLength(ref index, bytes);
        var decoded = Encoding.UTF8.GetString(bytes, index, (int)length);
        index += (int)length;
        return decoded;
    }

    internal static uint DecodeLength(ref int index, byte[] bytes)
    {
        int i = index;
        var encodingType = (uint)bytes[i] >> 6;
        if (encodingType == 0b00)
        {
            index += 1;
            return bytes[i];
        }
        if (encodingType == 0b01)
        {
            index += 2;
            return (((uint)(bytes[i] & 0b11_1111) << 8) | bytes[i + 1]);
        }
        if (encodingType == 0b10)
        {
            index += 5;
            return (uint)
                 ((bytes[i + 1] << 24)
                + (bytes[i + 2] << 16)
                + (bytes[i + 3] << 08)
                + (bytes[i + 4] << 00));
        }
        if (encodingType == 0b11)
        {
            throw new NotSupportedException("Length encoding starts with 11, indicating special format, which is currently not supported.");
        }
        throw new InvalidDataException("Reached a part of the code that should not be reachable.");
    }
}