using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Foundry.Configuration.Infrastructure.Persistence;
using Foundry.Configuration.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;

namespace Foundry.Configuration.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class CustomFieldDefinitionRepositoryTests : DbContextIntegrationTestBase<ConfigurationDbContext>
{
    public CustomFieldDefinitionRepositoryTests(PostgresContainerFixture fixture) : base(fixture) { }

    protected override bool UseMigrateAsync => true;

    private CustomFieldDefinitionRepository CreateRepository() => new(DbContext);

    private CustomFieldDefinition CreateDefinition(string? fieldKey = null, string entityType = "Invoice")
    {
        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            TestTenantId,
            entityType,
            fieldKey ?? $"field_{Guid.NewGuid():N}".Substring(0, 30),
            "Test Field",
            CustomFieldType.Text,
            TestUserId);
        definition.ClearDomainEvents();
        return definition;
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsDefinition()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();
        CustomFieldDefinition definition = CreateDefinition();

        await repository.AddAsync(definition);
        await repository.SaveChangesAsync();

        CustomFieldDefinition? result = await repository.GetByIdAsync(definition.Id);

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Test Field");
        result.EntityType.Should().Be("Invoice");
        result.FieldType.Should().Be(CustomFieldType.Text);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();

        CustomFieldDefinition? result = await repository.GetByIdAsync(CustomFieldDefinitionId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEntityTypeAsync_ReturnsActiveDefinitionsOrdered()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();
        CustomFieldDefinition def1 = CreateDefinition(entityType: "Invoice");
        def1.SetDisplayOrder(2, TestUserId);
        CustomFieldDefinition def2 = CreateDefinition(entityType: "Invoice");
        def2.SetDisplayOrder(1, TestUserId);

        await repository.AddAsync(def1);
        await repository.AddAsync(def2);
        await repository.SaveChangesAsync();

        IReadOnlyList<CustomFieldDefinition> result = await repository.GetByEntityTypeAsync("Invoice");

        result.Should().HaveCountGreaterThanOrEqualTo(2);
        int idx1 = result.ToList().FindIndex(d => d.Id == def1.Id);
        int idx2 = result.ToList().FindIndex(d => d.Id == def2.Id);
        idx2.Should().BeLessThan(idx1, "lower DisplayOrder should come first");
    }

    [Fact]
    public async Task GetByEntityTypeAsync_ExcludesInactiveByDefault()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();
        CustomFieldDefinition activeDef = CreateDefinition(entityType: "Invoice");
        CustomFieldDefinition inactiveDef = CreateDefinition(entityType: "Invoice");
        inactiveDef.Deactivate(TestUserId);
        inactiveDef.ClearDomainEvents();

        await repository.AddAsync(activeDef);
        await repository.AddAsync(inactiveDef);
        await repository.SaveChangesAsync();

        IReadOnlyList<CustomFieldDefinition> result = await repository.GetByEntityTypeAsync("Invoice");

        result.Should().Contain(d => d.Id == activeDef.Id);
        result.Should().NotContain(d => d.Id == inactiveDef.Id);
    }

    [Fact]
    public async Task GetByEntityTypeAsync_IncludesInactiveWhenRequested()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();
        CustomFieldDefinition inactiveDef = CreateDefinition(entityType: "Invoice");
        inactiveDef.Deactivate(TestUserId);
        inactiveDef.ClearDomainEvents();

        await repository.AddAsync(inactiveDef);
        await repository.SaveChangesAsync();

        IReadOnlyList<CustomFieldDefinition> result = await repository.GetByEntityTypeAsync("Invoice", includeInactive: true);

        result.Should().Contain(d => d.Id == inactiveDef.Id);
    }

    [Fact]
    public async Task FieldKeyExistsAsync_WhenExists_ReturnsTrue()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();
        string fieldKey = $"fk_{Guid.NewGuid():N}".Substring(0, 20);
        CustomFieldDefinition definition = CreateDefinition(fieldKey);

        await repository.AddAsync(definition);
        await repository.SaveChangesAsync();

        bool exists = await repository.FieldKeyExistsAsync("Invoice", fieldKey);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task FieldKeyExistsAsync_WhenNotExists_ReturnsFalse()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();

        bool exists = await repository.FieldKeyExistsAsync("Invoice", "nonexistent_key");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingDefinition()
    {
        CustomFieldDefinitionRepository repository = CreateRepository();
        CustomFieldDefinition definition = CreateDefinition(entityType: "Invoice");

        await repository.AddAsync(definition);
        await repository.SaveChangesAsync();

        definition.UpdateDisplayName("Updated Name", TestUserId);
        await repository.UpdateAsync(definition);
        await repository.SaveChangesAsync();

        CustomFieldDefinition? result = await repository.GetByIdAsync(definition.Id);

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Updated Name");
    }
}
