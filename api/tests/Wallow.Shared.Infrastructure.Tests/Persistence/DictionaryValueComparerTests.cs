using Wallow.Shared.Infrastructure.Core.Persistence;

namespace Wallow.Shared.Infrastructure.Tests.Persistence;

public class DictionaryValueComparerTests
{
    private readonly DictionaryValueComparer _comparer = new();

    [Fact]
    public void Equals_WithNullDictionaries_ReturnsTrue()
    {
        bool result = _comparer.Equals(null, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithSameReference_ReturnsTrue()
    {
        Dictionary<string, object> dict = new() { { "key", "value" } };

        bool result = _comparer.Equals(dict, dict);

        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNullAndNonNull_ReturnsFalse()
    {
        Dictionary<string, object> dict = new() { { "key", "value" } };

        _comparer.Equals(null, dict).Should().BeFalse();
        _comparer.Equals(dict, null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentCounts_ReturnsFalse()
    {
        Dictionary<string, object> dict1 = new() { { "key1", "value1" } };
        Dictionary<string, object> dict2 = new()
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        bool result = _comparer.Equals(dict1, dict2);

        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithIdenticalSimpleValues_ReturnsTrue()
    {
        Dictionary<string, object> dict1 = new()
        {
            { "key1", "value1" },
            { "key2", 42 }
        };
        Dictionary<string, object> dict2 = new()
        {
            { "key1", "value1" },
            { "key2", 42 }
        };

        bool result = _comparer.Equals(dict1, dict2);

        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentSimpleValues_ReturnsFalse()
    {
        Dictionary<string, object> dict1 = new() { { "key", "value1" } };
        Dictionary<string, object> dict2 = new() { { "key", "value2" } };

        bool result = _comparer.Equals(dict1, dict2);

        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentKeys_ReturnsFalse()
    {
        Dictionary<string, object> dict1 = new() { { "key1", "value" } };
        Dictionary<string, object> dict2 = new() { { "key2", "value" } };

        bool result = _comparer.Equals(dict1, dict2);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WithNull_ReturnsZero()
    {
        int hash = _comparer.GetHashCode(null);

        hash.Should().Be(0);
    }

    [Fact]
    public void GetHashCode_WithIdenticalDictionaries_ReturnsSameHash()
    {
        Dictionary<string, object> dict1 = new()
        {
            { "key1", "value1" },
            { "key2", 42 }
        };
        Dictionary<string, object> dict2 = new()
        {
            { "key1", "value1" },
            { "key2", 42 }
        };

        int hash1 = _comparer.GetHashCode(dict1);
        int hash2 = _comparer.GetHashCode(dict2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_WithDifferentDictionaries_ReturnsDifferentHashes()
    {
        Dictionary<string, object> dict1 = new() { { "key", "value1" } };
        Dictionary<string, object> dict2 = new() { { "key", "value2" } };

        int hash1 = _comparer.GetHashCode(dict1);
        int hash2 = _comparer.GetHashCode(dict2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Snapshot_WithNull_ReturnsNull()
    {
        Dictionary<string, object>? snapshot = _comparer.Snapshot(null);

        snapshot.Should().BeNull();
    }

    [Fact]
    public void Snapshot_CreatesDifferentInstance()
    {
        Dictionary<string, object> original = new()
        {
            { "key", "value" }
        };

        Dictionary<string, object>? snapshot = _comparer.Snapshot(original);

        snapshot.Should().NotBeSameAs(original);
    }

    [Fact]
    public void Snapshot_CreatesDeepCopy()
    {
        Dictionary<string, object> original = new()
        {
            { "key1", "value1" },
            { "key2", 42 }
        };

        Dictionary<string, object>? snapshot = _comparer.Snapshot(original);

        snapshot.Should().NotBeSameAs(original);
        snapshot.Should().ContainKey("key1");
        snapshot.Should().ContainKey("key2");
    }

    [Fact]
    public void Snapshot_IsIndependentOfOriginal()
    {
        Dictionary<string, object> original = new()
        {
            { "key", "value" }
        };
        Dictionary<string, object>? snapshot = _comparer.Snapshot(original);

        original["key"] = "modified";

        snapshot.Should().ContainKey("key");
        snapshot["key"].ToString().Should().Be("value");
    }

    [Fact]
    public void Equals_WithCamelCaseSerialization_IsConsistent()
    {
        Dictionary<string, object> dict1 = new() { { "MyKey", "value" } };
        Dictionary<string, object> dict2 = new() { { "MyKey", "value" } };

        bool result = _comparer.Equals(dict1, dict2);

        result.Should().BeTrue();
    }
}
