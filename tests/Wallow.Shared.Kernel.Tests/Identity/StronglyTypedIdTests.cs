using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Tests.Identity;

public class TenantIdTests
{
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        TenantId id = TenantId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        TenantId id = TenantId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        TenantId id1 = TenantId.Create(guid);
        TenantId id2 = TenantId.Create(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        TenantId id1 = TenantId.New();
        TenantId id2 = TenantId.New();

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        Guid guid = Guid.NewGuid();
        TenantId id = TenantId.Create(guid);

        string result = id.ToString();

        result.Should().Contain(guid.ToString());
    }

    [Fact]
    public void Default_HasEmptyGuid()
    {
        TenantId id = default;

        id.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        Guid guid = Guid.NewGuid();
        TenantId id1 = TenantId.Create(guid);
        TenantId id2 = TenantId.Create(guid);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_ReturnsDifferentHash()
    {
        TenantId id1 = TenantId.New();
        TenantId id2 = TenantId.New();

        id1.GetHashCode().Should().NotBe(id2.GetHashCode());
    }

    [Fact]
    public void Platform_HasExpectedGuid()
    {
        Guid expected = new("00000000-0000-0000-0000-000000000001");

        TenantId.Platform.Value.Should().Be(expected);
    }

    [Fact]
    public void Platform_IsNotEmpty()
    {
        TenantId.Platform.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Platform_IsNotEqualToNewId()
    {
        TenantId newId = TenantId.New();

        TenantId.Platform.Should().NotBe(newId);
    }
}

public class UserIdTests
{
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        UserId id = UserId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        UserId id = UserId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        UserId id1 = UserId.Create(guid);
        UserId id2 = UserId.Create(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        UserId id1 = UserId.New();
        UserId id2 = UserId.New();

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        Guid guid = Guid.NewGuid();
        UserId id = UserId.Create(guid);

        string result = id.ToString();

        result.Should().Contain(guid.ToString());
    }

    [Fact]
    public void Default_HasEmptyGuid()
    {
        UserId id = default;

        id.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        Guid guid = Guid.NewGuid();
        UserId id1 = UserId.Create(guid);
        UserId id2 = UserId.Create(guid);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_ReturnsDifferentHash()
    {
        UserId id1 = UserId.New();
        UserId id2 = UserId.New();

        id1.GetHashCode().Should().NotBe(id2.GetHashCode());
    }
}

public class StronglyTypedIdConverterTests
{
    [Fact]
    public void ConvertToProvider_ReturnUnderlyingGuid()
    {
        StronglyTypedIdConverter<TenantId> converter = new();
        Guid guid = Guid.NewGuid();
        TenantId id = TenantId.Create(guid);

        Guid result = (Guid)converter.ConvertToProvider(id)!;

        result.Should().Be(guid);
    }

    [Fact]
    public void ConvertFromProvider_ReturnsStronglyTypedId()
    {
        StronglyTypedIdConverter<TenantId> converter = new();
        Guid guid = Guid.NewGuid();

        TenantId result = (TenantId)converter.ConvertFromProvider(guid)!;

        result.Value.Should().Be(guid);
    }

    [Fact]
    public void RoundTrip_PreservesValue()
    {
        StronglyTypedIdConverter<UserId> converter = new();
        UserId original = UserId.New();

        Guid asGuid = (Guid)converter.ConvertToProvider(original)!;
        UserId restored = (UserId)converter.ConvertFromProvider(asGuid)!;

        restored.Should().Be(original);
    }
}

public class StronglyTypedIdExtensionsTests
{
    [Fact]
    public void EnsureId_WhenEmpty_GeneratesNewId()
    {
        TenantId empty = default;

        TenantId result = empty.EnsureId();

        result.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void EnsureId_WhenNonEmpty_ReturnsSameId()
    {
        TenantId id = TenantId.New();

        TenantId result = id.EnsureId();

        result.Should().Be(id);
    }

    [Fact]
    public void EnsureId_WhenEmpty_GeneratesUniqueIds()
    {
        TenantId empty = default;

        TenantId result1 = empty.EnsureId();
        TenantId result2 = empty.EnsureId();

        result1.Should().NotBe(result2);
    }
}
