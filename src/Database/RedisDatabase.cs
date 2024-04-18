using System.Text;
using Redis.Server;

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

    private bool ParseOpcode(ref int index, byte[] bytes)
    {
        switch (bytes[index])
        {
            // parse database selector
            case 0xFE:
            {
                var databaseNumber = bytes[++index];
                if (bytes[++index] != 0xFB) throw new InvalidDataException("Expected 0xFB, the Resizedb information.");
                ++index;
                (var databaseHashTableSize, index) = DecodeLength(index, bytes);
                (var expiryHashTableSize, index) = DecodeLength(index, bytes);
                var db = new Database(databaseNumber, databaseHashTableSize, expiryHashTableSize);

                while (HasNextKeyValue(bytes[index]))
                {
                    // parse expiration
                    DateTime? expiration = null;
                    if (bytes[index] == (byte)0xFD)
                    {
                        var timeAsInt = BitConverter.ToInt32(bytes, ++index);
                        index += 4;
                        expiration = DateTimeOffset.FromUnixTimeSeconds(timeAsInt).UtcDateTime;
                    }
                    if (bytes[index] == (byte)0xFC)
                    {
                        var timeAsInt = BitConverter.ToInt64(bytes, ++index);
                        index += 8;
                        expiration = DateTimeOffset.FromUnixTimeMilliseconds(timeAsInt).UtcDateTime;
                    }

                    // parse key value pair
                    var valueType = bytes[index++];
                    var key = DecodeLengthPrefixedString(ref index, bytes);
                    var value = DecodeValue(ref index, bytes, valueType);
                    db.Values[key] = new RedisValue { Value = value, Expiration = expiration };
                }
                Databases.Add(db);
                break;
            }
            case 0xFF:
                return false;
            default:
                ++index;
                break;
        }

        return true;
    }

    private static readonly HashSet<byte> ValueTypes = new() { 0, 1, 2, 3, 4, 9, 10, 11, 12, 13, 14 };

    private static bool HasNextKeyValue(byte value)
        => ValueTypes.Contains(value) || (value == 0xFD) || (value == 0xFC);

    private static string DecodeValue(ref int index, byte[] bytes, byte valueType)
        => valueType switch
        {
            (byte)0x0 => DecodeLengthPrefixedString(ref index, bytes),
            _ => throw new NotSupportedException($"Only Value type 0 is currently supported, found {valueType}. ")
        };

    internal static string DecodeLengthPrefixedString(ref int index, byte[] bytes)
    {
        (var length, index) = DecodeLength(index, bytes);
        var decoded = Encoding.UTF8.GetString(bytes, index, (int)length);
        index += (int)length;
        return decoded;
    }

    internal static (uint length, int index) DecodeLength(int index, byte[] bytes)
    {
        return (bytes[index] >> 6) switch
        {
            0b00 => (bytes[index], index + 1),
            0b01 => (Length2(bytes, index), index + 2),
            0b10 => (Length5(bytes, index), index + 5),
            0b11 => throw new NotSupportedException("Length encoding starts with 11, indicating special format, which is currently not supported."),
            _ => throw new InvalidDataException("Reached a part of the code that should not be reachable.")
        };

        static uint Length2(byte[] bytes, int i) => (((uint)(bytes[i] & 0b11_1111) << 8) | bytes[i + 1]);

        static uint Length5(byte[] bytes, int i) => (uint)(0
                                                           | (bytes[i + 1] << 24)
                                                           | (bytes[i + 2] << 16)
                                                           | (bytes[i + 3] << 08)
                                                           | (bytes[i + 4] << 00));
    }
}