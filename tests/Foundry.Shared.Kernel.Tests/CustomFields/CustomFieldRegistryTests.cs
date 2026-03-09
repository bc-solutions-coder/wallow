using Foundry.Shared.Kernel.CustomFields;

namespace Foundry.Shared.Kernel.Tests.CustomFields;

public class CustomFieldRegistryTests
{
    [Fact]
    public void Register_NewEntityType_MakesItSupported()
    {
        CustomFieldRegistry.Register("TestEntity_Reg", "TestModule", "Test entity for registration");

        bool isSupported = CustomFieldRegistry.IsSupported("TestEntity_Reg");

        isSupported.Should().BeTrue();
    }

    [Fact]
    public void Register_NewEntityType_IsReturnedByGetSupportedEntityTypes()
    {
        CustomFieldRegistry.Register("TestEntity_List", "TestModule", "Test entity for listing");

        IReadOnlyList<EntityTypeInfo> types = CustomFieldRegistry.GetSupportedEntityTypes();

        types.Should().Contain(t => t.EntityType == "TestEntity_List" && t.Module == "TestModule");
    }

    [Fact]
    public void Register_ExistingEntityType_OverwritesPrevious()
    {
        CustomFieldRegistry.Register("TestEntity_Overwrite", "OldModule", "Old description");

        CustomFieldRegistry.Register("TestEntity_Overwrite", "NewModule", "New description");

        EntityTypeInfo? info = CustomFieldRegistry.GetEntityType("TestEntity_Overwrite");
        info.Should().NotBeNull();
        info.Module.Should().Be("NewModule");
        info.Description.Should().Be("New description");
    }

    [Fact]
    public void GetEntityType_ExistingType_ReturnsEntityTypeInfo()
    {
        CustomFieldRegistry.Register("TestEntity_Get", "TestModule", "A test entity");

        EntityTypeInfo? info = CustomFieldRegistry.GetEntityType("TestEntity_Get");

        info.Should().NotBeNull();
        info.EntityType.Should().Be("TestEntity_Get");
        info.Module.Should().Be("TestModule");
        info.Description.Should().Be("A test entity");
    }

    [Fact]
    public void GetEntityType_NonExistentType_ReturnsNull()
    {
        EntityTypeInfo? info = CustomFieldRegistry.GetEntityType("NonExistent_XYZ_12345");

        info.Should().BeNull();
    }

    [Fact]
    public void IsSupported_NonExistentType_ReturnsFalse()
    {
        bool isSupported = CustomFieldRegistry.IsSupported("CompletelyFake_99999");

        isSupported.Should().BeFalse();
    }

    [Fact]
    public void IsSupported_PreRegisteredInvoice_ReturnsTrue()
    {
        bool isSupported = CustomFieldRegistry.IsSupported("Invoice");

        isSupported.Should().BeTrue();
    }

    [Fact]
    public void GetSupportedEntityTypes_ContainsPreRegisteredTypes()
    {
        IReadOnlyList<EntityTypeInfo> types = CustomFieldRegistry.GetSupportedEntityTypes();

        types.Should().Contain(t => t.EntityType == "Invoice");
        types.Should().Contain(t => t.EntityType == "Payment");
        types.Should().Contain(t => t.EntityType == "Subscription");
    }
}

public class CustomFieldOptionTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        CustomFieldOption option1 = new() { Value = "opt1", Label = "Option 1", Order = 1, IsActive = true };
        CustomFieldOption option2 = new() { Value = "opt1", Label = "Option 1", Order = 1, IsActive = true };

        option1.Should().Be(option2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        CustomFieldOption option1 = new() { Value = "opt1", Label = "Option 1" };
        CustomFieldOption option2 = new() { Value = "opt2", Label = "Option 2" };

        option1.Should().NotBe(option2);
    }

    [Fact]
    public void Defaults_IsActiveTrue_OrderZero()
    {
        CustomFieldOption option = new() { Value = "val", Label = "Label" };

        option.IsActive.Should().BeTrue();
        option.Order.Should().Be(0);
    }

    [Fact]
    public void Init_AllProperties_SetsCorrectly()
    {
        CustomFieldOption option = new() { Value = "priority", Label = "Priority", Order = 3, IsActive = false };

        option.Value.Should().Be("priority");
        option.Label.Should().Be("Priority");
        option.Order.Should().Be(3);
        option.IsActive.Should().BeFalse();
    }
}

public class CustomFieldTypeTests
{
    [Theory]
    [InlineData(CustomFieldType.Text, 0)]
    [InlineData(CustomFieldType.TextArea, 1)]
    [InlineData(CustomFieldType.Number, 2)]
    [InlineData(CustomFieldType.Decimal, 3)]
    [InlineData(CustomFieldType.Date, 4)]
    [InlineData(CustomFieldType.DateTime, 5)]
    [InlineData(CustomFieldType.Boolean, 6)]
    [InlineData(CustomFieldType.Dropdown, 7)]
    [InlineData(CustomFieldType.MultiSelect, 8)]
    [InlineData(CustomFieldType.Email, 9)]
    [InlineData(CustomFieldType.Url, 10)]
    [InlineData(CustomFieldType.Phone, 11)]
    public void EnumValue_HasExpectedIntValue(CustomFieldType fieldType, int expectedValue)
    {
        ((int)fieldType).Should().Be(expectedValue);
    }

    [Fact]
    public void EnumValues_HasExpectedCount()
    {
        int count = Enum.GetValues<CustomFieldType>().Length;

        count.Should().Be(12);
    }
}

