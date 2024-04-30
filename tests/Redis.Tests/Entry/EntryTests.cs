using FluentAssertions;
using Redis.Entry;

namespace Redis.Tests.Entry;

public class EntryTests
{
    [Test]
    public void StringEntry_IsExpired_is_false_if_null()
        => new StringEntry().IsExpired.Should().BeFalse();

    [Test]
    public void StringEntry_IsExpired_is_false_if_expiration_in_future()
        => new StringEntry() { Expiration = DateTime.MaxValue }.IsExpired.Should().BeFalse();

    [Test]
    public void StringEntry_IsExpired_is_true_if_expiration_in_future()
        => new StringEntry() { Expiration = DateTime.MinValue }.IsExpired.Should().BeTrue();
}