using FluentAssertions;
using Redis.Database;
using Redis.Entry;

namespace Redis.Tests.Database;

public class RdbParser
{
    private readonly byte[] _dump =
    {
        0x52, 0x45, 0x44, 0x49, 0x53, 0x30, 0x30, 0x31, 0x31, 0xfa, 0x09, 0x72, 0x65,
        0x64, 0x69, 0x73, 0x2d, 0x76, 0x65, 0x72, 0x05, 0x37, 0x2e, 0x32, 0x2e, 0x34,
        0xfa, 0x0a, 0x72, 0x65, 0x64, 0x69, 0x73, 0x2d, 0x62, 0x69, 0x74, 0x73, 0xc0,
        0x40, 0xfa, 0x05, 0x63, 0x74, 0x69, 0x6d, 0x65, 0xc2, 0x3e, 0x0f, 0xd2, 0x65,
        0xfa, 0x08, 0x75, 0x73, 0x65, 0x64, 0x2d, 0x6d, 0x65, 0x6d, 0xc2, 0x00, 0x37,
        0x0d, 0x00, 0xfa, 0x08, 0x61, 0x6f, 0x66, 0x2d, 0x62, 0x61, 0x73, 0x65, 0xc0,
        0x00, 0xfe, 0x00, 0xfb, 0x01, 0x00, 0x00, 0x05, 0x6d, 0x79, 0x6b, 0x65, 0x79,
        0x05, 0x6d, 0x79, 0x76, 0x61, 0x6c, 0xff, 0x06, 0xf1, 0xe9, 0x16, 0x2a, 0x90,
        0x9a, 0x88
    };

    [Test]
    public void parses_database_info()
    {
        var dbInfo = RedisDatabase.FromBytes(_dump);
        dbInfo.Databases.Should().ContainSingle();
        dbInfo.Databases[0].Should().BeEquivalentTo(new RedisDatabase.Database(dbNumber: 0, dbHashTableSize: 1, expiryHashTableSize: 0)
        {
            Values = new Dictionary<string, StringEntry>
            {
                { "mykey", new StringEntry { Value = "myval" } }
            }
        });
    }
}